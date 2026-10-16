using LostSpawns.Models;
using LostSpawns.Rendering;
using SpawnDev.SpawnJS;

namespace LostSpawns.Services;

/// <summary>
/// Loads real-world heightmap data and generates terrain chunks from it.
/// Heightmap format: raw Int16 little-endian, row-major (north-to-south).
/// Each value = elevation in meters. Sea level = 0. Negative = underwater.
/// </summary>
public class HeightmapLoader
{
    /// <summary>The world seed the trees and ground cover are placed with.</summary>
    public int WorldSeed { get; set; } = 42;

    private short[]? _heightmap;
    private bool _bathymetryApplied;
    private int _gridSize;
    private float _cellSizeMeters;
    private int _seaLevelVoxel;

    /// <summary>Whether a heightmap has been loaded.</summary>
    public bool IsLoaded => _heightmap != null;

    /// <summary>Grid size (e.g., 1024 for 1024x1024).</summary>
    public int GridSize => _gridSize;

    /// <summary>Size of each cell in meters.</summary>
    public float CellSizeMeters => _cellSizeMeters;

    /// <summary>
    /// Load a binary heightmap from a URL (static asset in wwwroot/maps/).
    /// </summary>
    public async Task LoadAsync(SpawnJSRuntime js, string url)
    {

        using var response = await js.CallAsync<string, SpawnDev.SpawnJS.JSObjects.Response>("fetch", url);
        using var arrayBuffer = await response.ArrayBuffer();
        var bytes = arrayBuffer.ReadBytes();

        // Int16 = 2 bytes per sample
        int sampleCount = bytes.Length / 2;
        _gridSize = (int)Math.Sqrt(sampleCount);

        if (_gridSize * _gridSize != sampleCount)
            throw new InvalidOperationException($"Heightmap is not square: {sampleCount} samples, sqrt={Math.Sqrt(sampleCount):F2}");

        // One managed copy: reinterpret bytes as short[] without a second ToArray().
        // MemoryMarshal.Cast is a view over `bytes`; we must keep a copy that owns the
        // storage once `bytes` is eligible for GC - allocate short[] once and BlockCopy.
        _heightmap = new short[sampleCount];
        Buffer.BlockCopy(bytes, 0, _heightmap, 0, bytes.Length);

        // Sea level at voxel Y=64 (gives room for underwater terrain)
        _seaLevelVoxel = 64;

        Console.WriteLine($"[Heightmap] {_gridSize}x{_gridSize}, {bytes.Length:N0} bytes");
    }

    /// <summary>
    /// The DEM has no bathymetry: every sea cell is exactly 0, so the sea floor would sit at sea level and hold no water at all. This gives
    /// each cell that is not land (elevation 0 or below) a floor that deepens with its distance from the nearest land cell (a two-pass
    /// chamfer distance, 3 per cell side and 4 per diagonal), about 1 block at the shore to 28 blocks far out, with a little relief.
    /// </summary>
    private void ApplyBathymetry()
    {
        int n = _gridSize;
        const int inf = 60000;
        var dist = new ushort[n * n];
        for (int i = 0; i < dist.Length; i++) dist[i] = _heightmap![i] > 0 ? (ushort)0 : (ushort)inf;

        for (int y = 0; y < n; y++)
        for (int x = 0; x < n; x++)
        {
            int i = y * n + x;
            int d = dist[i];
            if (x > 0) d = Math.Min(d, dist[i - 1] + 3);
            if (y > 0)
            {
                d = Math.Min(d, dist[i - n] + 3);
                if (x > 0) d = Math.Min(d, dist[i - n - 1] + 4);
                if (x < n - 1) d = Math.Min(d, dist[i - n + 1] + 4);
            }
            dist[i] = (ushort)Math.Min(d, inf);
        }
        for (int y = n - 1; y >= 0; y--)
        for (int x = n - 1; x >= 0; x--)
        {
            int i = y * n + x;
            int d = dist[i];
            if (x < n - 1) d = Math.Min(d, dist[i + 1] + 3);
            if (y < n - 1)
            {
                d = Math.Min(d, dist[i + n] + 3);
                if (x < n - 1) d = Math.Min(d, dist[i + n + 1] + 4);
                if (x > 0) d = Math.Min(d, dist[i + n - 1] + 4);
            }
            dist[i] = (ushort)Math.Min(d, inf);
        }

        for (int i = 0; i < dist.Length; i++)
        {
            if (_heightmap![i] > 0) continue;
            float meters = dist[i] / 3f * _cellSizeMeters;
            float relief = (TileNoise.Rand(i % n, i / n, 9001) - 0.5f) * 1.4f;
            int depth = (int)Math.Clamp(MathF.Round(1f + meters * 0.055f + relief), 1f, 28f);
            _heightmap[i] = (short)-depth;
        }
    }

