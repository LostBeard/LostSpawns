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

        // Delegate to the library: VoxelMeshPipeline.MeshChunkColumnAsync owns the padding
        // assembly, intra-chunk Y-slab derivation, and (critically) the all-air fast path
        // that skips kernel dispatches for sections whose interior is entirely zero. For a
        // Lost Spawns terrain column (geometry concentrated in a narrow Y band) that is
        // 13-of-16 sections with no GPU work.
        await _meshLock.WaitAsync();
        try
        {
            return await _meshPipeline.MeshChunkColumnAsync(
                blocks,
                neighborXMinus, neighborXPlus, neighborZMinus, neighborZPlus,
                Models.ChunkData.SizeXZ,
                Models.ChunkData.Height);
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
