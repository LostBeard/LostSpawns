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
/// Full GPU pipeline: heightmap -> block fill -> VoxelEngine column mesh -> GPU-resident quads.
/// No CPU readback - mesh data stays on GPU from generation to rendering.
/// Dig/terraform: edits mark dirty columns; ProcessDirtyRemesh drains one coalesced pass per frame
/// (pauses stream mesh while dirty) so paint strokes do not stampede column remeshes.
/// </summary>
public class WorldService
{
    private readonly VoxelEngineService _engine;
    // Meshed columns keyed by (cx, cz): one mesh per column (opaque, plants, water), drawn with one call per list
    private readonly Dictionary<(int cx, int cz), WorldColumn> _columns = new();
    // Pending/in-flight track chunk columns (cx, cz)
    private readonly Queue<(int cx, int cz)> _pendingQueue = new();
    private readonly HashSet<(int cx, int cz)> _inFlight = new();
    private readonly Queue<WorldColumn> _readyQueue = new();
    // Track which columns are loaded (for chunk streaming logic)
    private readonly HashSet<(int cx, int cz)> _loadedColumns = new();
    // GPU copy of each cached block column, as ushort ids: what the column mesher reads (the column and its four neighbours)
    private readonly Dictionary<(int cx, int cz), MemoryBuffer1D<ushort, Stride1D.Dense>> _gpuBlocks = new();
    // Origin slots of the block pipeline: a column owns one while it is loaded
    private readonly Stack<int> _freeSlots = new();
    private int _nextSlot;
    /// <summary>How many columns can be drawn at once (the block pipeline's origin buffer).</summary>
    public const int MaxColumnSlots = 4096;
    // CPU-side block cache keyed by (cx, cz). Populated whenever a chunk's blocks are
    // generated - self load, neighbor-padding lookup, or otherwise. Lets the 4 neighbor
    // lookups per column hit already-computed blocks instead of regenerating them,
    // avoiding the ~5x CPU work amplification in the initial load path.
    private readonly Dictionary<(int cx, int cz), byte[]> _blocksCache = new();

    // Dig remesh coalesce: columns dirty since last ProcessDirtyRemesh.
    // Do NOT fire-and-forget a remesh per SphereOp - that stampeded _meshLock.
    private readonly HashSet<(int cx, int cz)> _dirtyColumns = new();
    private bool _dirtyRemeshRunning;
    // Reused raycast section buffer - CastWorld consumes each getSection result before
    // the next call, so one int[4096] is safe and avoids WASM GC on terraform preview.
    private readonly int[] _raycastSectionBuf = new int[16 * 16 * 16];

    // Dig stage timings (last completed remesh pass) - F3 / console.
    public double LastCarveMs { get; private set; }
    public double LastRemeshMs { get; private set; }
    public int LastDirtySectionCount { get; private set; }
    public int LastSectionsMeshed { get; private set; }
    public int DirtyRemeshPending => _dirtyColumns.Count;
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
    /// <summary>Sections that hold quads, over all loaded columns.</summary>
    public int LoadedSections { get { int n = 0; foreach (var c in _columns.Values) n += c.SectionsWithQuads; return n; } }
    public int LoadedColumns => _loadedColumns.Count;
    public bool HasPendingChunks => _pendingQueue.Count > 0 || _inFlight.Count > 0 || _readyQueue.Count > 0;
    public int PendingCount => _pendingQueue.Count + _inFlight.Count + _readyQueue.Count;

    /// <summary>The heightmap loader, if a real-world map is loaded.</summary>
    public HeightmapLoader? HeightmapLoader => _heightmapLoader;