    /// <summary>
    /// Get the elevation in meters at a world position.
    /// World coordinates: 1 block = 1 meter. Origin at center of map.
    /// </summary>
    public int GetElevation(float worldX, float worldZ)
    {
        if (_heightmap == null) return 0;

        // World origin at center of map. Convert to grid coords.
        float mapSizeMeters = _gridSize * _cellSizeMeters;
        float gridX = (worldX + mapSizeMeters / 2) / _cellSizeMeters;
        float gridZ = (worldZ + mapSizeMeters / 2) / _cellSizeMeters;

        // Bilinear interpolation for smooth terrain between heightmap cells
        int gx0 = Math.Clamp((int)MathF.Floor(gridX), 0, _gridSize - 2);
        int gz0 = Math.Clamp((int)MathF.Floor(gridZ), 0, _gridSize - 2);
        float fx = gridX - gx0;
        float fz = gridZ - gz0;

        float h00 = _heightmap[gz0 * _gridSize + gx0];
        float h10 = _heightmap[gz0 * _gridSize + gx0 + 1];
        float h01 = _heightmap[(gz0 + 1) * _gridSize + gx0];
        float h11 = _heightmap[(gz0 + 1) * _gridSize + gx0 + 1];

        float elev = h00 * (1 - fx) * (1 - fz)
                   + h10 * fx * (1 - fz)
                   + h01 * (1 - fx) * fz
                   + h11 * fx * fz;

        return _seaLevelVoxel + (int)MathF.Round(elev);
    }

    /// <summary>
    /// Generate a chunk from the heightmap at the given chunk coordinates.
    /// Chunk coordinates are in blocks (16 blocks per chunk).
    /// </summary>
    public ChunkData GenerateChunk(int chunkX, int chunkZ)
    {
        var chunk = new ChunkData(chunkX, chunkZ);

        for (int x = 0; x < ChunkData.SizeXZ; x++)
        for (int z = 0; z < ChunkData.SizeXZ; z++)
        {
            float worldX = chunkX * ChunkData.SizeXZ + x;
            float worldZ = chunkZ * ChunkData.SizeXZ + z;
            int h = GetElevation(worldX, worldZ);

            // Compute slope by checking neighbor heights (steepness detection)
            int hN = GetElevation(worldX, worldZ - 1);
            int hS = GetElevation(worldX, worldZ + 1);
            int hE = GetElevation(worldX + 1, worldZ);
            int hW = GetElevation(worldX - 1, worldZ);
            int slope = Math.Max(Math.Abs(h - hN), Math.Max(Math.Abs(h - hS),
                        Math.Max(Math.Abs(h - hE), Math.Abs(h - hW))));

            FillColumn(chunk, (int)worldX, (int)worldZ, x, z, h, slope);
        }

        Vegetation.Place(chunk, chunkX, chunkZ, SurfaceAt, _seaLevelVoxel, WorldSeed);
        return chunk;
    }

    /// <summary>
    /// How many chunks wide/deep the map is.
    /// </summary>
    public int MapSizeInChunks
    {
        get
        {
            if (_heightmap == null) return 0;
            float mapSizeMeters = _gridSize * _cellSizeMeters;
            return (int)(mapSizeMeters / ChunkData.SizeXZ);
        }
    }

