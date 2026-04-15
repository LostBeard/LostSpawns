using System.Runtime.InteropServices;
using LostSpawns.Models;
using LostSpawns.Rendering;
using SpawnDev.BlazorJS;

namespace LostSpawns.Services;

/// <summary>
/// Loads real-world heightmap data and generates terrain chunks from it.
/// Heightmap format: raw Int16 little-endian, row-major (north-to-south).
/// Each value = elevation in meters. Sea level = 0. Negative = underwater.
/// </summary>
public class HeightmapLoader
{
    private short[]? _heightmap;
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
    public async Task LoadAsync(BlazorJSRuntime js, string url)
    {
        Console.WriteLine($"[HeightmapLoader] Loading: {url}");

        using var response = await js.CallAsync<SpawnDev.BlazorJS.JSObjects.Response>("fetch", url);
        using var arrayBuffer = await response.ArrayBuffer();
        var bytes = arrayBuffer.ReadBytes();

        // Int16 = 2 bytes per sample
        int sampleCount = bytes.Length / 2;
        _gridSize = (int)Math.Sqrt(sampleCount);

        if (_gridSize * _gridSize != sampleCount)
            throw new InvalidOperationException($"Heightmap is not square: {sampleCount} samples, sqrt={Math.Sqrt(sampleCount):F2}");

        _heightmap = MemoryMarshal.Cast<byte, short>(bytes).ToArray();

        // Sea level at voxel Y=64 (gives room for underwater terrain)
        _seaLevelVoxel = 64;

        Console.WriteLine($"[HeightmapLoader] Loaded {_gridSize}x{_gridSize} heightmap ({bytes.Length:N0} bytes)");

        // Log elevation stats
        short min = short.MaxValue, max = short.MinValue;
        int waterCount = 0;
        foreach (var h in _heightmap)
        {
            if (h <= 0) waterCount++;
            if (h < min) min = h;
            if (h > max) max = h;
        }
        Console.WriteLine($"[HeightmapLoader] Elevation: {min}m to {max}m, water: {100.0 * waterCount / sampleCount:F1}%");
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

            FillColumn(chunk, x, z, h, slope);
        }

        PlaceTrees(chunk, chunkX, chunkZ);
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
    }

    private void FillColumn(ChunkData chunk, int x, int z, int surfaceY, int slope = 0)
    {
        surfaceY = Math.Clamp(surfaceY, 1, ChunkData.Height - 2);
        int elevAboveSea = surfaceY - _seaLevelVoxel;

        for (int y = 0; y < ChunkData.Height; y++)
        {
            BlockType block;
            if (y > surfaceY)
            {
                // Above surface: water if below sea level, air otherwise
                block = y <= _seaLevelVoxel ? BlockType.Water : BlockType.Air;
            }
            else if (y == surfaceY)
            {
                // Surface block - biome by elevation + slope
                if (surfaceY <= _seaLevelVoxel)
                    block = BlockType.Sand;           // underwater sand
                else if (elevAboveSea <= 3)
                    block = BlockType.Sand;           // beach (wider shore)
                else if (slope >= 4)
                    block = BlockType.Stone;           // cliff face - steep slope = exposed rock
                else if (elevAboveSea > 55)
                    block = BlockType.Stone;           // high altitude = rocky summit
                else if (elevAboveSea > 45)
                    block = slope >= 2 ? BlockType.Stone : BlockType.Dirt; // high = sparse vegetation
                else
                    block = BlockType.Grass;           // normal terrain
            }
            else if (y > surfaceY - 2 && slope >= 4)
            {
                block = BlockType.Stone;              // steep slopes are stone deeper too
            }
            else if (y > surfaceY - 4)
            {
                block = BlockType.Dirt;
            }
            else
            {
                block = BlockType.Stone;
            }

            chunk.SetBlock(x, y, z, block);
        }
    }

    private void PlaceTrees(ChunkData chunk, int chunkX, int chunkZ)
    {
        // Deterministic RNG per chunk
        var rng = new Random(HashCode.Combine(42, chunkX * 73856093, chunkZ * 19349663));

        for (int x = 3; x < ChunkData.SizeXZ - 3; x++)
        for (int z = 3; z < ChunkData.SizeXZ - 3; z++)
        {
            if (rng.NextDouble() > 0.012) continue; // ~1.2% chance

            // Find grass surface
            int groundY = -1;
            for (int y = ChunkData.Height - 1; y >= 0; y--)
            {
                if (chunk.GetBlock(x, y, z) == BlockType.Grass)
                {
                    groundY = y;
                    break;
                }
            }
            if (groundY < 0 || groundY > ChunkData.Height - 10) continue;
            if (groundY <= _seaLevelVoxel + 4) continue; // no trees on beach
            if (groundY > _seaLevelVoxel + 55) continue; // no trees on rocky summits
            // Skip if surface is stone (cliff face)
            if (chunk.GetBlock(x, groundY, z) == BlockType.Stone) continue;

            int trunkH = 4 + rng.Next(3); // 4-6 blocks tall

            // Trunk
            for (int y = 1; y <= trunkH; y++)
                chunk.SetBlock(x, groundY + y, z, BlockType.Wood);

            // Leaves
            int leafBase = groundY + trunkH;
            for (int ly = 0; ly <= 2; ly++)
            for (int lx = -2; lx <= 2; lx++)
            for (int lz = -2; lz <= 2; lz++)
            {
                if (Math.Abs(lx) == 2 && Math.Abs(lz) == 2) continue;
                if (ly == 2 && (Math.Abs(lx) > 1 || Math.Abs(lz) > 1)) continue;

                int tx = x + lx, ty = leafBase + ly, tz = z + lz;
                if (chunk.GetBlock(tx, ty, tz) == BlockType.Air)
                    chunk.SetBlock(tx, ty, tz, BlockType.Leaves);
            }
        }
    }
}
