using ILGPU;
using ILGPU.Runtime;
using SpawnDev.SpawnJS;
using SpawnDev.ILGPU;
using SpawnDev.ILGPU.WebGPU;
using SpawnDev.VoxelEngine;
using SpawnDev.VoxelEngine.Destruction;
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
    private readonly SpawnJSRuntime _js;
    private Context? _context;
    private Accelerator? _accelerator;

    // Heightmap kernel (kept - generates terrain heights on GPU)
    private Action<Index1D, ArrayView<int>, ArrayView<int>, float, float, float, float, float, int, int>? _heightmapKernel;
    private MemoryBuffer1D<int, Stride1D.Dense>? _permBuffer;
    // Reused heightmap output buffer - avoid Allocate1D(256) every column miss.
    private MemoryBuffer1D<int, Stride1D.Dense>? _heightmapOutBuffer;

    // VoxelEngine greedy mesh pipeline (replaces old per-face MeshKernel)
    private VoxelMeshPipeline? _meshPipeline;
    private BlockColumnCarveService? _carve;

    // Serialize mesh dispatches - VoxelMeshPipeline shares intermediate GPU buffers
    private readonly SemaphoreSlim _meshLock = new(1, 1);

    // Dig remesh waiters - stream mesh releases the lock if dig is waiting.
    private int _digWaiters;

    public Accelerator? Accelerator => _accelerator;
    public BlockColumnCarveService? Carve => _carve;
    public bool IsInitialized { get; private set; }
    public string? BackendName { get; private set; }
    /// <summary>Milliseconds spent waiting on _meshLock in the last mesh call (F3).</summary>
    public double LastMeshLockWaitMs { get; private set; }

    /// <summary>Call around dig remesh so in-flight stream mesh yields the shared pipeline lock.</summary>
    public void BeginDigRemesh() => Interlocked.Increment(ref _digWaiters);
    public void EndDigRemesh() => Interlocked.Decrement(ref _digWaiters);

    public VoxelEngineService(SpawnJSRuntime js)
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

        _heightmapOutBuffer = _accelerator.Allocate1D<int>(256);

        // VoxelEngine greedy mesh pipeline (face cull + greedy merge on GPU)
        _meshPipeline = new VoxelMeshPipeline(_accelerator);
        _carve = new BlockColumnCarveService(_accelerator);

        Console.WriteLine($"[VoxelEngine] {BackendName}");
        IsInitialized = true;
    }

    public void SetPermutationTable(int[] permTable)
    {
        _permBuffer?.Dispose();
        _permBuffer = _accelerator!.Allocate1D(permTable);
    }

    /// <summary>
    /// Generate a 16x16 heightmap into a caller-owned buffer (or returns a pooled copy).
    /// Prefer <see cref="FillColumnFromHeightmapAsync"/> to avoid host round-trip on the dig path.
    /// </summary>
    public async Task<int[]> GenerateHeightmapAsync(int chunkX, int chunkZ)
    {
        if (_heightmapKernel == null || _permBuffer == null || _heightmapOutBuffer == null)
            throw new InvalidOperationException("Not initialized");

        _heightmapKernel(
            (Index1D)256,
            _permBuffer.View,
            _heightmapOutBuffer.View,
            chunkX * 16f, chunkZ * 16f,
            TerrainGenerator.NoiseScale,
            TerrainGenerator.HeightScale,
            TerrainGenerator.BaseHeight,
            4, Models.ChunkData.Height);

        await _accelerator!.SynchronizeAsync();
        // Terminal sink for callers that still need CPU heights (legacy). Prefer FillColumnFromHeightmapAsync.
        return await _heightmapOutBuffer.CopyToHostAsync();
    }

    /// <summary>
    /// Run heightmap on GPU then fill a byte[] column on CPU from the small 256-int
    /// heightmap without allocating a new heightmap buffer each call. The CopyToHost
    /// of 256 ints is unavoidable until a GPU fill kernel lands; the win is no
    /// Allocate1D churn and a single reused output buffer.
    /// </summary>
    public async Task FillColumnFromHeightmapAsync(int chunkX, int chunkZ, byte[] column, Func<int, int, int, byte> fillColumnXz)
    {
        if (_heightmapKernel == null || _permBuffer == null || _heightmapOutBuffer == null)
            throw new InvalidOperationException("Not initialized");

        _heightmapKernel(
            (Index1D)256,
            _permBuffer.View,
            _heightmapOutBuffer.View,
            chunkX * 16f, chunkZ * 16f,
            TerrainGenerator.NoiseScale,
            TerrainGenerator.HeightScale,
            TerrainGenerator.BaseHeight,
            4, Models.ChunkData.Height);

        await _accelerator!.SynchronizeAsync();
        var heights = await _heightmapOutBuffer.CopyToHostAsync();
        for (int z = 0; z < 16; z++)
            for (int x = 0; x < 16; x++)
                fillColumnXz(x, z, heights[x + z * 16]);
        _ = column; // caller mutates column inside fillColumnXz
    }

    /// <summary>
    /// Generate mesh for all 16 vertical sections of a chunk (16x16x256 -> 16x 16x16x16).
    /// </summary>
    public async Task<List<(int sectionY, VoxelMeshPipeline.MeshResult mesh)>> GenerateChunkMeshesAsync(
        byte[] blocks,
        byte[]? neighborXMinus = null,
        byte[]? neighborXPlus = null,
        byte[]? neighborZMinus = null,
        byte[]? neighborZPlus = null,
        bool digPriority = false)
    {
        if (_meshPipeline == null)
            throw new InvalidOperationException("Not initialized");

        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (true)
        {
            // Prefer dig: don't even enter the FIFO WaitAsync queue while dig is waiting.
            if (!digPriority && Volatile.Read(ref _digWaiters) > 0)
            {
                await Task.Yield();
                continue;
            }

            await _meshLock.WaitAsync();
            // Stream path: if dig remesh is waiting, release and retry so pickaxe isn't stuck
            // behind a full-column mesh (was ~5s of dirt lag).
            if (!digPriority && Volatile.Read(ref _digWaiters) > 0)
            {
                _meshLock.Release();
                await Task.Yield();
                continue;
            }
            LastMeshLockWaitMs = sw.Elapsed.TotalMilliseconds;
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
    }

    /// <summary>
    /// Remesh only the listed section-Y indices (dig/terraform dirty queue).
    /// </summary>
    public async Task<List<(int sectionY, VoxelMeshPipeline.MeshResult mesh)>> GenerateChunkMeshesSectionsAsync(
        byte[] blocks,
        IReadOnlyCollection<int> sectionYs,
        byte[]? neighborXMinus = null,
        byte[]? neighborXPlus = null,
        byte[]? neighborZMinus = null,
        byte[]? neighborZPlus = null)
    {
        if (_meshPipeline == null)
            throw new InvalidOperationException("Not initialized");

        var sw = System.Diagnostics.Stopwatch.StartNew();
        // Dig priority is owned by WorldService.DrainDirtyRemeshAsync (BeginDigRemesh).
        await _meshLock.WaitAsync();
        LastMeshLockWaitMs = sw.Elapsed.TotalMilliseconds;
        try
        {
            return await _meshPipeline.MeshChunkColumnSectionsAsync(
                blocks, sectionYs,
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
        _carve?.Dispose();
        _permBuffer?.Dispose();
        _heightmapOutBuffer?.Dispose();
        _meshPipeline?.Dispose();
        _accelerator?.Dispose();
        _context?.Dispose();
        IsInitialized = false;
        return ValueTask.CompletedTask;
    }
}
