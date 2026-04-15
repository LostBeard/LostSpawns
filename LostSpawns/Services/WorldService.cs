using System.Numerics;
using LostSpawns.Models;
using LostSpawns.Rendering;

namespace LostSpawns.Services;

/// <summary>
/// Manages the voxel world: chunk loading/unloading around the player.
/// Full GPU pipeline: heightmap → block fill → GPU mesh → ready queue.
/// Game loop just dequeues fully-meshed chunks for upload.
/// </summary>
public class WorldService
{
    private readonly VoxelEngineService _engine;
    private readonly Dictionary<(int cx, int cz), bool> _chunks = new();
    private readonly Queue<(int cx, int cz)> _pendingQueue = new();
    private readonly HashSet<(int cx, int cz)> _inFlight = new();
    private readonly Queue<(int cx, int cz, float[] mesh)> _readyQueue = new();
    private TerrainGenerator? _generator;
    private HeightmapLoader? _heightmapLoader;
    private int _lastCX = int.MinValue;
    private int _lastCZ = int.MinValue;
    private bool _gpuReady;

    private const int MaxConcurrentGpu = 16;

    public int Seed { get; private set; }
    public bool IsInitialized { get; private set; }
    public int LoadedCount => _chunks.Count;
    public bool HasPendingChunks => _pendingQueue.Count > 0 || _inFlight.Count > 0 || _readyQueue.Count > 0;
    public int PendingCount => _pendingQueue.Count + _inFlight.Count + _readyQueue.Count;

    /// <summary>The heightmap loader, if a real-world map is loaded.</summary>
    public HeightmapLoader? HeightmapLoader => _heightmapLoader;

    public WorldService(VoxelEngineService engine)
    {
        _engine = engine;
    }

    /// <summary>
    /// Initialize with procedural terrain (Perlin noise).
    /// </summary>
    public void Init(int seed = 42)
    {
        if (IsInitialized) return;
        Seed = seed;
        _generator = new TerrainGenerator(seed);
        _heightmapLoader = null;

        if (_engine.IsInitialized)
        {
            var noise = new PerlinNoise(seed);
            _engine.SetPermutationTable(noise.PermTable);
            _gpuReady = true;
            Console.WriteLine("[WorldService] GPU heightmap + mesh generation enabled");
        }

        IsInitialized = true;
    }

    /// <summary>
    /// Initialize with a real-world heightmap (e.g., Deer Isle terrain data).
    /// Block filling from heightmap is a simple array lookup (CPU).
    /// Mesh generation runs on GPU via ILGPU (the expensive part).
    /// </summary>
    public void InitWithHeightmap(HeightmapLoader loader, int seed = 42)
    {
        if (IsInitialized) return;
        Seed = seed;
        _generator = new TerrainGenerator(seed);
        _heightmapLoader = loader;

        if (_engine.IsInitialized)
        {
            _gpuReady = true;
            Console.WriteLine("[WorldService] Heightmap mode: block fill from heightmap + GPU mesh generation");
        }

        Console.WriteLine($"[WorldService] Real-world heightmap loaded: {loader.GridSize}x{loader.GridSize}, {loader.MapSizeInChunks} chunks");
        IsInitialized = true;
    }

    public List<(int cx, int cz)> UpdateDesiredChunks(Vector3 playerPos, int drawDistance)
    {
        int pcx = (int)MathF.Floor(playerPos.X / ChunkData.SizeXZ);
        int pcz = (int)MathF.Floor(playerPos.Z / ChunkData.SizeXZ);

        if (pcx == _lastCX && pcz == _lastCZ && (_chunks.Count > 0 || _pendingQueue.Count > 0 || _inFlight.Count > 0))
        {
            if (_gpuReady)
                DispatchGpuPending();
            else
                ProcessCpuPending();
            return new();
        }

        _lastCX = pcx;
        _lastCZ = pcz;

        var desired = new HashSet<(int, int)>();
        int r2 = drawDistance * drawDistance;
        for (int dx = -drawDistance; dx <= drawDistance; dx++)
        for (int dz = -drawDistance; dz <= drawDistance; dz++)
        {
            if (dx * dx + dz * dz <= r2)
                desired.Add((pcx + dx, pcz + dz));
        }

        var removed = new List<(int, int)>();
        foreach (var key in _chunks.Keys.ToList())
        {
            if (!desired.Contains(key))
            {
                _chunks.Remove(key);
                removed.Add(key);
            }
        }

        _pendingQueue.Clear();
        var toAdd = desired
            .Where(k => !_chunks.ContainsKey(k) && !_inFlight.Contains(k))
            .OrderBy(k => (k.Item1 - pcx) * (k.Item1 - pcx) + (k.Item2 - pcz) * (k.Item2 - pcz));

        foreach (var key in toAdd)
            _pendingQueue.Enqueue(key);

        if (_gpuReady)
            DispatchGpuPending();
        else
            ProcessCpuPending();

        return removed;
    }

    /// <summary>Process pending chunks via CPU when GPU heightmap isn't available (heightmap mode).</summary>
    private void ProcessCpuPending()
    {
        // Generate up to 4 chunks per frame to avoid blocking
        int processed = 0;
        while (_pendingQueue.Count > 0 && processed < 4)
        {
            var key = _pendingQueue.Dequeue();
            if (_chunks.ContainsKey(key)) continue;
            FallbackCpuGenerate(key.Item1, key.Item2);
            processed++;
        }
    }

    private void DispatchGpuPending()
    {
        while (_inFlight.Count < MaxConcurrentGpu && _pendingQueue.Count > 0)
        {
            var key = _pendingQueue.Dequeue();
            if (_chunks.ContainsKey(key)) continue;
            _inFlight.Add(key);
            _ = GenerateChunkFullGpuAsync(key.Item1, key.Item2);
        }
    }

