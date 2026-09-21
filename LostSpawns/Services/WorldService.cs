using System.Numerics;
using ILGPU;
using ILGPU.Runtime;
using SpawnDev.SpawnJS.JSObjects;
using SpawnDev.ILGPU.WebGPU;
using SpawnDev.VoxelEngine;
using SpawnDev.VoxelEngine.Destruction;
using SpawnDev.VoxelEngine.Meshing;
using SpawnDev.VoxelEngine.Physics;
using LostSpawns.Models;
using LostSpawns.Rendering;

namespace LostSpawns.Services;

/// <summary>
/// Manages the voxel world: chunk loading/unloading around the player.
/// Full GPU pipeline: heightmap -> block fill -> VoxelEngine greedy mesh -> GPU-resident quads.
/// No CPU readback - mesh data stays on GPU from generation to rendering.
/// Dig/terraform: edits mark dirty sections; ProcessDirtyRemesh drains one coalesced pass per frame
/// (pauses stream mesh while dirty) so paint strokes do not stampede full-column remeshes.
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

    // Dig remesh coalesce: section keys dirty since last ProcessDirtyRemesh.
    // Do NOT fire-and-forget ReMeshColumnAsync per SphereOp - that stampeded _meshLock.
    private readonly HashSet<(int cx, int sy, int cz)> _dirtySections = new();
    private bool _dirtyRemeshRunning;
    // Reused raycast section buffer - CastWorld consumes each getSection result before
    // the next call, so one int[4096] is safe and avoids WASM GC on terraform preview.
    private readonly int[] _raycastSectionBuf = new int[16 * 16 * 16];

    // Dig stage timings (last completed remesh pass) - F3 / console.
    public double LastCarveMs { get; private set; }
    public double LastRemeshMs { get; private set; }
    public int LastDirtySectionCount { get; private set; }
    public int LastSectionsMeshed { get; private set; }
    public int DirtyRemeshPending => _dirtySections.Count;
    public double LastMeshLockWaitMs => _engine.LastMeshLockWaitMs;

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
    // Keep this small: all mesh work shares one _meshLock. A large in-flight count
    // queues many WaitAsync callers FIFO, so dig remesh sat behind ~16 full-column
    // meshes (~5s pickaxe lag). Dig BeginDigRemesh + stream yield handles new work;
    // this cap limits how deep the FIFO already is when dig arrives.
    private const int MaxConcurrentGpu = 2;

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
        // Dig remesh wins: do not start new stream columns while dirty sections pending.
        if (_dirtySections.Count > 0 || _dirtyRemeshRunning)
            return;

        while (_inFlight.Count < MaxConcurrentGpu && _pendingQueue.Count > 0)
        {
            var key = _pendingQueue.Dequeue();
            if (_loadedColumns.Contains(key)) continue;
            _inFlight.Add(key);
            _ = GenerateChunkGpuAsync(key.Item1, key.Item2);
        }
    }

    /// <summary>
    /// Coalesce dirty sections into one remesh pass. Grouped by column; each column
    /// remeshes only its dirty section-Y indices via MeshChunkColumnSectionsAsync.
    /// </summary>
    public void ProcessDirtyRemesh()
    {
        if (_dirtyRemeshRunning || _dirtySections.Count == 0) return;
        _dirtyRemeshRunning = true;
        var snapshot = _dirtySections.ToList();
        _dirtySections.Clear();
        LastDirtySectionCount = snapshot.Count;
        _ = DrainDirtyRemeshAsync(snapshot);
    }

    private async Task DrainDirtyRemeshAsync(List<(int cx, int sy, int cz)> snapshot)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        int meshed = 0;
        _engine.BeginDigRemesh();
        try
        {
            var byColumn = new Dictionary<(int cx, int cz), List<int>>();
            foreach (var (cx, sy, cz) in snapshot)
            {
                if (sy < 0 || sy >= 16) continue;
                var col = (cx, cz);
                if (!byColumn.TryGetValue(col, out var list))
                {
                    list = new List<int>();
                    byColumn[col] = list;
                }
                if (!list.Contains(sy))
                    list.Add(sy);
            }

            foreach (var ((cx, cz), sectionYs) in byColumn)
                meshed += await RemeshSectionsAsync(cx, cz, sectionYs);
        }
        finally
        {
            _engine.EndDigRemesh();
            sw.Stop();
            LastRemeshMs = sw.Elapsed.TotalMilliseconds;
            LastSectionsMeshed = meshed;
            _dirtyRemeshRunning = false;
            if (_dirtySections.Count == 0)
                DispatchGpuPending();
        }
    }

    private void MarkSectionsDirty(IEnumerable<SectionCoord> coords)
    {
        foreach (var c in coords)
        {
            if (c.Sy < 0 || c.Sy >= 16) continue;
            _dirtySections.Add((c.Cx, c.Sy, c.Cz));
        }
    }

    private void MarkBlockDirty(int worldX, int worldY, int worldZ)
    {
        const int ss = ChunkData.SizeXZ;
        int cx = (int)MathF.Floor(worldX / (float)ss);
        int cz = (int)MathF.Floor(worldZ / (float)ss);
        int sy = worldY / ss;
        int lx = worldX - cx * ss;
        int lz = worldZ - cz * ss;

        _dirtySections.Add((cx, sy, cz));
        if (worldY % ss == 0 && sy > 0) _dirtySections.Add((cx, sy - 1, cz));
        if (worldY % ss == ss - 1 && sy < 15) _dirtySections.Add((cx, sy + 1, cz));
        if (lx == 0) _dirtySections.Add((cx - 1, sy, cz));
        if (lx == ss - 1) _dirtySections.Add((cx + 1, sy, cz));
        if (lz == 0) _dirtySections.Add((cx, sy, cz - 1));
        if (lz == ss - 1) _dirtySections.Add((cx, sy, cz + 1));

        // Kick remesh now (don't wait for next frame's ProcessReadyChunks) so pickaxe
        // dirt updates in the same interaction, once stream tasks yield the mesh lock.
        ProcessDirtyRemesh();
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

            // Dig remesh must win the mesh lock. In-flight stream tasks that already
            // passed DispatchGpuPending used to hold _meshLock for seconds (full-column
            // mesh ×16) and made pickaxe dirt lag ~5s. Yield here until dig drain finishes.
            while (_dirtySections.Count > 0 || _dirtyRemeshRunning)
                await Task.Yield();

            if (_loadedColumns.Contains((cx, cz))) { _inFlight.Remove((cx, cz)); return; }

            // XZ neighbors for boundary padding. Cached across calls so a neighbor
            // generated once gets reused when adjacent columns mesh.
            var nxMinus = await GetOrGenerateBlocksAsync(cx - 1, cz);
            var nxPlus = await GetOrGenerateBlocksAsync(cx + 1, cz);
            var nzMinus = await GetOrGenerateBlocksAsync(cx, cz - 1);
            var nzPlus = await GetOrGenerateBlocksAsync(cx, cz + 1);

            // Re-check dig priority after neighbor gen (can take a while).
            while (_dirtySections.Count > 0 || _dirtyRemeshRunning)
                await Task.Yield();

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
    /// Called per frame. Drains dirty dig remesh first, then dequeues streamed sections.
    /// </summary>
    public int ProcessReadyChunks(int maxCount = 16)
    {
        ProcessDirtyRemesh();

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

    /// <summary>Adapter: flat byte[] column cache -> int[] 16x16x16 section in PackedBlock format.
    /// Reuses one buffer - CastWorld reads each section before requesting the next.</summary>
    private int[]? GetSectionBlocksForRaycast(SectionCoord coord)
    {
        const int ss = 16;
        if (!_blocksCache.TryGetValue((coord.Cx, coord.Cz), out var col))
            return null;
        if (coord.Sy < 0 || (coord.Sy + 1) * ss > ChunkData.Height)
            return null;

        var section = _raycastSectionBuf;
        System.Array.Clear(section, 0, section.Length);
        int yStart = coord.Sy * ss;
        for (int y = 0; y < ss; y++)
        {
            int srcYBase = (yStart + y) * ChunkData.SizeXZ * ChunkData.SizeXZ;
            int dstYBase = y * ss * ss;
            for (int z = 0; z < ss; z++)
                for (int x = 0; x < ss; x++)
                {
                    byte b = col[x + z * ChunkData.SizeXZ + srcYBase];
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
        MarkBlockDirty(worldX, worldY, worldZ);
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
        MarkBlockDirty(worldX, worldY, worldZ);
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
    /// Carve out a sphere of voxels centered at world-space `center` with the
    /// given `radius`. Cells inside the sphere become Air. Returns a dictionary
    /// of removed-cell counts keyed by the original BlockType, so the caller
    /// can roll up loot drops or just count total cells removed.
    ///
    /// All affected columns are re-meshed exactly once at the end (batching
    /// the GPU mesh kernel work) instead of once per cell - critical for a
    /// reasonable-radius sphere that touches dozens of cells.
    /// </summary>
    public Dictionary<BlockType, int> CarveSphere(Vector3 center, float radius)
    {
        return SphereOp(center, radius, replaceWith: 0, allowOverwrite: true);
    }

    /// <summary>
    /// Fill a sphere of voxels centered at world-space `center` with the given
    /// block type. Cells already non-air are skipped (no overwriting), matching
    /// TryPlaceBlock's contract. Returns counts of cells written keyed by the
    /// type written (always one entry today, but mirrors CarveSphere's API).
    /// </summary>
    public Dictionary<BlockType, int> BuildSphere(Vector3 center, float radius, BlockType type)
    {
        if (type == BlockType.Air) return new Dictionary<BlockType, int>();
        return SphereOp(center, radius, replaceWith: (byte)type, allowOverwrite: false);
    }

    private Dictionary<BlockType, int> SphereOp(Vector3 center, float radius, byte replaceWith, bool allowOverwrite)
    {
        var changed = new Dictionary<BlockType, int>();
        if (radius <= 0) return changed;

        var carveSw = System.Diagnostics.Stopwatch.StartNew();

        int minY = Math.Max(0, (int)MathF.Floor(center.Y - radius));
        int maxY = Math.Min(ChunkData.Height - 1, (int)MathF.Floor(center.Y + radius));
        float r2 = radius * radius;

        int minX = (int)MathF.Floor(center.X - radius);
        int maxX = (int)MathF.Floor(center.X + radius);
        int minZ = (int)MathF.Floor(center.Z - radius);
        int maxZ = (int)MathF.Floor(center.Z + radius);

        var touchedColumns = new HashSet<(int cx, int cz)>();
        for (int wx = minX; wx <= maxX; wx++)
        for (int wz = minZ; wz <= maxZ; wz++)
            touchedColumns.Add((
                (int)MathF.Floor(wx / (float)ChunkData.SizeXZ),
                (int)MathF.Floor(wz / (float)ChunkData.SizeXZ)));

        // Sync carve must stay on CPU - GetAwaiter().GetResult() on GPU deadlocks Blazor WASM.
        // BlockColumnCarveService is available for async blast paths; brush uses library CPU.
        foreach (var (cx, cz) in touchedColumns)
        {
            if (!_blocksCache.TryGetValue((cx, cz), out var col)) continue;
            float localCx = center.X - cx * ChunkData.SizeXZ;
            float localCz = center.Z - cz * ChunkData.SizeXZ;

            // Snapshot candidates for tallies before mutating.
            var pre = new List<(int idx, byte original)>(64);
            int cMinX = Math.Max(0, (int)MathF.Floor(localCx - radius));
            int cMaxX = Math.Min(ChunkData.SizeXZ - 1, (int)MathF.Floor(localCx + radius));
            int cMinZ = Math.Max(0, (int)MathF.Floor(localCz - radius));
            int cMaxZ = Math.Min(ChunkData.SizeXZ - 1, (int)MathF.Floor(localCz + radius));
            for (int lx = cMinX; lx <= cMaxX; lx++)
            for (int wy = minY; wy <= maxY; wy++)
            for (int lz = cMinZ; lz <= cMaxZ; lz++)
            {
                float dx = (lx + 0.5f) - localCx;
                float dy = (wy + 0.5f) - center.Y;
                float dz = (lz + 0.5f) - localCz;
                if (dx * dx + dy * dy + dz * dz > r2) continue;
                int idx = lx + lz * ChunkData.SizeXZ + wy * ChunkData.SizeXZ * ChunkData.SizeXZ;
                byte original = col[idx];
                if (allowOverwrite) { if (original == 0) continue; }
                else { if (original != 0) continue; }
                pre.Add((idx, original));
            }

            if (pre.Count == 0) continue;

            if (allowOverwrite)
                ExplosionKernels.DestroyInSphereBytes(col, ChunkData.SizeXZ, ChunkData.Height,
                    localCx, center.Y, localCz, radius);
            else
                ExplosionKernels.FillInSphereBytes(col, ChunkData.SizeXZ, ChunkData.Height,
                    localCx, center.Y, localCz, radius, replaceWith);

            foreach (var (idx, original) in pre)
            {
                byte now = col[idx];
                if (allowOverwrite)
                {
                    if (now != 0) continue;
                    RecordEdit(cx, cz, idx, 0);
                    changed[(BlockType)original] = changed.GetValueOrDefault((BlockType)original) + 1;
                }
                else
                {
                    if (now != replaceWith) continue;
                    RecordEdit(cx, cz, idx, replaceWith);
                    changed[(BlockType)replaceWith] = changed.GetValueOrDefault((BlockType)replaceWith) + 1;
                }
            }
        }

        var affected = new HashSet<SectionCoord>();
        ExplosionKernels.CollectAffectedSections(
            center.X, center.Y, center.Z, radius, ChunkData.SizeXZ, affected);
        foreach (var s in affected.ToList())
        {
            affected.Add(new SectionCoord(s.Cx - 1, s.Sy, s.Cz));
            affected.Add(new SectionCoord(s.Cx + 1, s.Sy, s.Cz));
            affected.Add(new SectionCoord(s.Cx, s.Sy, s.Cz - 1));
            affected.Add(new SectionCoord(s.Cx, s.Sy, s.Cz + 1));
            if (s.Sy > 0) affected.Add(new SectionCoord(s.Cx, s.Sy - 1, s.Cz));
            if (s.Sy < 15) affected.Add(new SectionCoord(s.Cx, s.Sy + 1, s.Cz));
        }
        MarkSectionsDirty(affected);

        carveSw.Stop();
        LastCarveMs = carveSw.Elapsed.TotalMilliseconds;
        ProcessDirtyRemesh();
        return changed;
    }

    /// <summary>
    /// Async GPU sphere carve for large blasts. Prefer this over sync SphereOp when
    /// volume is huge; brush path stays on CPU DestroyInSphereBytes to avoid WASM deadlock.
    /// </summary>
    public async Task<int> CarveSphereGpuAsync(Vector3 center, float radius)
    {
        if (_engine.Carve == null || radius <= 0) return 0;
        int total = 0;
        int minX = (int)MathF.Floor(center.X - radius);
        int maxX = (int)MathF.Floor(center.X + radius);
        int minZ = (int)MathF.Floor(center.Z - radius);
        int maxZ = (int)MathF.Floor(center.Z + radius);
        var cols = new HashSet<(int, int)>();
        for (int wx = minX; wx <= maxX; wx++)
        for (int wz = minZ; wz <= maxZ; wz++)
            cols.Add(((int)MathF.Floor(wx / (float)ChunkData.SizeXZ),
                      (int)MathF.Floor(wz / (float)ChunkData.SizeXZ)));

        foreach (var (cx, cz) in cols)
        {
            if (!_blocksCache.TryGetValue((cx, cz), out var col)) continue;
            float localCx = center.X - cx * ChunkData.SizeXZ;
            float localCz = center.Z - cz * ChunkData.SizeXZ;
            total += await _engine.Carve.DestroySphereAsync(
                col, ChunkData.SizeXZ, ChunkData.Height, localCx, center.Y, localCz, radius);
        }

        var affected = new HashSet<SectionCoord>();
        ExplosionKernels.CollectAffectedSections(
            center.X, center.Y, center.Z, radius, ChunkData.SizeXZ, affected);
        MarkSectionsDirty(affected);
        return total;
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
    public Task ReMeshColumn(int cx, int cz) => RemeshSectionsAsync(cx, cz, null).AsTask();

    /// <summary>
    /// Remesh selected section-Y indices of a column (null = full column).
    /// Returns number of sections that produced a mesh.
    /// </summary>
    private async ValueTask<int> RemeshSectionsAsync(int cx, int cz, List<int>? sectionYs)
    {
        if (!_blocksCache.TryGetValue((cx, cz), out var blocks)) return 0;
        var nxMinus = _blocksCache.TryGetValue((cx - 1, cz), out var a) ? a : null;
        var nxPlus  = _blocksCache.TryGetValue((cx + 1, cz), out var b) ? b : null;
        var nzMinus = _blocksCache.TryGetValue((cx, cz - 1), out var c) ? c : null;
        var nzPlus  = _blocksCache.TryGetValue((cx, cz + 1), out var d) ? d : null;

        List<(int sectionY, VoxelMeshPipeline.MeshResult mesh)> sectionMeshes;
        try
        {
            if (sectionYs == null || sectionYs.Count == 0)
            {
                sectionMeshes = await _engine.GenerateChunkMeshesAsync(
                    blocks, nxMinus, nxPlus, nzMinus, nzPlus);
            }
            else
            {
                sectionMeshes = await _engine.GenerateChunkMeshesSectionsAsync(
                    blocks, sectionYs, nxMinus, nxPlus, nzMinus, nzPlus);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[World] Re-mesh ({cx},{cz}) failed: {ex.Message}");
            return 0;
        }

        if (sectionYs == null || sectionYs.Count == 0)
        {
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
        }
        else
        {
            // Partial remesh: drop dirty sections that became all-air.
            var fresh = new HashSet<int>(sectionMeshes.Select(s => s.sectionY));
            foreach (int sy in sectionYs)
            {
                if (fresh.Contains(sy)) continue;
                var key = (cx, sy, cz);
                if (_chunks.TryGetValue(key, out var old))
                {
                    old.Dispose();
                    _chunks.Remove(key);
                }
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
        return sectionMeshes.Count;
    }

    private static bool TryParseChunkKey(string key, out int cx, out int cz)
    {
        cx = 0; cz = 0;
        int comma = key.IndexOf(',');
        if (comma <= 0) return false;
        return int.TryParse(key.AsSpan(0, comma), out cx) &&
               int.TryParse(key.AsSpan(comma + 1), out cz);
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
        _dirtySections.Clear();
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