    /// <summary>All loaded columns with their GPU mesh data. Used by the terrain renderer for drawing.</summary>
    public IReadOnlyDictionary<(int cx, int cz), WorldColumn> Columns => _columns;

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
                // Give back the column's mesh and origin slot
                if (_columns.Remove(col, out var gone))
                    ReleaseColumn(gone);
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
        {
            _blocksCache.Remove(key);
            if (_gpuBlocks.Remove(key, out var gpu))
                gpu.Dispose();
        }
    }

    private void DispatchGpuPending()
    {
        // Dig remesh wins: do not start new stream columns while dirty sections pending.
        if (_dirtyColumns.Count > 0 || _dirtyRemeshRunning)
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
    /// Coalesce dirty columns into one remesh pass: each is uploaded again (its blocks changed) and meshed on the GPU.
    /// </summary>
    public void ProcessDirtyRemesh()
    {
        if (_dirtyRemeshRunning || _dirtyColumns.Count == 0) return;
        _dirtyRemeshRunning = true;
        var snapshot = _dirtyColumns.ToList();
        _dirtyColumns.Clear();
        LastDirtySectionCount = snapshot.Count;
        _ = DrainDirtyRemeshAsync(snapshot);
    }

    private async Task DrainDirtyRemeshAsync(List<(int cx, int cz)> snapshot)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        int meshed = 0;
        _engine.BeginDigRemesh();
        try
        {
            foreach (var (cx, cz) in snapshot)
                meshed += await RemeshColumnAsync(cx, cz, digPriority: true) ? 1 : 0;
        }
        finally
        {
            _engine.EndDigRemesh();
            sw.Stop();
            LastRemeshMs = sw.Elapsed.TotalMilliseconds;
            LastSectionsMeshed = meshed;
            _dirtyRemeshRunning = false;
            if (_dirtyColumns.Count == 0)
                DispatchGpuPending();
        }
    }

    private void MarkSectionsDirty(IEnumerable<SectionCoord> coords)
    {
        foreach (var c in coords)
        {
            if (c.Sy < 0 || c.Sy >= 16) continue;
            _dirtyColumns.Add((c.Cx, c.Cz));
        }
    }

    private void MarkBlockDirty(int worldX, int worldY, int worldZ)
    {
        const int ss = ChunkData.SizeXZ;
        int cx = (int)MathF.Floor(worldX / (float)ss);
        int cz = (int)MathF.Floor(worldZ / (float)ss);
        int lx = worldX - cx * ss;
        int lz = worldZ - cz * ss;

        _dirtyColumns.Add((cx, cz));
        // A block on the border changes the faces of the column beside it
        if (lx == 0) _dirtyColumns.Add((cx - 1, cz));
        if (lx == ss - 1) _dirtyColumns.Add((cx + 1, cz));
        if (lz == 0) _dirtyColumns.Add((cx, cz - 1));
        if (lz == ss - 1) _dirtyColumns.Add((cx, cz + 1));

        // Kick remesh now (don't wait for next frame's ProcessReadyChunks) so pickaxe
        // dirt updates in the same interaction, once stream tasks yield the mesh lock.
        ProcessDirtyRemesh();
    }

    /// <summary>
    /// Full GPU pipeline: heightmap -> fill blocks -> upload -> column mesh. Mesh data stays GPU-resident (no CPU readback).
    /// Also generates the 4 XZ neighbor chunks so the mesher can see real neighbor block data at the column
    /// borders, preventing see-through faces at chunk boundaries.
    /// </summary>
    private async Task GenerateChunkGpuAsync(int cx, int cz)
    {
        try
        {
            var blocks = await GetOrGenerateBlocksAsync(cx, cz);
            if (blocks == null) { _inFlight.Remove((cx, cz)); return; }

            if (_loadedColumns.Contains((cx, cz))) { _inFlight.Remove((cx, cz)); return; }

            // Dig remesh must win the mesh lock. In-flight stream tasks that already
            // passed DispatchGpuPending used to hold _meshLock for seconds and made
            // pickaxe dirt lag ~5s. Yield here until dig drain finishes.
            while (_dirtyColumns.Count > 0 || _dirtyRemeshRunning)
                await Task.Yield();

            if (_loadedColumns.Contains((cx, cz))) { _inFlight.Remove((cx, cz)); return; }

            // XZ neighbors for boundary data. Cached across calls so a neighbor
            // generated once gets reused when adjacent columns mesh.
            await GetOrGenerateBlocksAsync(cx - 1, cz);
            await GetOrGenerateBlocksAsync(cx + 1, cz);
            await GetOrGenerateBlocksAsync(cx, cz - 1);
            await GetOrGenerateBlocksAsync(cx, cz + 1);

            // Re-check dig priority after neighbor gen (can take a while).
            while (_dirtyColumns.Count > 0 || _dirtyRemeshRunning)
                await Task.Yield();

            var column = await MeshNewColumnAsync(cx, cz);

            _inFlight.Remove((cx, cz));
            if (column == null) { DispatchGpuPending(); return; }
            if (_loadedColumns.Contains((cx, cz)))
            {
                ReleaseColumn(column);
                return;
            }

            _loadedColumns.Add((cx, cz));
            _readyQueue.Enqueue(column);

            DispatchGpuPending();
        }
        catch (Exception ex)
        {
            _inFlight.Remove((cx, cz));
            Console.WriteLine($"[World] Chunk ({cx},{cz}) error: {ex.Message}");
            DispatchGpuPending();
        }
    }

    /// <summary>Mesh a column that is not yet in the world, with the neighbours that exist. Null when there is no room or no data.</summary>
    private async Task<WorldColumn?> MeshNewColumnAsync(int cx, int cz)
    {
        if (!_blocksCache.TryGetValue((cx, cz), out var blocks)) return null;
        int slot = TakeSlot();
        if (slot < 0)
        {
            Console.WriteLine($"[World] No column slot left for ({cx},{cz})");
            return null;
        }
        var column = new WorldColumn { Cx = cx, Cz = cz, Slot = slot };
        try
        {
            var gpu = EnsureGpuBlocks(cx, cz, blocks);
            column.Mesh = await _engine.MeshColumnAsync(gpu, GpuNeighbour(cx - 1, cz), GpuNeighbour(cx + 1, cz), GpuNeighbour(cx, cz - 1), GpuNeighbour(cx, cz + 1));
            column.UpdateSections();
            return column;
        }
        catch
        {
            ReleaseColumn(column);
            throw;
        }
    }

    private MemoryBuffer1D<ushort, Stride1D.Dense>? GpuNeighbour(int cx, int cz)
        => _gpuBlocks.TryGetValue((cx, cz), out var b) ? b : null;

    /// <summary>The column's GPU block buffer, made from the CPU array when it does not exist yet.</summary>
    private MemoryBuffer1D<ushort, Stride1D.Dense> EnsureGpuBlocks(int cx, int cz, byte[] blocks)
    {
        if (_gpuBlocks.TryGetValue((cx, cz), out var existing)) return existing;
        var gpu = _engine.UploadColumn(blocks);
        _gpuBlocks[(cx, cz)] = gpu;
        return gpu;
    }

    private int TakeSlot()
    {
        if (_freeSlots.Count > 0) return _freeSlots.Pop();
        return _nextSlot < MaxColumnSlots ? _nextSlot++ : -1;
    }

    private void ReleaseColumn(WorldColumn column)
    {
        column.Mesh?.Dispose();
        column.Mesh = null;
        _freeSlots.Push(column.Slot);
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
            var column = _readyQueue.Dequeue();
            var key = (column.Cx, column.Cz);
            if (_columns.TryGetValue(key, out var old))
            {
                // Replaced (a remesh finished first): the old one goes back
                ReleaseColumn(old);
            }
            _columns[key] = column;
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

                await GetOrGenerateBlocksAsync(cx - 1, cz);
                await GetOrGenerateBlocksAsync(cx + 1, cz);
                await GetOrGenerateBlocksAsync(cx, cz - 1);
                await GetOrGenerateBlocksAsync(cx, cz + 1);

                var column = await MeshNewColumnAsync(cx, cz);
                if (column == null) continue;

                _loadedColumns.Add((cx, cz));
                _columns[(cx, cz)] = column;
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
            EnsureGpuBlocks(ncx, ncz, blocks);
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
    public Task ReMeshColumn(int cx, int cz) => RemeshColumnAsync(cx, cz, digPriority: false);

    /// <summary>
    /// Upload a column's blocks again (they changed) and mesh it, swapping the new mesh in when it is ready.
    /// Returns true when the column has a mesh now.
    /// </summary>
    private async Task<bool> RemeshColumnAsync(int cx, int cz, bool digPriority)
    {
        if (!_blocksCache.TryGetValue((cx, cz), out var blocks)) return false;
        if (!_columns.TryGetValue((cx, cz), out var column)) return false;

        try
        {
            var gpu = _gpuBlocks.TryGetValue((cx, cz), out var existing)
                ? _engine.UploadColumn(blocks, existing)
                : EnsureGpuBlocks(cx, cz, blocks);
            var mesh = await _engine.MeshColumnAsync(gpu, GpuNeighbour(cx - 1, cz), GpuNeighbour(cx + 1, cz), GpuNeighbour(cx, cz - 1), GpuNeighbour(cx, cz + 1), digPriority);

            // The column may have been unloaded while the mesh was made
            if (!_columns.TryGetValue((cx, cz), out var still) || !ReferenceEquals(still, column))
            {
                mesh.Dispose();
                return false;
            }
            var old = column.Mesh;
            column.Mesh = mesh;
            column.UpdateSections();
            old?.Dispose();
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[World] Re-mesh ({cx},{cz}) failed: {ex.Message}");
            return false;
        }
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
        foreach (var column in _columns.Values)
            column.Mesh?.Dispose();
        while (_readyQueue.Count > 0)
            _readyQueue.Dequeue().Mesh?.Dispose();
        foreach (var gpu in _gpuBlocks.Values)
            gpu.Dispose();

        _columns.Clear();
        _gpuBlocks.Clear();
        _freeSlots.Clear();
        _nextSlot = 0;
        _loadedColumns.Clear();
        _pendingQueue.Clear();
        _inFlight.Clear();
        _blocksCache.Clear();
        _dirtyColumns.Clear();
        _lastCX = int.MinValue;
        _lastCZ = int.MinValue;
        _generator = null;
        IsInitialized = false;
    }
}