    /// <summary>
    /// Set the cell size (meters per heightmap pixel). Call before using GetElevation.
    /// </summary>
    public void SetCellSize(float cellSizeMeters)
    {
        _cellSizeMeters = cellSizeMeters;
        // The elevation data is flat zero over the sea: give it a floor that deepens away from the shore (needs the cell size, so it is made here)
        if (_heightmap != null && !_bathymetryApplied)
        {
            ApplyBathymetry();
            _bathymetryApplied = true;
        }
    }

    /// <summary>
    /// The ground at a world column: the height of its top block and which block that is. Pure in the position, so the trees
    /// (which reach over chunk borders) can ask about the ground of a neighbouring chunk.
    /// </summary>
    public (int y, BlockType type) SurfaceAt(int worldX, int worldZ)
    {
        int h = Math.Clamp(GetElevation(worldX, worldZ), 1, ChunkData.Height - 2);
        int hN = GetElevation(worldX, worldZ - 1);
        int hS = GetElevation(worldX, worldZ + 1);
        int hE = GetElevation(worldX + 1, worldZ);
        int hW = GetElevation(worldX - 1, worldZ);
        int slope = Math.Max(Math.Abs(h - hN), Math.Max(Math.Abs(h - hS), Math.Max(Math.Abs(h - hE), Math.Abs(h - hW))));
        return (h, SurfaceBlock(h, slope, worldX, worldZ));
    }

    /// <summary>The block on top of the ground at a place: sand and gravel on the shore, granite on cliffs and summits, grass elsewhere.</summary>
    private BlockType SurfaceBlock(int surfaceY, int slope, int worldX, int worldZ)
    {
        int elevAboveSea = surfaceY - _seaLevelVoxel;
        if (surfaceY <= _seaLevelVoxel)
        {
            // The sea floor: sand near the shore, gravel where it is deeper or the shore is stony
            return (_seaLevelVoxel - surfaceY > 2 || Vegetation.ShoreGravel(worldX, worldZ) > 0.5f) ? BlockType.Gravel : BlockType.Sand;
        }
        if (elevAboveSea <= 3)
        {
            if (slope >= 3) return BlockType.Granite;                                   // rocky shore
            return Vegetation.ShoreGravel(worldX, worldZ) > 0.45f ? BlockType.Gravel : BlockType.Sand;
        }
        if (slope >= 4) return BlockType.Granite;                                       // cliff face
        if (elevAboveSea > 55) return BlockType.Granite;                                // rocky summit
        if (elevAboveSea > 45) return slope >= 2 ? BlockType.Granite : BlockType.Dirt;  // high, sparse
        if (elevAboveSea > 6 && Vegetation.BarePatch(worldX, worldZ) > 0.8f && slope <= 1) return BlockType.Dirt; // bare earth
        return BlockType.Grass;
    }

    private void FillColumn(ChunkData chunk, int worldX, int worldZ, int x, int z, int surfaceY, int slope = 0)
    {
        surfaceY = Math.Clamp(surfaceY, 1, ChunkData.Height - 2);
        var top = SurfaceBlock(surfaceY, slope, worldX, worldZ);
        // What lies under the top layer: a beach is sand deep, a cliff is granite a little way in, soil elsewhere
        var under = top switch
        {
            BlockType.Sand or BlockType.Gravel => BlockType.Sand,
            BlockType.Granite => BlockType.Granite,
            _ => BlockType.Dirt,
        };

        for (int y = 0; y < ChunkData.Height; y++)
        {
            BlockType block;
            if (y > surfaceY)
                block = y <= _seaLevelVoxel ? BlockType.Water : BlockType.Air;
            else if (y == surfaceY)
                block = top;
            else if (y > surfaceY - 2 && slope >= 4)
                block = BlockType.Granite;                  // steep slopes are rock a little deeper too
            else if (y > surfaceY - 4)
                block = under;
            else
                block = BlockType.Stone;

            chunk.SetBlock(x, y, z, block);
        }
    }
}
