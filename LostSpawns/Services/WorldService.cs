using System.Numerics;
using ILGPU;
using ILGPU.Runtime;
using SpawnDev.BlazorJS.JSObjects;
using SpawnDev.ILGPU.WebGPU;
using SpawnDev.VoxelEngine;
using SpawnDev.VoxelEngine.Meshing;
using SpawnDev.VoxelEngine.Physics;
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
    // Sections keyed by (cx, sectionY, cz) - each 16x16x16 section is independently meshed and culled
    private readonly Dictionary<(int cx, int sy, int cz), ChunkMesh> _chunks = new();
    // Pending/in-flight track chunk columns (cx, cz) - one column produces up to 16 sections
    private readonly Queue<(int cx, int cz)> _pendingQueue = new();
    private readonly HashSet<(int cx, int cz)> _inFlight = new();
    private readonly Queue<(int cx, int sy, int cz, ChunkMesh mesh)> _readyQueue = new();
    // Track which columns are loaded (for chunk streaming logic)
    private readonly HashSet<(int cx, int cz)> _loadedColumns = new();
    // CPU-side block cache keyed by (cx, cz). Populated whenever a chunk's blocks are
    // generated - self load, neighbor-padding lookup, or otherwise. Lets the 4 neighbor
    // lookups per column hit already-computed blocks instead of regenerating them,
    // avoiding the ~5x CPU work amplification in the initial load path.
    private readonly Dictionary<(int cx, int cz), byte[]> _blocksCache = new();

    // Sparse edit log per column. Key = byte-array index within the column,
    // value = the new block byte. Only covers player modifications (break/place);
    // procedural terrain is recreated from the heightmap every time the column
    // gets generated. SaveService serializes this + load-side re-applies it and
    // re-meshes affected columns so a chopped tree stays chopped across sessions.
    private readonly Dictionary<(int cx, int cz), Dictionary<int, byte>> _edits = new();
    // Cap to keep memory bounded on long sessions. 64KB per entry; 512 = 32MB ceiling.
    // UpdateDesiredChunks trims to the draw-distance footprint + 1-ring on each eviction pass.
    private const int MaxCachedBlockColumns = 512;
    private TerrainGenerator? _generator;
    private HeightmapLoader? _heightmapLoader;
    private int _lastCX = int.MinValue;
    private int _lastCZ = int.MinValue;
    private const int MaxConcurrentGpu = 16;

    public int Seed { get; private set; }
    public bool IsInitialized { get; private set; }
    public int LoadedSections => _chunks.Count;
    public int LoadedColumns => _loadedColumns.Count;
    public bool HasPendingChunks => _pendingQueue.Count > 0 || _inFlight.Count > 0 || _readyQueue.Count > 0;
    public int PendingCount => _pendingQueue.Count + _inFlight.Count + _readyQueue.Count;

    /// <summary>The heightmap loader, if a real-world map is loaded.</summary>
    public HeightmapLoader? HeightmapLoader => _heightmapLoader;

    /// <summary>All loaded sections with their GPU mesh data. Used by RenderService for drawing.</summary>
    public IReadOnlyDictionary<(int cx, int sy, int cz), ChunkMesh> Sections => _chunks;

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
            Console.WriteLine("[World] GPU meshing enabled");
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

        Console.WriteLine($"[World] Heightmap: {loader.GridSize}x{loader.GridSize}, {loader.MapSizeInChunks} chunks");
        IsInitialized = true;
    }

    public List<(int cx, int cz)> UpdateDesiredChunks(Vector3 playerPos, int drawDistance)
    {
        int pcx = (int)MathF.Floor(playerPos.X / ChunkData.SizeXZ);
        int pcz = (int)MathF.Floor(playerPos.Z / ChunkData.SizeXZ);

        if (pcx == _lastCX && pcz == _lastCZ && (_loadedColumns.Count > 0 || _pendingQueue.Count > 0 || _inFlight.Count > 0))
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
        foreach (var col in _loadedColumns.ToList())
        {
            if (!desired.Contains(col))
            {
                // Dispose all GPU buffers for sections in this column
                for (int sy = 0; sy < 16; sy++)
                {
                    var key = (col.Item1, sy, col.Item2);
                    if (_chunks.TryGetValue(key, out var mesh))
                    {
                        mesh.Dispose();
                        _chunks.Remove(key);
                    }
                }
                _loadedColumns.Remove(col);
                removed.Add(col);
            }
        }

        _pendingQueue.Clear();
        var toAdd = desired
            .Where(k => !_loadedColumns.Contains(k) && !_inFlight.Contains(k))
            .OrderBy(k => (k.Item1 - pcx) * (k.Item1 - pcx) + (k.Item2 - pcz) * (k.Item2 - pcz));

        foreach (var key in toAdd)
            _pendingQueue.Enqueue(key);

        // Trim the blocks cache to cells that could still be referenced as a neighbor by
        // any desired column (i.e. desired + 1-ring). Prevents the cache from growing
        // forever as the player walks across the map. The desired footprint is bounded by
        // drawDistance, so trimmed cache size is O((drawDistance+1)^2) chunks.
        TrimBlocksCache(desired);

        DispatchGpuPending();
        return removed;
    }

    private void TrimBlocksCache(HashSet<(int, int)> desired)
    {
        if (_blocksCache.Count <= MaxCachedBlockColumns && _blocksCache.Count < desired.Count * 2)
            return;

        // Keep cells that are either in desired, or one step away from any desired cell
        // (since those are read as neighbor-padding during meshing of the nearest desired cell).
        var keep = new HashSet<(int, int)>(desired);
        foreach (var (dx, dz) in desired)
        {
            keep.Add((dx - 1, dz));
            keep.Add((dx + 1, dz));
            keep.Add((dx, dz - 1));
            keep.Add((dx, dz + 1));
        }

        var toEvict = new List<(int, int)>();
        foreach (var key in _blocksCache.Keys)
            if (!keep.Contains(key))
                toEvict.Add(key);

        foreach (var key in toEvict)
            _blocksCache.Remove(key);
    }

    private void DispatchGpuPending()
    {
        while (_inFlight.Count < MaxConcurrentGpu && _pendingQueue.Count > 0)
        {
            var key = _pendingQueue.Dequeue();
            if (_loadedColumns.Contains(key)) continue;
            _inFlight.Add(key);
            _ = GenerateChunkGpuAsync(key.Item1, key.Item2);
        }
    }

    /// <summary>
    /// Full GPU pipeline: heightmap -> fill blocks -> split into 16x16x16 sections -> greedy mesh each.
    /// Mesh data stays GPU-resident (no CPU readback).
    /// Also regenerates the 4 XZ neighbor chunks so the mesher can pad section borders with
    /// real neighbor block data, preventing see-through faces at chunk boundaries.
    /// </summary>
    private async Task GenerateChunkGpuAsync(int cx, int cz)
    {
        try
        {
            var blocks = await GetOrGenerateBlocksAsync(cx, cz);
            if (blocks == null) { _inFlight.Remove((cx, cz)); return; }

            if (_loadedColumns.Contains((cx, cz))) { _inFlight.Remove((cx, cz)); return; }

            // XZ neighbors for boundary padding. Cached across calls so a neighbor
            // generated once gets reused when adjacent columns mesh.
            var nxMinus = await GetOrGenerateBlocksAsync(cx - 1, cz);
            var nxPlus = await GetOrGenerateBlocksAsync(cx + 1, cz);
            var nzMinus = await GetOrGenerateBlocksAsync(cx, cz - 1);
            var nzPlus = await GetOrGenerateBlocksAsync(cx, cz + 1);

            // VoxelEngine greedy mesh: split chunk into 16x16x16 sections, mesh each with neighbor padding
            var sectionMeshes = await _engine.GenerateChunkMeshesAsync(
                blocks, nxMinus, nxPlus, nzMinus, nzPlus);

            _inFlight.Remove((cx, cz));
            if (_loadedColumns.Contains((cx, cz)))
            {
                foreach (var (_, m) in sectionMeshes)
                    m.QuadBuffer?.Dispose();
                return;
            }

            _loadedColumns.Add((cx, cz));

            foreach (var (sy, meshResult) in sectionMeshes)
            {
                var gpuBuffer = meshResult.QuadBuffer!.GetGPUBuffer();
                var sectionMesh = new ChunkMesh
                {
                    QuadBuffer = gpuBuffer,
                    QuadCount = meshResult.QuadCount,
                    IlgpuBuffer = meshResult.QuadBuffer,
                };
                _readyQueue.Enqueue((cx, sy, cz, sectionMesh));
            }

            DispatchGpuPending();
        }
        catch (Exception ex)
        {
            _inFlight.Remove((cx, cz));
            Console.WriteLine($"[World] Chunk ({cx},{cz}) error: {ex.Message}");
            DispatchGpuPending();
        }
    }

    /// <summary>
    /// Called per frame. Dequeues fully-meshed chunks and adds them to the active chunks dictionary.
    /// </summary>
    public int ProcessReadyChunks(int maxCount = 16)
    {
        int processed = 0;
        while (processed < maxCount && _readyQueue.Count > 0)
        {
            var (cx, sy, cz, mesh) = _readyQueue.Dequeue();
            var key = (cx, sy, cz);
            if (_chunks.ContainsKey(key))
            {
                mesh.Dispose();
                continue;
            }
            _chunks[key] = mesh;
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
            if (_loadedColumns.Contains((cx, cz))) continue;
            toGenerate.Add((cx, cz));
        }

        int columnCount = 0;
        foreach (var (cx, cz) in toGenerate)
        {
            try
            {
                var blocks = await GetOrGenerateBlocksAsync(cx, cz);
                if (blocks == null) continue;

                var nxMinus = await GetOrGenerateBlocksAsync(cx - 1, cz);
                var nxPlus = await GetOrGenerateBlocksAsync(cx + 1, cz);
                var nzMinus = await GetOrGenerateBlocksAsync(cx, cz - 1);
                var nzPlus = await GetOrGenerateBlocksAsync(cx, cz + 1);

                var sectionMeshes = await _engine.GenerateChunkMeshesAsync(
                    blocks, nxMinus, nxPlus, nzMinus, nzPlus);

                _loadedColumns.Add((cx, cz));
                foreach (var (sy, meshResult) in sectionMeshes)
                {
                    var gpuBuffer = meshResult.QuadBuffer!.GetGPUBuffer();
                    _chunks[(cx, sy, cz)] = new ChunkMesh
                    {
                        QuadBuffer = gpuBuffer,
                        QuadCount = meshResult.QuadCount,
                        IlgpuBuffer = meshResult.QuadBuffer,
                    };
                }
                columnCount++;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[World] Chunk ({cx},{cz}) error: {ex.Message}");
            }
        }
        return columnCount;
    }

    /// <summary>
    /// Return the full block byte[] for a chunk column, generating it on demand if not cached.
    /// Hit path is a single Dictionary lookup, miss path runs the same generation as before
    /// (HeightmapLoader for real maps, TerrainGenerator.GenerateChunkFromHeightmap for procedural)
    /// and populates the cache. Returns null if the coordinate is outside the loaded heightmap.
    /// </summary>
    private async Task<byte[]?> GetOrGenerateBlocksAsync(int ncx, int ncz)
    {
        if (_blocksCache.TryGetValue((ncx, ncz), out var cached))
            return cached;

        try
        {
            ChunkData neighbor;
            if (_heightmapLoader != null)
            {
                neighbor = _heightmapLoader.GenerateChunk(ncx, ncz);
            }
            else
            {
                var hm = await _engine.GenerateHeightmapAsync(ncx, ncz);
                neighbor = _generator!.GenerateChunkFromHeightmap(ncx, ncz, hm);
            }

            // If the player has edited this column before (from a prior save or an
            // earlier in-session visit after unload), overlay those edits onto the
            // freshly-generated pristine blocks. Keeps broken trees broken and
            // placed walls present the moment the column re-enters the cache.
            var blocks = neighbor.Blocks;
            if (_edits.TryGetValue((ncx, ncz), out var chunkEdits))
            {
                foreach (var (idx, val) in chunkEdits)
                    if ((uint)idx < (uint)blocks.Length)
                        blocks[idx] = val;
            }

            _blocksCache[(ncx, ncz)] = blocks;
            return blocks;
        }
        catch
        {
            return null;
        }
    }

    public int GetHeightAt(float worldX, float worldZ)
    {
        if (_heightmapLoader != null)
            return _heightmapLoader.GetElevation(worldX, worldZ);
        return _generator?.GetHeight(worldX, worldZ) ?? 30;
    }

    /// <summary>
    /// Read the BlockType at a world-space integer voxel position. Returns Air
    /// for out-of-bounds or unloaded chunks (so the player can traverse terrain
    /// beyond the cached footprint without being blocked by nothing).
    /// </summary>
    public BlockType GetBlockAt(int worldX, int worldY, int worldZ)
    {
        if (worldY < 0 || worldY >= ChunkData.Height) return BlockType.Air;

        int cx = (int)MathF.Floor(worldX / (float)ChunkData.SizeXZ);
        int cz = (int)MathF.Floor(worldZ / (float)ChunkData.SizeXZ);
        if (!_blocksCache.TryGetValue((cx, cz), out var col)) return BlockType.Air;

        int lx = worldX - cx * ChunkData.SizeXZ;
        int lz = worldZ - cz * ChunkData.SizeXZ;
        int idx = lx + lz * ChunkData.SizeXZ + worldY * ChunkData.SizeXZ * ChunkData.SizeXZ;
        return (BlockType)col[idx];
    }

    /// <summary>True if the block at the given world voxel is non-air and non-water.</summary>
    public bool IsSolidAt(int worldX, int worldY, int worldZ)
    {
        var t = GetBlockAt(worldX, worldY, worldZ);
        return t != BlockType.Air && t != BlockType.Water;
    }

    private static readonly VoxelEngineConfig _raycastConfig = new()
    {
        VoxelSize = 1.0f,
        SectionSize = 16,
        BaseY = 0f,
    };

    /// <summary>
    /// Cast a ray through the loaded world and return the first solid block hit, or
    /// RaycastHit.None if the ray exits the loaded area without hitting anything.
    /// Thin wrapper over SpawnDev.VoxelEngine.Physics.VoxelRaycast.CastWorld that
    /// adapts our byte[] column cache to the library's int[] PackedBlock section view.
    /// Water blocks are treated as transparent for interaction.
    /// </summary>
    public RaycastHit Raycast(Vector3 origin, Vector3 dir, float maxDistance)
    {
        return VoxelRaycast.CastWorld(
            GetSectionBlocksForRaycast,
            _raycastConfig,
            origin,
            Vector3.Normalize(dir),
            maxDistance,
            packed =>
            {
                // Stop on everything except water (so rays pass through water).
                var type = (BlockType)PackedBlock.GetType(packed);
                return type != BlockType.Water;
            });
    }

    /// <summary>Adapter: flat byte[] column cache -> int[] 16x16x16 section in PackedBlock format.</summary>
    private int[]? GetSectionBlocksForRaycast(SectionCoord coord)
    {
        const int ss = 16;
        if (!_blocksCache.TryGetValue((coord.Cx, coord.Cz), out var col))
            return null;
        if (coord.Sy < 0 || (coord.Sy + 1) * ss > ChunkData.Height)
            return null;

        var section = new int[ss * ss * ss];
        int yStart = coord.Sy * ss;
        for (int y = 0; y < ss; y++)
        {
            int srcYBase = (yStart + y) * ChunkData.SizeXZ * ChunkData.SizeXZ;
            int dstYBase = y * ss * ss;
            for (int z = 0; z < ss; z++)
                for (int x = 0; x < ss; x++)
                {
                    byte b = col[x + z * ChunkData.SizeXZ + srcYBase];
                    // byte -> PackedBlock int (lower 12 bits = block type)
                    section[x + z * ss + dstYBase] = b;
                }
        }
        return section;
    }

    /// <summary>
    /// Place the given block type at the world-space integer voxel position.
    /// Returns true on success, false if the target is out of bounds, in an unloaded
    /// chunk, or already occupied by a non-air block (no overwriting existing geometry).
    /// Re-meshes the affected column + XZ neighbors if the block sits on a boundary.
    /// </summary>
    public bool TryPlaceBlock(int worldX, int worldY, int worldZ, BlockType type)
    {
        if (type == BlockType.Air) return false;
        if (worldY < 0 || worldY >= ChunkData.Height) return false;

        int cx = (int)MathF.Floor(worldX / (float)ChunkData.SizeXZ);
        int cz = (int)MathF.Floor(worldZ / (float)ChunkData.SizeXZ);
        if (!_blocksCache.TryGetValue((cx, cz), out var col)) return false;

        int lx = worldX - cx * ChunkData.SizeXZ;
        int lz = worldZ - cz * ChunkData.SizeXZ;
        int idx = lx + lz * ChunkData.SizeXZ + worldY * ChunkData.SizeXZ * ChunkData.SizeXZ;

        if (col[idx] != 0) return false;

        col[idx] = (byte)type;
        RecordEdit(cx, cz, idx, (byte)type);

        _ = ReMeshColumnAsync(cx, cz);
        if (lx == 0) _ = ReMeshColumnAsync(cx - 1, cz);
        if (lx == ChunkData.SizeXZ - 1) _ = ReMeshColumnAsync(cx + 1, cz);
        if (lz == 0) _ = ReMeshColumnAsync(cx, cz - 1);
        if (lz == ChunkData.SizeXZ - 1) _ = ReMeshColumnAsync(cx, cz + 1);

        return true;
    }

    /// <summary>
    /// Break the block at the given world-space integer voxel position. Zeroes the
    /// byte in the column cache, fires off a re-mesh of the affected column (and any
    /// XZ-neighbor column if the block sat on the boundary), and returns the
    /// original BlockType so the caller can decide what item to drop.
    /// Returns BlockType.Air if the target position is out of bounds, in an unloaded
    /// chunk, or was already air.
    /// </summary>
    public BlockType TryBreakBlock(int worldX, int worldY, int worldZ)
    {
        if (worldY < 0 || worldY >= ChunkData.Height) return BlockType.Air;

        int cx = (int)MathF.Floor(worldX / (float)ChunkData.SizeXZ);
        int cz = (int)MathF.Floor(worldZ / (float)ChunkData.SizeXZ);
        if (!_blocksCache.TryGetValue((cx, cz), out var col)) return BlockType.Air;

        int lx = worldX - cx * ChunkData.SizeXZ;
        int lz = worldZ - cz * ChunkData.SizeXZ;
        int idx = lx + lz * ChunkData.SizeXZ + worldY * ChunkData.SizeXZ * ChunkData.SizeXZ;

        byte original = col[idx];
        if (original == 0) return BlockType.Air;

        col[idx] = 0;
        RecordEdit(cx, cz, idx, 0);

        // Re-mesh this column + any edge-neighbor columns that share the boundary.
        _ = ReMeshColumnAsync(cx, cz);
        if (lx == 0) _ = ReMeshColumnAsync(cx - 1, cz);
        if (lx == ChunkData.SizeXZ - 1) _ = ReMeshColumnAsync(cx + 1, cz);
        if (lz == 0) _ = ReMeshColumnAsync(cx, cz - 1);
        if (lz == ChunkData.SizeXZ - 1) _ = ReMeshColumnAsync(cx, cz + 1);

        return (BlockType)original;
    }

    private void RecordEdit(int cx, int cz, int idx, byte newByte)
    {
        if (!_edits.TryGetValue((cx, cz), out var chunkEdits))
        {
            chunkEdits = new Dictionary<int, byte>();
            _edits[(cx, cz)] = chunkEdits;
        }
        chunkEdits[idx] = newByte;
    }

    /// <summary>
    /// Snapshot every column's edits as a flat list suitable for JSON. Keys are
    /// a packed "cx,cz" so save files stay compact and schema-stable.
    /// </summary>
    public Dictionary<string, Dictionary<int, byte>> GetEditsSnapshot()
    {
        var copy = new Dictionary<string, Dictionary<int, byte>>(_edits.Count);
        foreach (var ((cx, cz), map) in _edits)
            copy[$"{cx},{cz}"] = new Dictionary<int, byte>(map);
        return copy;
    }

    /// <summary>
    /// Apply an edit snapshot on top of loaded chunks. Any column we have cached
    /// gets its byte array patched in place. Columns not yet loaded queue their
    /// edits for when generation later populates the cache (rare once the
    /// initial radius is up, but keeps correctness around draw-distance edges).
    /// Returns the list of columns whose mesh should be rebuilt.
    /// </summary>
    public IEnumerable<(int cx, int cz)> ApplyEdits(IReadOnlyDictionary<string, Dictionary<int, byte>> edits)
    {
        var touched = new HashSet<(int, int)>();
        foreach (var (key, map) in edits)
        {
            if (!TryParseChunkKey(key, out int cx, out int cz)) continue;
            if (!_edits.TryGetValue((cx, cz), out var chunkEdits))
            {
                chunkEdits = new Dictionary<int, byte>();
                _edits[(cx, cz)] = chunkEdits;
            }
            foreach (var (idx, val) in map)
                chunkEdits[idx] = val;

            if (_blocksCache.TryGetValue((cx, cz), out var col))
            {
                foreach (var (idx, val) in map)
                    if ((uint)idx < (uint)col.Length)
                        col[idx] = val;
                touched.Add((cx, cz));
            }
        }
        return touched;
    }

    /// <summary>Public hook for Game.razor to re-mesh a column after applying saved edits.</summary>
    public Task ReMeshColumn(int cx, int cz) => ReMeshColumnAsync(cx, cz);

    private static bool TryParseChunkKey(string key, out int cx, out int cz)
    {
        cx = 0; cz = 0;
        int comma = key.IndexOf(',');
        if (comma <= 0) return false;
        return int.TryParse(key.AsSpan(0, comma), out cx) &&
               int.TryParse(key.AsSpan(comma + 1), out cz);
    }

    private async Task ReMeshColumnAsync(int cx, int cz)
    {
        if (!_blocksCache.TryGetValue((cx, cz), out var blocks)) return;
        var nxMinus = _blocksCache.TryGetValue((cx - 1, cz), out var a) ? a : null;
        var nxPlus  = _blocksCache.TryGetValue((cx + 1, cz), out var b) ? b : null;
        var nzMinus = _blocksCache.TryGetValue((cx, cz - 1), out var c) ? c : null;
        var nzPlus  = _blocksCache.TryGetValue((cx, cz + 1), out var d) ? d : null;

        List<(int sectionY, VoxelMeshPipeline.MeshResult mesh)> sectionMeshes;
        try
        {
            sectionMeshes = await _engine.GenerateChunkMeshesAsync(
                blocks, nxMinus, nxPlus, nzMinus, nzPlus);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[World] Re-mesh ({cx},{cz}) failed: {ex.Message}");
            return;
        }

        // Swap the column's sections with the fresh meshes. Dispose any old sections
        // the new pass didn't produce (they're now all-air after the break).
        var freshSyIndices = new HashSet<int>(sectionMeshes.Select(s => s.sectionY));
        for (int sy = 0; sy < 16; sy++)
        {
            var key = (cx, sy, cz);
            if (_chunks.TryGetValue(key, out var old) && !freshSyIndices.Contains(sy))
            {
                old.Dispose();
                _chunks.Remove(key);
            }
        }
        foreach (var (sy, meshResult) in sectionMeshes)
        {
            var key = (cx, sy, cz);
            if (_chunks.TryGetValue(key, out var old)) old.Dispose();

            var gpuBuffer = meshResult.QuadBuffer!.GetGPUBuffer();
            _chunks[key] = new ChunkMesh
            {
                QuadBuffer = gpuBuffer,
                QuadCount = meshResult.QuadCount,
                IlgpuBuffer = meshResult.QuadBuffer,
            };
        }
    }

    /// <summary>Resets all state so the service can be re-initialized.</summary>
    public void Reset()
    {
        foreach (var mesh in _chunks.Values)
            mesh.Dispose();
        while (_readyQueue.Count > 0)
            _readyQueue.Dequeue().mesh.Dispose();

        _chunks.Clear();
        _loadedColumns.Clear();
        _pendingQueue.Clear();
        _inFlight.Clear();
        _blocksCache.Clear();
        _lastCX = int.MinValue;
        _lastCZ = int.MinValue;
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
