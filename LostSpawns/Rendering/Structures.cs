using System.Numerics;
using LostSpawns.Models;
using static LostSpawns.Rendering.TileNoise;

namespace LostSpawns.Rendering;

/// <summary>
/// The buildings of the island: sheds, cabins and farmhouses left standing and left to rot. One may stand in each 48 x 48 block cell, on flat
/// dry ground; its plan is a pure function of the cell, the seed and the terrain, so every chunk that a building touches builds the same wall, and
/// the game can ask for the plan of a cell (for its loot) without having built anything.
/// </summary>
public static class Structures
{
    public const int CellSize = 48;
    public const int WallHeight = 4;

    /// <summary>A well is a pool 3 x 3 behind a low cobble rim: somewhere to drink inland.</summary>
    public enum Kind { Shed, Cabin, House, Well }

    /// <summary>A building: its footprint in world blocks, the level of its floor, which wall has the door, and how far gone it is.</summary>
    public readonly record struct Plan(int CellX, int CellZ, Kind Kind, int X0, int Z0, int Width, int Depth, int FloorY, int DoorSide, float Ruin)
    {
        /// <summary>A name that is the same on every run: where its loot is remembered.</summary>
        public string Id => $"{CellX},{CellZ}";
        public Vector3 Centre => new(X0 + Width * 0.5f, FloorY + 1f, Z0 + Depth * 0.5f);
    }

    /// <summary>The building of a cell, or null when nothing stands there (no flat dry ground, or the cell is simply empty).</summary>
    public static Plan? PlanAt(int cellX, int cellZ, Vegetation.SurfaceFn surface, int seaLevel, int seed)
    {
        if (Rand(cellX, cellZ, seed + 101) > 0.3f) return null;

        float roll = Rand(cellX, cellZ, seed + 102);
        var kind = roll < 0.25f ? Kind.Shed : roll < 0.62f ? Kind.Cabin : roll < 0.86f ? Kind.House : Kind.Well;
        int w, d;
        switch (kind)
        {
            case Kind.Shed: w = 4 + (int)(Rand(cellX, cellZ, seed + 103) * 2f); d = 4 + (int)(Rand(cellX, cellZ, seed + 104) * 2f); break;
            case Kind.Well: w = 5; d = 5; break;
            case Kind.Cabin: w = 7 + (int)(Rand(cellX, cellZ, seed + 103) * 3f); d = 6 + (int)(Rand(cellX, cellZ, seed + 104) * 2f); break;
            default: w = 10 + (int)(Rand(cellX, cellZ, seed + 103) * 3f); d = 8 + (int)(Rand(cellX, cellZ, seed + 104) * 2f); break;
        }
        int x0 = cellX * CellSize + 4 + (int)(Rand(cellX, cellZ, seed + 105) * (CellSize - 8 - w));
        int z0 = cellZ * CellSize + 4 + (int)(Rand(cellX, cellZ, seed + 106) * (CellSize - 8 - d));

        // The ground under it: nine points, all dry land that is not rock, within two blocks of each other
        int lo = int.MaxValue, hi = int.MinValue;
        int[] xs = { x0, x0 + w / 2, x0 + w - 1 };
        int[] zs = { z0, z0 + d / 2, z0 + d - 1 };
        foreach (int sx in xs)
        foreach (int sz in zs)
        {
            var (y, type) = surface(sx, sz);
            if (y <= seaLevel + 3) return null;
            if (type != BlockType.Grass && type != BlockType.Dirt && type != BlockType.Gravel && type != BlockType.Sand) return null;
            lo = Math.Min(lo, y);
            hi = Math.Max(hi, y);
        }
        if (hi - lo > 2) return null;

        return new Plan(cellX, cellZ, kind, x0, z0, w, d, (lo + hi + 1) / 2, (int)(Rand(cellX, cellZ, seed + 107) * 4f), 0.25f + 0.6f * Rand(cellX, cellZ, seed + 108));
    }

    /// <summary>Where loot lies inside a building (the floor cells, standing height), a few for a shed, more for a house.</summary>
    public static List<Vector3> LootSpots(Plan plan, int seed)
    {
        if (plan.Kind == Kind.Well) return new List<Vector3>();
        int n = plan.Kind switch { Kind.Shed => 1 + (int)(Rand(plan.CellX, plan.CellZ, seed + 301) * 2f), Kind.Cabin => 2 + (int)(Rand(plan.CellX, plan.CellZ, seed + 301) * 2f), _ => 4 + (int)(Rand(plan.CellX, plan.CellZ, seed + 301) * 3f) };
        var spots = new List<Vector3>(n);
        for (int i = 0; i < n; i++)
        {
            int ix = 1 + (int)(Rand(plan.CellX + i * 17, plan.CellZ, seed + 302) * (plan.Width - 2));
            int iz = 1 + (int)(Rand(plan.CellX, plan.CellZ + i * 13, seed + 303) * (plan.Depth - 2));
            // Not in the partition of a farmhouse
            if (plan.Kind == Kind.House && plan.X0 + ix == PartitionX(plan)) ix = Math.Max(1, ix - 1);
            spots.Add(new Vector3(plan.X0 + ix + 0.5f, plan.FloorY + 1f, plan.Z0 + iz + 0.5f));
        }
        return spots;
    }

    private static int PartitionX(Plan p) => p.X0 + p.Width / 2;