/// <summary>
/// A loaded column: its GPU mesh (quad lists of the opaque, plant and water passes, in the shared arena) and the slot that
/// holds its origin in the block pipeline.
/// </summary>
public class WorldColumn
{
    public required int Cx { get; init; }
    public required int Cz { get; init; }
    /// <summary>Index of this column's origin in the block pipeline (written once by the renderer).</summary>
    public required int Slot { get; init; }
    public ColumnMesh? Mesh { get; set; }
    /// <summary>Sections that hold quads, and the lowest and highest of them (the box the frustum test uses).</summary>
    public int SectionsWithQuads { get; private set; }
    public int LowestSection { get; private set; } = int.MaxValue;
    public int HighestSection { get; private set; } = -1;

    public bool HasMesh => Mesh != null && HighestSection >= 0;

    public void UpdateSections()
    {
        SectionsWithQuads = 0;
        LowestSection = int.MaxValue;
        HighestSection = -1;
        var mesh = Mesh;
        if (mesh == null) return;
        for (int sy = 0; sy < mesh.Opaque.SectionCount; sy++)
        {
            if (mesh.Opaque.QuadsIn(sy) + mesh.Plants.QuadsIn(sy) + mesh.Water.QuadsIn(sy) == 0) continue;
            SectionsWithQuads++;
            LowestSection = Math.Min(LowestSection, sy);
            HighestSection = sy;
        }
    }
}
