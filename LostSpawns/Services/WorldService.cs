using System.Numerics;
using ILGPU;
using ILGPU.Runtime;
using SpawnDev.BlazorJS.JSObjects;
using SpawnDev.ILGPU.WebGPU;
using SpawnDev.VoxelEngine;
using SpawnDev.VoxelEngine.Meshing;
using LostSpawns.Models;
using LostSpawns.Rendering;

namespace LostSpawns.Services;

/// <summary>
/// Manages the voxel world: chunk loading/unloading around the player.
/// Full GPU pipeline: heightmap -> block fill -> VoxelEngine greedy mesh -> GPU-resident quads.
/// No CPU readback - mesh data stays on GPU from generation to rendering.
/// </summary>
public class WorldService
{
    private readonly VoxelEngineService _engine;
    private readonly Dictionary<(int cx, int cz), ChunkMesh> _chunks = new();
    private readonly Queue<(int cx, int cz)> _pendingQueue = new();
    private readonly HashSet<(int cx, int cz)> _inFlight = new();
    private readonly Queue<(int cx, int cz, ChunkMesh mesh)> _readyQueue = new();
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

    /// <summary>All loaded chunks with their GPU mesh data. Used by RenderService for drawing.</summary>
    public IReadOnlyDictionary<(int cx, int cz), ChunkMesh> Chunks => _chunks;

    public WorldService(VoxelEngineService engine)
    {
        _engine = engine;
    }