    /// <summary>Build the part of the building that lies in this chunk.</summary>
    public static void Place(ChunkData chunk, int cx, int cz, Vegetation.SurfaceFn surface, int seaLevel, int seed)
    {
        int x0c = cx * ChunkData.SizeXZ, z0c = cz * ChunkData.SizeXZ;
        // A chunk lies inside one cell: the cells are 3 chunks wide and line up with the chunk grid
        int cellX = FloorDiv(x0c, CellSize), cellZ = FloorDiv(z0c, CellSize);
        var plan = PlanAt(cellX, cellZ, surface, seaLevel, seed);
        if (plan is not { } p) return;
        if (p.X0 + p.Width + 1 <= x0c || p.X0 - 1 >= x0c + ChunkData.SizeXZ || p.Z0 + p.Depth + 1 <= z0c || p.Z0 - 1 >= z0c + ChunkData.SizeXZ) return;

        int top = p.FloorY + WallHeight + 3;
        for (int wz = p.Z0 - 1; wz <= p.Z0 + p.Depth; wz++)
        for (int wx = p.X0 - 1; wx <= p.X0 + p.Width; wx++)
        {
            int lx = wx - x0c, lz = wz - z0c;
            if ((uint)lx >= ChunkData.SizeXZ || (uint)lz >= ChunkData.SizeXZ) continue;

            bool inside = wx >= p.X0 && wx < p.X0 + p.Width && wz >= p.Z0 && wz < p.Z0 + p.Depth;
            // The ground and what grows on it (grass, trees, a hill) are cleared from the floor up, a block round the walls too
            for (int y = p.FloorY + 1; y <= top && y < ChunkData.Height; y++) chunk.SetBlock(lx, y, lz, BlockType.Air);
            if (!inside) continue;

            // A foundation of cobble where the ground is lower than the floor, planks on top
            for (int y = p.FloorY - 1; y >= p.FloorY - 4; y--)
            {
                var at = chunk.GetBlock(lx, y, lz);
                if (at == BlockType.Air || at == BlockType.Water || at.IsPlant()) chunk.SetBlock(lx, y, lz, BlockType.Cobblestone);
                else break;
            }
            int ix = wx - p.X0, iz = wz - p.Z0;
            if (p.Kind == Kind.Well)
            {
                BuildWell(chunk, p, lx, lz, wx, wz, ix, iz, seed);
                continue;
            }
            chunk.SetBlock(lx, p.FloorY, lz, BlockType.Planks);

            bool wallCell = ix == 0 || iz == 0 || ix == p.Width - 1 || iz == p.Depth - 1;
            bool partition = p.Kind == Kind.House && wx == PartitionX(p) && !wallCell;
            if (wallCell || partition) BuildWall(chunk, p, lx, lz, wx, wz, ix, iz, partition, seed);

            // The roof: planks, with holes where it has fallen in
            if (Rand(wx, wz + 7, seed + 205) > 0.18f * (0.4f + p.Ruin)) chunk.SetBlock(lx, p.FloorY + WallHeight + 1, lz, BlockType.Planks);
        }
    }

    // A rim of cobble two blocks high round a pool that is two deep (the water's top is a block under the rim's)
    private static void BuildWell(ChunkData chunk, Plan p, int lx, int lz, int wx, int wz, int ix, int iz, int seed)
    {
        bool rim = ix == 0 || iz == 0 || ix == p.Width - 1 || iz == p.Depth - 1;
        if (rim)
        {
            chunk.SetBlock(lx, p.FloorY, lz, BlockType.Cobblestone);
            if (Rand(wx, wz, seed + 210) >= 0.15f * p.Ruin) chunk.SetBlock(lx, p.FloorY + 1, lz, BlockType.Cobblestone);
            return;
        }
        chunk.SetBlock(lx, p.FloorY - 2, lz, BlockType.Cobblestone);
        chunk.SetBlock(lx, p.FloorY - 1, lz, BlockType.Water);
        chunk.SetBlock(lx, p.FloorY, lz, BlockType.Water);
    }

    private static void BuildWall(ChunkData chunk, Plan p, int lx, int lz, int wx, int wz, int ix, int iz, bool partition, int seed)
    {
        // Which wall, and how far along it (for the door and the windows)
        int along, length;
        int side;
        if (partition) { along = iz; length = p.Depth; side = -1; }
        else if (iz == 0) { along = ix; length = p.Width; side = 0; }
        else if (iz == p.Depth - 1) { along = ix; length = p.Width; side = 1; }
        else if (ix == 0) { along = iz; length = p.Depth; side = 2; }
        else { along = iz; length = p.Depth; side = 3; }

        bool corner = !partition && (ix == 0 || ix == p.Width - 1) && (iz == 0 || iz == p.Depth - 1);
        bool door = !corner && side == p.DoorSide && (along == length / 2 || (length >= 7 && along == length / 2 - 1));
        // The farmhouse's partition has a doorway of its own
        bool doorway = partition && (along == length / 2 || along == length / 2 + 1);
        bool window = !corner && !partition && !door && along % 3 == 1 && Rand(wx, wz, seed + 201) > 0.3f;

        for (int h = 1; h <= WallHeight; h++)
        {
            int y = p.FloorY + h;
            if (y >= ChunkData.Height) break;
            if ((door || doorway) && h <= 2) continue;
            if (window && h is 2 or 3) continue;
            // Broken: a block missing here and there, more toward the top and the more it has gone to ruin
            if (Rand(wx + h * 31, wz, seed + 202) < (0.05f + 0.16f * p.Ruin) * (h / (float)WallHeight + 0.4f)) continue;
            // A course of cobble at the foot of a farmhouse
            chunk.SetBlock(lx, y, lz, p.Kind == Kind.House && h == 1 ? BlockType.Cobblestone : BlockType.Planks);
        }
    }

    private static int FloorDiv(int a, int b) => (int)MathF.Floor(a / (float)b);
}
