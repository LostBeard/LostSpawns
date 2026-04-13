using ILGPU;
using ILGPU.Runtime;
using SpawnDev.BlazorJS;
using SpawnDev.ILGPU;
using SpawnDev.ILGPU.WebGPU;
using LostSpawns.Rendering;

namespace LostSpawns.Services;

/// <summary>
/// Owns the ILGPU Context and Accelerator for GPU compute.
/// Mesh dispatches are serialized via SemaphoreSlim with shared buffers.
/// All GPU readbacks use CopyToHostAsync (required by WebGPU backend).
/// </summary>
public class VoxelEngineService : IAsyncDisposable
{
    private readonly BlazorJSRuntime _js;
    private Context? _context;
    private Accelerator? _accelerator;

    // Heightmap kernel
    private Action<Index1D, ArrayView<int>, ArrayView<int>, float, float, float, float, float, int, int>? _heightmapKernel;
    private MemoryBuffer1D<int, Stride1D.Dense>? _permBuffer;

    // Mesh kernel + shared buffers (protected by _meshLock)
    private Action<Index1D, ArrayView<int>, ArrayView<float>, ArrayView<int>, int, int>? _meshKernel;
    private MemoryBuffer1D<int, Stride1D.Dense>? _meshBlockBuffer;
    private MemoryBuffer1D<float, Stride1D.Dense>? _meshVertexBuffer;
    private MemoryBuffer1D<int, Stride1D.Dense>? _meshCounterBuffer;
    private MemoryBuffer1D<float, Stride1D.Dense>? _meshResultBuffer;
    private readonly SemaphoreSlim _meshLock = new(1, 1);

    // Pooled arrays
    private readonly int[] _counterReset = new[] { 0 };
    private int[]? _blockIntsPool;

    private const int MaxOutputFloats = 600_000; // only used portion is ever transferred

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

        _heightmapKernel = _accelerator.LoadAutoGroupedStreamKernel<
            Index1D,
            ArrayView<int>, ArrayView<int>,
            float, float, float, float, float, int, int
        >(TerrainKernels.HeightmapKernel);

        _meshKernel = _accelerator.LoadAutoGroupedStreamKernel<
            Index1D,
            ArrayView<int>, ArrayView<float>, ArrayView<int>,
            int, int
        >(TerrainKernels.MeshKernel);

        _meshBlockBuffer = _accelerator.Allocate1D<int>(Models.ChunkData.Volume); // 16x16x256 = 65536
        _meshVertexBuffer = _accelerator.Allocate1D<float>(MaxOutputFloats);
        _meshCounterBuffer = _accelerator.Allocate1D<int>(1);
        _blockIntsPool = new int[Models.ChunkData.Volume];

        Console.WriteLine($"[VoxelEngineService] Initialized: {BackendName}");
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
    /// Generates mesh vertex data using the GPU kernel.
    /// Accepts raw byte[] blocks — conversion to int[] is done inside the semaphore
    /// to prevent data races when multiple concurrent tasks share the pooled array.
    /// </summary>
    public async Task<(float[] vertices, int vertexCount)> GenerateMeshAsync(byte[] blocks, int chunkX, int chunkZ)
    {
        if (_meshKernel == null)
            throw new InvalidOperationException("Not initialized");

        await _meshLock.WaitAsync();
        try
        {
            // Convert byte[] → int[] inside semaphore (pooled array is safe here)
            var blockInts = _blockIntsPool!;
            for (int i = 0; i < blocks.Length; i++)
                blockInts[i] = blocks[i];

            _meshBlockBuffer!.CopyFromCPU(blockInts);
            _meshCounterBuffer!.CopyFromCPU(_counterReset);

            _meshKernel(
                (Index1D)Models.ChunkData.Volume,
                _meshBlockBuffer.View,
                _meshVertexBuffer!.View,
                _meshCounterBuffer.View,
                chunkX * 16, chunkZ * 16);

            await _accelerator!.SynchronizeAsync();

            // Read counter
            var counterResult = await _meshCounterBuffer.CopyToHostAsync();
            int floatCount = counterResult[0];

            if (floatCount <= 0)
                return (Array.Empty<float>(), 0);

            if (floatCount > MaxOutputFloats)
                floatCount = MaxOutputFloats;

            // Ensure shared result buffer is large enough
            if (_meshResultBuffer == null || _meshResultBuffer.Length < floatCount)
            {
                _meshResultBuffer?.Dispose();
                // Allocate with some headroom to avoid frequent resizing
                int allocSize = Math.Max(floatCount, 100_000);
                _meshResultBuffer = _accelerator.Allocate1D<float>(allocSize);
            }

            // GPU→GPU sub-copy into shared result buffer (no per-call allocation)
            _meshResultBuffer.View.SubView(0, floatCount).CopyFrom(
                _meshVertexBuffer.View.SubView(0, floatCount));
            await _accelerator.SynchronizeAsync();

            // Only unavoidable allocation: the final float[] that gets stored as CpuData
            var usedVertices = await _meshResultBuffer.CopyToHostAsync(0, floatCount);
            return (usedVertices, floatCount / 9);
        }
        finally
        {
            _meshLock.Release();
        }
    }

    public ValueTask DisposeAsync()
    {
        _meshResultBuffer?.Dispose();
        _meshCounterBuffer?.Dispose();
        _meshVertexBuffer?.Dispose();
        _meshBlockBuffer?.Dispose();
        _permBuffer?.Dispose();
        _accelerator?.Dispose();
        _context?.Dispose();
        _meshLock.Dispose();
        IsInitialized = false;
        return ValueTask.CompletedTask;
    }
}
