using LostSpawns.Models;

namespace LostSpawns.Rendering;

/// <summary>
/// Trees and ground cover. Everything here is a pure function of the world position and the seed, never of the chunk being generated:
/// a tree is placed by the 4 x 4 cell its trunk stands in, so a crown that reaches over a chunk border is drawn by both chunks, the same,
/// and nothing ends at a seam. Forests and meadows come from large-scale noise: dense spruce with ferns under it, open meadow with
/// grass and flowers, birch-and-oak groves in places.
/// </summary>
public static class Vegetation
{
    /// <summary>The ground at a world column: its surface height and the block on top of it.</summary>
    public delegate (int y, BlockType type) SurfaceFn(int wx, int wz);

    private static readonly PerlinNoise Noise = new(1337);
    private const int CellSize = 4;
    private const int MaxCrownRadius = 3;

    /// <summary>How forested a place is, 0 (open meadow) to 1 (dense forest). Patches a few hundred blocks across.</summary>
    public static float Forest(float wx, float wz)
    {
        float n = Noise.OctaveNoise(wx * 0.0042f, wz * 0.0042f, 3);
        return TileNoise.SmoothStep(-0.08f, 0.32f, n);
    }

    /// <summary>How much of a forest is broadleaf (oak and birch) rather than spruce, 0..1.</summary>
    public static float Broadleaf(float wx, float wz)
    {
        float n = Noise.OctaveNoise(wx * 0.0075f + 41f, wz * 0.0075f - 17f, 2);
        return TileNoise.SmoothStep(0.12f, 0.35f, n);
    }

    /// <summary>A bare-earth patch: forest floor, game trails, 0..1.</summary>
    public static float BarePatch(float wx, float wz)
    {
        float n = Noise.OctaveNoise(wx * 0.05f + 7f, wz * 0.05f + 3f, 2);
        return TileNoise.SmoothStep(0.30f, 0.50f, n);
    }

    /// <summary>A shore patch: gravel along the water instead of sand, 0..1.</summary>
    public static float ShoreGravel(float wx, float wz)
    {
        float n = Noise.OctaveNoise(wx * 0.03f - 11f, wz * 0.03f + 5f, 2);
        return TileNoise.SmoothStep(0.05f, 0.30f, n);
    }

    private static float Hash01(int a, int b, int salt) => TileNoise.Rand(a, b, salt);

    /// <summary>Plant the chunk: trees first, then the cover on the ground between them.</summary>
    public static void Place(ChunkData chunk, int cx, int cz, SurfaceFn surface, int seaLevel, int worldSeed)
    {
        PlaceTrees(chunk, cx, cz, surface, seaLevel, worldSeed);
        PlaceCover(chunk, cx, cz, worldSeed);
    }

    private static void PlaceTrees(ChunkData chunk, int cx, int cz, SurfaceFn surface, int seaLevel, int seed)
    {
        int x0 = cx * ChunkData.SizeXZ, z0 = cz * ChunkData.SizeXZ;
        // Every cell whose tree could reach this chunk: the chunk, and one crown radius round it
        int cellX0 = FloorDiv(x0 - MaxCrownRadius, CellSize), cellX1 = FloorDiv(x0 + ChunkData.SizeXZ - 1 + MaxCrownRadius, CellSize);
        int cellZ0 = FloorDiv(z0 - MaxCrownRadius, CellSize), cellZ1 = FloorDiv(z0 + ChunkData.SizeXZ - 1 + MaxCrownRadius, CellSize);
        for (int gz = cellZ0; gz <= cellZ1; gz++)
        for (int gx = cellX0; gx <= cellX1; gx++)
        {
            // One candidate trunk per cell, jittered inside it
            int wx = gx * CellSize + (int)(Hash01(gx, gz, seed + 11) * CellSize);
            int wz = gz * CellSize + (int)(Hash01(gx, gz, seed + 12) * CellSize);
            float forest = Forest(wx, wz);
            // A meadow keeps a lone tree here and there; a forest fills nearly every cell
            float presence = 0.035f + 0.80f * forest * forest;
            if (Hash01(gx, gz, seed + 13) > presence) continue;

            var (groundY, top) = surface(wx, wz);
            if (top != BlockType.Grass) continue;
            if (groundY <= seaLevel + 4 || groundY > seaLevel + 55 || groundY > ChunkData.Height - 16) continue;

            bool broadleaf = Broadleaf(wx, wz) * forest > 0.30f || (forest < 0.2f && Hash01(gx, gz, seed + 14) < 0.55f);
            float roll = Hash01(gx, gz, seed + 15);
            if (broadleaf) Oak(chunk, x0, z0, wx, groundY, wz, roll);
            else Spruce(chunk, x0, z0, wx, groundY, wz, roll);
        }
    }

    private static int FloorDiv(int a, int b) => (int)MathF.Floor(a / (float)b);

