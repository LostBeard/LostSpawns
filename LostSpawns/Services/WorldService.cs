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

    private const int MaxConcurrentGpu = 4;

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
    /// Falls back to procedural generation for the terrain generator but uses
    /// the heightmap data for chunk generation when available.
    /// </summary>
    public void InitWithHeightmap(HeightmapLoader loader, int seed = 42)
    {
        if (IsInitialized) return;
        Seed = seed;
        _generator = new TerrainGenerator(seed);
        _heightmapLoader = loader;

        // GPU terrain gen stays with Perlin for now - heightmap uses CPU path
        _gpuReady = false;

        Console.WriteLine($"[WorldService] Real-world heightmap loaded: {loader.GridSize}x{loader.GridSize}, {loader.MapSizeInChunks} chunks");
        IsInitialized = true;
    }

    public List<(int cx, int cz)> UpdateDesiredChunks(Vector3 playerPos, int drawDistance)
    {
        int pcx = (int)MathF.Floor(playerPos.X / ChunkData.SizeXZ);
        int pcz = (int)MathF.Floor(playerPos.Z / ChunkData.SizeXZ);

        if (pcx == _lastCX && pcz == _lastCZ && (_chunks.Count > 0 || _pendingQueue.Count > 0 || _inFlight.Count > 0))
        {
            if (_gpuReady) DispatchGpuPending();
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

        return removed;
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
            // Step 1: GPU heightmap
            var heightmap = await _engine.GenerateHeightmapAsync(cx, cz);

            if (_chunks.ContainsKey((cx, cz))) { _inFlight.Remove((cx, cz)); return; }

            // Step 2: Fill blocks from heightmap (lightweight CPU work)
            var chunk = _generator!.GenerateChunkFromHeightmap(cx, cz, heightmap);

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

        // Generate sequentially using GPU pipeline (same noise as streaming)
        foreach (var (cx, cz) in toGenerate)
        {
            try
            {
                var heightmap = await _engine.GenerateHeightmapAsync(cx, cz);
                var chunk = _generator!.GenerateChunkFromHeightmap(cx, cz, heightmap);
                var (mesh, vertexCount) = await _engine.GenerateMeshAsync(chunk.Blocks, cx, cz);
                _chunks[(cx, cz)] = true;
                if (mesh.Length > 0)
                    results.Add((cx, cz, mesh));
            }
            catch
            {
                // CPU fallback
                var chunk = _generator!.GenerateChunk(cx, cz);
                var mesh = VoxelMesher.GenerateMesh(chunk);
                _chunks[(cx, cz)] = true;
                if (mesh.Length > 0)
                    results.Add((cx, cz, mesh));
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
