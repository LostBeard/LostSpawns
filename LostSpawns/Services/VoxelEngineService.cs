using ILGPU;
using ILGPU.Runtime;
using SpawnDev.BlazorJS;
using SpawnDev.ILGPU;
using SpawnDev.ILGPU.WebGPU;
using SpawnDev.VoxelEngine;
using SpawnDev.VoxelEngine.Meshing;
using LostSpawns.Rendering;

namespace LostSpawns.Services;

/// <summary>
/// Owns the ILGPU Context and Accelerator for GPU compute.
/// Uses VoxelEngine's VoxelMeshPipeline for greedy-merged GPU meshing.
/// Mesh output stays GPU-resident (no CPU readback, no float[] vertices).
///
/// Memory: ~25MB vs old 686MB (27x reduction via greedy merge + PackedQuad format).
/// </summary>
public class VoxelEngineService : IAsyncDisposable
{
    private readonly BlazorJSRuntime _js;
    private Context? _context;
    private Accelerator? _accelerator;

    // Heightmap kernel (kept - generates terrain heights on GPU)
    private Action<Index1D, ArrayView<int>, ArrayView<int>, float, float, float, float, float, int, int>? _heightmapKernel;
    private MemoryBuffer1D<int, Stride1D.Dense>? _permBuffer;

    // VoxelEngine greedy mesh pipeline (replaces old per-face MeshKernel)
    private VoxelMeshPipeline? _meshPipeline;

    // Serialize mesh dispatches - VoxelMeshPipeline shares intermediate GPU buffers
    private readonly SemaphoreSlim _meshLock = new(1, 1);

    public Accelerator? Accelerator => _accelerator;
    public bool IsInitialized { get; private set; }
    public string? BackendName { get; private set; }

    public VoxelEngineService(BlazorJSRuntime js)
    {
        _js = js;
    }

    public async Task InitAsync()
    {
        if (IsInitialized) return;

        var builder = Context.Create();
        await builder.AllAcceleratorsAsync();
        _context = builder.ToContext();

        _accelerator = await _context.CreatePreferredAcceleratorAsync();
        BackendName = _accelerator.AcceleratorType.ToString();

        // Heightmap kernel (GPU Perlin noise)
        _heightmapKernel = _accelerator.LoadAutoGroupedStreamKernel<
            Index1D,
            ArrayView<int>, ArrayView<int>,
            float, float, float, float, float, int, int
        >(TerrainKernels.HeightmapKernel);

        // VoxelEngine greedy mesh pipeline (face cull + greedy merge on GPU)
        _meshPipeline = new VoxelMeshPipeline(_accelerator);

        Console.WriteLine($"[VoxelEngine] {BackendName}");
        IsInitialized = true;
    }

    public void SetPermutationTable(int[] permTable)
    {
        _permBuffer?.Dispose();
        _permBuffer = _accelerator!.Allocate1D(permTable);
    }

    public async Task<int[]> GenerateHeightmapAsync(int chunkX, int chunkZ)
    {
        if (_heightmapKernel == null || _permBuffer == null)
            throw new InvalidOperationException("Not initialized");

        using var outputBuffer = _accelerator!.Allocate1D<int>(256);

        _heightmapKernel(
            (Index1D)256,
            _permBuffer.View,
            outputBuffer.View,
            chunkX * 16f, chunkZ * 16f,
            TerrainGenerator.NoiseScale,
            TerrainGenerator.HeightScale,
            TerrainGenerator.BaseHeight,
            4, Models.ChunkData.Height);

        await _accelerator.SynchronizeAsync();
        return await outputBuffer.CopyToHostAsync();
    }