    // Writes one block of a tree if it falls inside this chunk and the cell is free (a tree never replaces ground)
    private static void Put(ChunkData chunk, int x0, int z0, int wx, int y, int wz, BlockType type, bool overwritePlant = true)
    {
        int lx = wx - x0, lz = wz - z0;
        if ((uint)lx >= ChunkData.SizeXZ || (uint)lz >= ChunkData.SizeXZ || (uint)y >= ChunkData.Height) return;
        var at = chunk.GetBlock(lx, y, lz);
        if (at == BlockType.Air || (overwritePlant && at.IsPlant()))
        {
            // A trunk beats a leaf that another tree put there first
            chunk.SetBlock(lx, y, lz, type);
        }
        else if (type == BlockType.Wood && at == BlockType.Leaves)
        {
            chunk.SetBlock(lx, y, lz, type);
        }
    }

    private static void Spruce(ChunkData chunk, int x0, int z0, int wx, int groundY, int wz, float roll)
    {
        int height = 8 + (int)(roll * 6f);             // trunk 8..13
        int top = groundY + height;
        for (int y = groundY + 1; y <= top; y++)
            Put(chunk, x0, z0, wx, y, wz, BlockType.Wood);

        // A cone of needle tiers: wide at the bottom (3), narrowing to a point; every second layer pulled in a block
        int firstLayer = groundY + 3;
        for (int y = top + 1; y >= firstLayer; y--)
        {
            int i = top + 1 - y;                         // layers below the tip
            int baseRadius = Math.Min(MaxCrownRadius, 1 + i / 3);
            int radius = i == 0 ? 0 : (i % 2 == 0 ? baseRadius : Math.Max(baseRadius - 1, 1));
            for (int dz = -radius; dz <= radius; dz++)
            for (int dx = -radius; dx <= radius; dx++)
            {
                if (dx == 0 && dz == 0 && y <= top) continue;                    // the trunk
                if (radius >= 2 && Math.Abs(dx) == radius && Math.Abs(dz) == radius) continue; // round the corners
                if (dx * dx + dz * dz > radius * radius + 1) continue;
                Put(chunk, x0, z0, wx + dx, y, wz + dz, BlockType.Leaves);
            }
        }
    }

    private static void Oak(ChunkData chunk, int x0, int z0, int wx, int groundY, int wz, float roll)
    {
        int trunk = 4 + (int)(roll * 3f);               // 4..6
        int top = groundY + trunk;
        for (int y = groundY + 1; y <= top; y++)
            Put(chunk, x0, z0, wx, y, wz, BlockType.Wood);

        // A round crown: radius 2, 3, 3, 2, 1 from the last block of the trunk up
        ReadOnlySpan<int> radii = stackalloc int[] { 2, 3, 3, 2, 1 };
        for (int layer = 0; layer < radii.Length; layer++)
        {
            int r = radii[layer];
            int y = top - 1 + layer;
            for (int dz = -r; dz <= r; dz++)
            for (int dx = -r; dx <= r; dx++)
            {
                if (dx == 0 && dz == 0 && y <= top) continue;
                if (dx * dx + dz * dz > r * r + 1) continue;
                // Rag the edge a little so the crown is not a perfect ball (stable per block)
                if (r >= 2 && dx * dx + dz * dz >= r * r && Hash01(wx + dx, wz + dz + y * 31, 77) < 0.35f) continue;
                Put(chunk, x0, z0, wx + dx, y, wz + dz, BlockType.Leaves);
            }
        }
    }

    /// <summary>Grass, ferns and flowers on the open grass of the chunk.</summary>
    private static void PlaceCover(ChunkData chunk, int cx, int cz, int seed)
    {
        int x0 = cx * ChunkData.SizeXZ, z0 = cz * ChunkData.SizeXZ;
        for (int lz = 0; lz < ChunkData.SizeXZ; lz++)
        for (int lx = 0; lx < ChunkData.SizeXZ; lx++)
        {
            // The highest block that is not air
            int y = ChunkData.Height - 1;
            while (y > 0 && chunk.GetBlock(lx, y, lz) == BlockType.Air) y--;
            if (chunk.GetBlock(lx, y, lz) != BlockType.Grass) continue;
            if (y + 1 >= ChunkData.Height || chunk.GetBlock(lx, y + 1, lz) != BlockType.Air) continue;

            int wx = x0 + lx, wz = z0 + lz;
            float forest = Forest(wx, wz);
            float roll = Hash01(wx, wz, seed + 21);
            float flowerPatch = Noise.OctaveNoise(wx * 0.045f + 19f, wz * 0.045f - 29f, 2);

            BlockType plant = BlockType.Air;
            if (forest > 0.3f && roll < 0.16f * forest) plant = BlockType.Fern;
            else if (roll < 0.10f + 0.24f * (1f - forest)) plant = BlockType.TallGrass;
            else if (flowerPatch > 0.15f && Hash01(wx, wz, seed + 22) < 0.10f * (1f - 0.7f * forest))
                plant = Hash01(wx, wz, seed + 23) < 0.5f ? BlockType.Dandelion : BlockType.Aster;
            if (plant != BlockType.Air) chunk.SetBlock(lx, y + 1, lz, plant);
        }
    }
}