    /// <summary>
    /// Full async GPU pipeline: heightmap → fill blocks → GPU mesh → queue result.
    /// Only lightweight CPU work (block filling) happens here.
    /// </summary>
    private async Task GenerateChunkFullGpuAsync(int cx, int cz)
    {
        try
        {
            Models.ChunkData chunk;
            if (_heightmapLoader != null)
            {
                // Heightmap mode: block fill from real terrain data (simple lookup)
                chunk = _heightmapLoader.GenerateChunk(cx, cz);
            }
            else
            {
                // Procedural mode: GPU Perlin heightmap + CPU block fill
                var heightmap = await _engine.GenerateHeightmapAsync(cx, cz);
                if (_chunks.ContainsKey((cx, cz))) { _inFlight.Remove((cx, cz)); return; }
                chunk = _generator!.GenerateChunkFromHeightmap(cx, cz, heightmap);
            }

            if (_chunks.ContainsKey((cx, cz))) { _inFlight.Remove((cx, cz)); return; }

            // Step 3: GPU meshing — blocks passed as byte[], converted inside semaphore
            var (mesh, vertexCount) = await _engine.GenerateMeshAsync(chunk.Blocks, cx, cz);

            _inFlight.Remove((cx, cz));
            if (_chunks.ContainsKey((cx, cz))) return;

            _chunks[(cx, cz)] = true;
            if (mesh.Length > 0)
                _readyQueue.Enqueue((cx, cz, mesh));

            DispatchGpuPending();
        }
        catch (Exception ex)
        {
            _inFlight.Remove((cx, cz));
            Console.WriteLine($"[WorldService] GPU pipeline error ({cx},{cz}): {ex.Message}");
            // Fallback to CPU
            FallbackCpuGenerate(cx, cz);
            DispatchGpuPending();
        }
    }

    /// <summary>CPU fallback for when GPU fails.</summary>
    private void FallbackCpuGenerate(int cx, int cz)
    {
        if (_chunks.ContainsKey((cx, cz))) return;
        // Use heightmap loader if available, otherwise procedural
        var chunk = _heightmapLoader != null
            ? _heightmapLoader.GenerateChunk(cx, cz)
            : _generator!.GenerateChunk(cx, cz);
        var mesh = VoxelMesher.GenerateMesh(chunk);
        _chunks[(cx, cz)] = true;
        if (mesh.Length > 0)
            _readyQueue.Enqueue((cx, cz, mesh));
    }

    /// <summary>
    /// Called per frame. Returns up to maxCount fully-meshed chunks for GPU upload.
    /// </summary>
    public List<(int cx, int cz, float[] mesh)> ProcessReadyChunks(int maxCount = 2)
    {
        var results = new List<(int, int, float[])>();
        while (results.Count < maxCount && _readyQueue.Count > 0)
        {
            var item = _readyQueue.Dequeue();
            if (!_chunks.ContainsKey((item.cx, item.cz))) continue; // was removed
            results.Add(item);
        }
        return results;
    }

    /// <summary>Async initial load using GPU pipeline for noise consistency with streaming chunks.</summary>
    public async Task<List<(int cx, int cz, float[] mesh)>> GenerateChunksAsync(Vector3 playerPos, int drawDistance)
    {
        _lastCX = (int)MathF.Floor(playerPos.X / ChunkData.SizeXZ);
        _lastCZ = (int)MathF.Floor(playerPos.Z / ChunkData.SizeXZ);

        var results = new List<(int, int, float[])>();
        int r2 = drawDistance * drawDistance;

        var toGenerate = new List<(int cx, int cz)>();
        for (int dx = -drawDistance; dx <= drawDistance; dx++)
        for (int dz = -drawDistance; dz <= drawDistance; dz++)
        {
            if (dx * dx + dz * dz > r2) continue;
            int cx = _lastCX + dx, cz = _lastCZ + dz;
            if (_chunks.ContainsKey((cx, cz))) continue;
            toGenerate.Add((cx, cz));
        }

        // Generate sequentially: heightmap block fill + GPU mesh
        foreach (var (cx, cz) in toGenerate)
        {
            try
            {
                ChunkData chunk;
                if (_heightmapLoader != null)
                    chunk = _heightmapLoader.GenerateChunk(cx, cz);
                else
                {
                    var heightmap = await _engine.GenerateHeightmapAsync(cx, cz);
                    chunk = _generator!.GenerateChunkFromHeightmap(cx, cz, heightmap);
                }
                // GPU mesh generation (ILGPU kernel)
                var (mesh, vertexCount) = await _engine.GenerateMeshAsync(chunk.Blocks, cx, cz);
                _chunks[(cx, cz)] = true;
                if (mesh.Length > 0)
                    results.Add((cx, cz, mesh));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[WorldService] Chunk ({cx},{cz}) error: {ex.Message}");
            }
        }
        return results;
    }

    public int GetHeightAt(float worldX, float worldZ)
    {
        if (_heightmapLoader != null)
            return _heightmapLoader.GetElevation(worldX, worldZ);
        return _generator?.GetHeight(worldX, worldZ) ?? 30;
    }

    /// <summary>Resets all state so the service can be re-initialized.</summary>
    public void Reset()
    {
        _chunks.Clear();
        _pendingQueue.Clear();
        _inFlight.Clear();
        _readyQueue.Clear();
        _lastCX = int.MinValue;
        _lastCZ = int.MinValue;
        _gpuReady = false;
        _generator = null;
        IsInitialized = false;
    }
}