    /// <summary>Initialize with procedural terrain (Perlin noise).</summary>
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
            Console.WriteLine("[WorldService] GPU heightmap + VoxelEngine greedy mesh enabled");
        }

        IsInitialized = true;
    }

    /// <summary>Initialize with a real-world heightmap (e.g., Deer Isle terrain data).</summary>
    public void InitWithHeightmap(HeightmapLoader loader, int seed = 42)
    {
        if (IsInitialized) return;
        Seed = seed;
        _generator = new TerrainGenerator(seed);
        _heightmapLoader = loader;

        if (_engine.IsInitialized)
        {
            _gpuReady = true;
            Console.WriteLine("[WorldService] Heightmap mode: block fill from heightmap + VoxelEngine greedy mesh");
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
            DispatchGpuPending();
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
                // Dispose GPU buffer when chunk unloads
                if (_chunks.TryGetValue(key, out var mesh))
                    mesh.Dispose();
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
            _ = GenerateChunkGpuAsync(key.Item1, key.Item2);
        }
    }

    /// <summary>
    /// Full GPU pipeline: heightmap -> fill blocks -> VoxelEngine greedy mesh.
    /// Mesh data stays GPU-resident (no CPU readback).
    /// </summary>
    private async Task GenerateChunkGpuAsync(int cx, int cz)
    {
        try
        {
            ChunkData chunk;
            if (_heightmapLoader != null)
            {
                chunk = _heightmapLoader.GenerateChunk(cx, cz);
            }
            else
            {
                var heightmap = await _engine.GenerateHeightmapAsync(cx, cz);
                if (_chunks.ContainsKey((cx, cz))) { _inFlight.Remove((cx, cz)); return; }
                chunk = _generator!.GenerateChunkFromHeightmap(cx, cz, heightmap);
            }

            if (_chunks.ContainsKey((cx, cz))) { _inFlight.Remove((cx, cz)); return; }

            // VoxelEngine greedy mesh: GPU face cull + greedy merge -> PackedQuad GPU buffer
            var meshResult = await _engine.GenerateMeshAsync(chunk.Blocks);

            _inFlight.Remove((cx, cz));
            if (_chunks.ContainsKey((cx, cz)))
            {
                // Chunk was removed while we were generating - dispose the buffer
                meshResult.QuadBuffer?.Dispose();
                return;
            }

            if (meshResult.HasMesh)
            {
                var gpuBuffer = meshResult.QuadBuffer!.GetGPUBuffer();
                var chunkMesh = new ChunkMesh
                {
                    QuadBuffer = gpuBuffer,
                    QuadCount = meshResult.QuadCount,
                    IlgpuBuffer = meshResult.QuadBuffer,
                };
                _readyQueue.Enqueue((cx, cz, chunkMesh));
            }
            else
            {
                // Empty chunk (all air) - mark as loaded with no mesh
                _chunks[(cx, cz)] = ChunkMesh.Empty;
            }

            DispatchGpuPending();
        }
        catch (Exception ex)
        {
            _inFlight.Remove((cx, cz));
            Console.WriteLine($"[WorldService] GPU pipeline error ({cx},{cz}): {ex.Message}");
            DispatchGpuPending();
        }
    }

    /// <summary>
    /// Called per frame. Dequeues fully-meshed chunks and adds them to the active chunks dictionary.
    /// </summary>
    public int ProcessReadyChunks(int maxCount = 2)
    {
        int processed = 0;
        while (processed < maxCount && _readyQueue.Count > 0)
        {
            var (cx, cz, mesh) = _readyQueue.Dequeue();
            if (_chunks.ContainsKey((cx, cz)))
            {
                // Duplicate - dispose the new buffer
                mesh.Dispose();
                continue;
            }
            _chunks[(cx, cz)] = mesh;
            processed++;
        }
        return processed;
    }

    /// <summary>Async initial load using GPU pipeline.</summary>
    public async Task<int> GenerateChunksAsync(Vector3 playerPos, int drawDistance)
    {
        _lastCX = (int)MathF.Floor(playerPos.X / ChunkData.SizeXZ);
        _lastCZ = (int)MathF.Floor(playerPos.Z / ChunkData.SizeXZ);

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

        int count = 0;
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

                var meshResult = await _engine.GenerateMeshAsync(chunk.Blocks);

                if (meshResult.HasMesh)
                {
                    var gpuBuffer = meshResult.QuadBuffer!.GetGPUBuffer();
                    _chunks[(cx, cz)] = new ChunkMesh
                    {
                        QuadBuffer = gpuBuffer,
                        QuadCount = meshResult.QuadCount,
                        IlgpuBuffer = meshResult.QuadBuffer,
                    };
                }
                else
                {
                    _chunks[(cx, cz)] = ChunkMesh.Empty;
                }
                count++;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[WorldService] Chunk ({cx},{cz}) error: {ex.Message}");
            }
        }
        return count;
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
        // Dispose all GPU buffers
        foreach (var mesh in _chunks.Values)
            mesh.Dispose();
        while (_readyQueue.Count > 0)
            _readyQueue.Dequeue().mesh.Dispose();

        _chunks.Clear();
        _pendingQueue.Clear();
        _inFlight.Clear();
        _lastCX = int.MinValue;
        _lastCZ = int.MinValue;
        _gpuReady = false;
        _generator = null;
        IsInitialized = false;
    }
}

/// <summary>
/// GPU-resident mesh data for a chunk. Holds the PackedQuad buffer
/// produced by VoxelEngine's greedy merge pipeline.
/// </summary>
public class ChunkMesh : IDisposable
{
    /// <summary>WebGPU buffer of PackedQuad data for VertexPullPipeline.</summary>
    public GPUBuffer? QuadBuffer { get; init; }

    /// <summary>Number of quads in the buffer.</summary>
    public int QuadCount { get; init; }

    /// <summary>ILGPU buffer reference (for disposal).</summary>
    public MemoryBuffer1D<long, Stride1D.Dense>? IlgpuBuffer { get; init; }

    /// <summary>True if this chunk has visible geometry.</summary>
    public bool HasMesh => QuadCount > 0 && QuadBuffer != null;

    /// <summary>Empty chunk (all air, no mesh data).</summary>
    public static ChunkMesh Empty { get; } = new() { QuadCount = 0 };

    public void Dispose()
    {
        IlgpuBuffer?.Dispose();
    }
}