    /// <summary>
    /// Generate mesh for all 16 vertical sections of a chunk (16x16x256 -> 16x 16x16x16).
    /// VoxelEngine's occupancy columns are 64-bit, so max section height is 64.
    /// We split into standard 16-high sections for correct face culling.
    /// Returns a list of (sectionY, MeshResult) for non-empty sections.
    ///
    /// Supplying neighbor chunk blocks fills the XZ padding border, which hides the boundary
    /// faces that would otherwise show through at chunk edges. Pass null for any neighbor that
    /// is not yet loaded - those boundary faces will render (air padding) until the neighbor
    /// arrives. Intra-chunk Y boundaries (Y=16,32,...,240) are now padded by reading the
    /// adjacent section's interior boundary layer from the same chunk column and feeding it
    /// to the kernel's Y-pad slabs - no see-through at section seams within a chunk.
    /// Inter-chunk Y boundaries (world top/bottom) remain air since chunks span the full
    /// world height of 256.
    /// </summary>
    /// <param name="blocks">Target chunk block data, flat byte[] of size SizeXZ*SizeXZ*Height.</param>
    /// <param name="neighborXMinus">Blocks of chunk at (cx-1, cz), or null.</param>
    /// <param name="neighborXPlus">Blocks of chunk at (cx+1, cz), or null.</param>
    /// <param name="neighborZMinus">Blocks of chunk at (cx, cz-1), or null.</param>
    /// <param name="neighborZPlus">Blocks of chunk at (cx, cz+1), or null.</param>
    public async Task<List<(int sectionY, VoxelMeshPipeline.MeshResult mesh)>> GenerateChunkMeshesAsync(
        byte[] blocks,
        byte[]? neighborXMinus = null,
        byte[]? neighborXPlus = null,
        byte[]? neighborZMinus = null,
        byte[]? neighborZPlus = null)
    {
        if (_meshPipeline == null)
            throw new InvalidOperationException("Not initialized");

        var results = new List<(int, VoxelMeshPipeline.MeshResult)>();
        const int SectionHeight = 16;
        const int SizeXZ = Models.ChunkData.SizeXZ;
        const int PaddedXZ = SizeXZ + 2;
        int sectionsPerColumn = Models.ChunkData.Height / SectionHeight; // 256/16 = 16

        await _meshLock.WaitAsync();
        try
        {
            // Padded layout: (SizeXZ+2) x (SizeXZ+2) x SectionHeight. Interior is at x,z in [1..SizeXZ].
            var padded = new int[PaddedXZ * PaddedXZ * SectionHeight];
            // Per-XZ solid slabs for the section above and below this one (kernel reads bit 0).
            var yPadMinusSlab = new int[PaddedXZ * PaddedXZ];
            var yPadPlusSlab = new int[PaddedXZ * PaddedXZ];

            for (int sy = 0; sy < sectionsPerColumn; sy++)
            {
                int yOffset = sy * SectionHeight;
                Array.Clear(padded);

                for (int y = 0; y < SectionHeight; y++)
                {
                    int paddedYBase = y * PaddedXZ * PaddedXZ;
                    int srcYBase = (yOffset + y) * SizeXZ * SizeXZ;

                    // Interior: chunk blocks at (1..SizeXZ, 1..SizeXZ) in padded coords
                    for (int z = 0; z < SizeXZ; z++)
                        for (int x = 0; x < SizeXZ; x++)
                        {
                            byte b = blocks[x + z * SizeXZ + srcYBase];
                            padded[(x + 1) + (z + 1) * PaddedXZ + paddedYBase] = b > 0 ? PackedBlock.Pack(b) : 0;
                        }

                    // -X edge (padded x=0) from neighbor (cx-1, cz)'s +X-most column (source x=SizeXZ-1)
                    if (neighborXMinus != null)
                        for (int z = 0; z < SizeXZ; z++)
                        {
                            byte b = neighborXMinus[(SizeXZ - 1) + z * SizeXZ + srcYBase];
                            padded[0 + (z + 1) * PaddedXZ + paddedYBase] = b > 0 ? PackedBlock.Pack(b) : 0;
                        }

                    // +X edge (padded x=SizeXZ+1) from neighbor (cx+1, cz)'s -X-most column (source x=0)
                    if (neighborXPlus != null)
                        for (int z = 0; z < SizeXZ; z++)
                        {
                            byte b = neighborXPlus[0 + z * SizeXZ + srcYBase];
                            padded[(SizeXZ + 1) + (z + 1) * PaddedXZ + paddedYBase] = b > 0 ? PackedBlock.Pack(b) : 0;
                        }

                    // -Z edge (padded z=0) from neighbor (cx, cz-1)'s +Z-most row (source z=SizeXZ-1)
                    if (neighborZMinus != null)
                        for (int x = 0; x < SizeXZ; x++)
                        {
                            byte b = neighborZMinus[x + (SizeXZ - 1) * SizeXZ + srcYBase];
                            padded[(x + 1) + 0 * PaddedXZ + paddedYBase] = b > 0 ? PackedBlock.Pack(b) : 0;
                        }

                    // +Z edge (padded z=SizeXZ+1) from neighbor (cx, cz+1)'s -Z-most row (source z=0)
                    if (neighborZPlus != null)
                        for (int x = 0; x < SizeXZ; x++)
                        {
                            byte b = neighborZPlus[x + 0 * SizeXZ + srcYBase];
                            padded[(x + 1) + (SizeXZ + 1) * PaddedXZ + paddedYBase] = b > 0 ? PackedBlock.Pack(b) : 0;
                        }
                }

                // Build Y-pad slabs from adjacent sections within the same chunk column.
                // Only interior (x,z) is read by the kernel; the padding border stays zero.
                int[]? yMinusArg = null;
                int[]? yPlusArg = null;

                if (sy > 0)
                {
                    Array.Clear(yPadMinusSlab);
                    int srcYBase = (yOffset - 1) * SizeXZ * SizeXZ;
                    for (int z = 0; z < SizeXZ; z++)
                        for (int x = 0; x < SizeXZ; x++)
                        {
                            byte b = blocks[x + z * SizeXZ + srcYBase];
                            yPadMinusSlab[(x + 1) + (z + 1) * PaddedXZ] = b > 0 ? PackedBlock.Pack(b) : 0;
                        }
                    yMinusArg = yPadMinusSlab;
                }

                if (sy < sectionsPerColumn - 1)
                {
                    Array.Clear(yPadPlusSlab);
                    int srcYBase = (yOffset + SectionHeight) * SizeXZ * SizeXZ;
                    for (int z = 0; z < SizeXZ; z++)
                        for (int x = 0; x < SizeXZ; x++)
                        {
                            byte b = blocks[x + z * SizeXZ + srcYBase];
                            yPadPlusSlab[(x + 1) + (z + 1) * PaddedXZ] = b > 0 ? PackedBlock.Pack(b) : 0;
                        }
                    yPlusArg = yPadPlusSlab;
                }

                var result = await _meshPipeline.MeshSectionAsync(padded, SizeXZ, SectionHeight, yMinusArg, yPlusArg);
                if (result.HasMesh)
                    results.Add((sy, result));
            }

            return results;
        }
        finally
        {
            _meshLock.Release();
        }
    }

    public ValueTask DisposeAsync()
    {
        _permBuffer?.Dispose();
        _accelerator?.Dispose();
        _context?.Dispose();
        IsInitialized = false;
        return ValueTask.CompletedTask;
    }
}
