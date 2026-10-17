using ILGPU;
using ILGPU.Runtime;
using SpawnDev.SpawnJS;
using SpawnDev.ILGPU;
using SpawnDev.ILGPU.WebGPU;
using SpawnDev.VoxelEngine;
using SpawnDev.VoxelEngine.Destruction;
using SpawnDev.VoxelEngine.Meshing;
using SpawnDev.VoxelEngine.Lighting;
using SpawnDev.VoxelEngine.Rendering;
using LostSpawns.Rendering;

namespace LostSpawns.Services;

/// <summary>
/// Owns the ILGPU Context and Accelerator for GPU compute.
/// Uses VoxelEngine's ColumnMesher for greedy-merged GPU meshing.
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

    // VoxelEngine column mesher: a whole 16 x 256 x 16 column per call, quad lists pooled in one arena
    private ColumnMesher? _mesher;
    private QuadArena? _arena;
    private BlockColumnCarveService? _carve;

    // Sky light and block light of the world, worked out on the GPU (LightPropagator) into slots of a LightVolume the block shader reads
    private LightPropagator? _lightPropagator;
    private readonly SemaphoreSlim _lightLock = new(1, 1);
    /// <summary>The light of the columns round the camera (null where the backend is not WebGPU: the world is lit as before).</summary>
    public LightVolume? Light { get; private set; }
    /// <summary>The window of columns the light covers, in columns on a side.</summary>
    public const int LightSide = 25;

    // Serialize mesh dispatches - ColumnMesher shares intermediate GPU buffers
    private readonly SemaphoreSlim _meshLock = new(1, 1);

    // Dig remesh waiters - stream mesh releases the lock if dig is waiting.
    private int _digWaiters;

    public Accelerator? Accelerator => _accelerator;
    /// <summary>The shared quad buffers every column mesh lives in.</summary>
    public QuadArena? Arena => _arena;
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

        // VoxelEngine column mesher (face cull + greedy merge on GPU): one call per column, the quads carry their section so a
        // column is one draw, and every list sits in a few big arena buffers
        _arena = new QuadArena(_accelerator);
        _mesher = new ColumnMesher(_accelerator, Models.ChunkData.Height / 16)
        {
            MissingNeighborsAreSolid = false,
            EmbedSectionIndex = true,
            AmbientOcclusion = true,   // each face corner is shaded by what stands beside it, baked into the quad by the mesher
            Arena = _arena,
        };
        _mesher.SetBlockClasses(BlockStyle.Classes());
        _carve = new BlockColumnCarveService(_accelerator);
        InitLight();

        Console.WriteLine($"[VoxelEngine] {BackendName}");
        IsInitialized = true;
    }

    private void InitLight()
    {
        if (_accelerator is not WebGPUAccelerator webGpu) return;
        var device = webGpu.NativeAccelerator.NativeDevice;
        var queue = webGpu.NativeAccelerator.Queue;
        if (device == null || queue == null) return;
        Light = new LightVolume();
        Light.Init(device, queue, LightSide * LightSide, LightSide, Models.ChunkData.Height, 0);
        // Where no sky reaches it is dim, not black; the blocks' own glow is not used yet (a campfire is not a block)
        Light.DarkFloor = 0.10f;
        _lightPropagator = new LightPropagator(_accelerator, Models.ChunkData.Height / 16);
        var classes = BlockStyle.Classes();
        var passes = new byte[classes.Length];
        var dims = new byte[classes.Length];
        for (int i = 0; i < classes.Length; i++)
        {
            // Light goes through air, water, the crossed planes of plants and leaves; leaves take a level off it for each layer (a forest is dim underneath), everything else stops it
            int c = classes[i];
            passes[i] = (c == BlockClass.None || c == BlockClass.Water || c == BlockClass.Cross || c == BlockClass.Cutout) ? (byte)1 : (byte)0;
            if (c == BlockClass.Cutout) dims[i] = 1;
        }
        _lightPropagator.SetBlockLight(passes, ReadOnlySpan<byte>.Empty, dims);
    }

    /// <summary>Light one column into its slot; says which neighbours must be lit again (see <see cref="LightBorders"/>).</summary>
    public async Task<LightBorders> PropagateLightAsync(int slot, MemoryBuffer1D<ushort, Stride1D.Dense> column,
        MemoryBuffer1D<ushort, Stride1D.Dense>? negX, MemoryBuffer1D<ushort, Stride1D.Dense>? posX,
        MemoryBuffer1D<ushort, Stride1D.Dense>? negZ, MemoryBuffer1D<ushort, Stride1D.Dense>? posZ, bool firstLight,
        int columnX, int columnZ, IReadOnlyList<LightEmitter>? emitters)
    {
        if (_lightPropagator == null || Light == null) return LightBorders.None;
        await _lightLock.WaitAsync();
        try
        {
            // A slot taken over from another column starts with no memory of what the last one looked like
            if (firstLight) _lightPropagator.Forget(Light, slot);
            return await _lightPropagator.PropagateColumnAsync(Light, slot, column, negX, posX, negZ, posZ, columnX, columnZ, emitters);
        }
        finally
        {
            _lightLock.Release();
        }
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
    /// Upload a block column (the world's byte[] 16 x 256 x 16, x + z * 16 + y * 256) as the ushort ids the column mesher reads.
    /// Pass the column's existing buffer to overwrite it (an edit) instead of allocating another.
    /// The world's columns live in managed memory (collision, raycast and edits read them), so this is a terminal sink for them,
    /// not a readback.
    /// </summary>
    public MemoryBuffer1D<ushort, Stride1D.Dense> UploadColumn(byte[] blocks, MemoryBuffer1D<ushort, Stride1D.Dense>? existing = null)
    {
        if (existing == null) return _mesher!.CreateColumnBuffer(blocks);
        _mesher!.WriteColumnBuffer(existing, blocks);
        return existing;
    }

    /// <summary>
    /// Mesh one column on the GPU (opaque, plants and water quad lists, in the shared arena). Neighbours are the adjacent
    /// columns' block buffers, or null where not loaded (their faces are left visible).
    /// </summary>
    public async Task<ColumnMesh> MeshColumnAsync(
        MemoryBuffer1D<ushort, Stride1D.Dense> column,
        MemoryBuffer1D<ushort, Stride1D.Dense>? negX,
        MemoryBuffer1D<ushort, Stride1D.Dense>? posX,
        MemoryBuffer1D<ushort, Stride1D.Dense>? negZ,
        MemoryBuffer1D<ushort, Stride1D.Dense>? posZ,
        bool digPriority = false)
    {
        if (_mesher == null)
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
            // Stream path: if dig remesh is waiting, release and retry so the pickaxe isn't stuck behind a column mesh.
            if (!digPriority && Volatile.Read(ref _digWaiters) > 0)
            {
                _meshLock.Release();
                await Task.Yield();
                continue;
            }
            LastMeshLockWaitMs = sw.Elapsed.TotalMilliseconds;
            try
            {
                var mesh = await _mesher.MeshAsync(column, negX, posX, negZ, posZ);
                if (mesh.Overflow != 0)
                    Console.WriteLine($"[VoxelEngine] column mesh overflowed its quad scratch by {mesh.Overflow}");
                return mesh;
            }
            finally
            {
                _meshLock.Release();
            }
        }
    }

    public ValueTask DisposeAsync()
    {
        _carve?.Dispose();
        _permBuffer?.Dispose();
        _heightmapOutBuffer?.Dispose();
        _mesher?.Dispose();
        _arena?.Dispose();
        _accelerator?.Dispose();
        _context?.Dispose();
        IsInitialized = false;
        return ValueTask.CompletedTask;
    }
}
