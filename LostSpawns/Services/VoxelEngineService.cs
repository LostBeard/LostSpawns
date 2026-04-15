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

    // Pooled int[] for block format conversion (byte -> PackedBlock)
    private int[]? _packedBlocksPool;

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

        // Pooled array for block conversion
        _packedBlocksPool = new int[Models.ChunkData.Volume];

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
    /// Generate mesh using VoxelEngine's GPU greedy merge pipeline.
    /// Returns GPU-resident MeshResult - no CPU readback, no float[] allocations.
    /// The caller owns the MeshResult and must dispose QuadBuffer when the chunk unloads.
    /// </summary>
    /// <summary>
    /// Generate mesh for all 16 vertical sections of a chunk (16x16x256 -> 16x 16x16x16).
    /// VoxelEngine's occupancy columns are 64-bit, so max section height is 64.
    /// We split into standard 16-high sections for correct face culling.
    /// Returns a list of (sectionY, MeshResult) for non-empty sections.
    /// </summary>
    public async Task<List<(int sectionY, VoxelMeshPipeline.MeshResult mesh)>> GenerateChunkMeshesAsync(byte[] blocks)
    {
        if (_meshPipeline == null)
            throw new InvalidOperationException("Not initialized");

        var results = new List<(int, VoxelMeshPipeline.MeshResult)>();
        const int SectionHeight = 16;
        int sectionsPerColumn = Models.ChunkData.Height / SectionHeight; // 256/16 = 16

        await _meshLock.WaitAsync();
        try
        {
            var sectionBlocks = new int[Models.ChunkData.SizeXZ * Models.ChunkData.SizeXZ * SectionHeight]; // 16*16*16 = 4096

            for (int sy = 0; sy < sectionsPerColumn; sy++)
            {
                int yOffset = sy * SectionHeight;

                // Extract this section's blocks from the full chunk column
                for (int y = 0; y < SectionHeight; y++)
                    for (int z = 0; z < Models.ChunkData.SizeXZ; z++)
                        for (int x = 0; x < Models.ChunkData.SizeXZ; x++)
                        {
                            int srcIdx = x + z * Models.ChunkData.SizeXZ + (yOffset + y) * Models.ChunkData.SizeXZ * Models.ChunkData.SizeXZ;
                            byte blockType = blocks[srcIdx];
                            sectionBlocks[x + z * Models.ChunkData.SizeXZ + y * Models.ChunkData.SizeXZ * Models.ChunkData.SizeXZ] =
                                blockType > 0 ? PackedBlock.Pack(blockType) : 0;
                        }

                var result = await _meshPipeline.MeshSectionUnpaddedAsync(
                    sectionBlocks, Models.ChunkData.SizeXZ, SectionHeight);

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
