using System.Numerics;
using LostSpawns.Rendering;

namespace LostSpawns.Services;

/// <summary>
/// The loot in the island's buildings. Each building (see <see cref="Structures"/>) is looked up as the player comes within range and its loot is
/// laid out on its floor, once: the ids of the buildings already looted are kept and saved, so what is taken stays taken.
/// </summary>
public sealed class PoiService
{
    /// <summary>How close the player must be to a building before its loot is laid out (blocks).</summary>
    public const float Range = 70f;

    private readonly WorldService _world;
    private readonly GroundItemService _ground;
    private readonly EntityService _wildlife;
    private readonly HashSet<string> _placed = new();
    private float _sinceCheck = 10f;
    private int _lastCellX = int.MinValue, _lastCellZ = int.MinValue;

    public PoiService(WorldService world, GroundItemService ground, EntityService wildlife)
    {
        _world = world;
        _ground = ground;
        _wildlife = wildlife;
    }

    /// <summary>The buildings within about 120 blocks of the player, refreshed every second (for the minimap).</summary>
    public IReadOnlyList<Structures.Plan> Nearby => _nearby;
    private List<Structures.Plan> _nearby = new();

    /// <summary>The buildings whose loot has been laid out (saved).</summary>
    public IReadOnlyCollection<string> Placed => _placed;

    public void Restore(IEnumerable<string>? ids)
    {
        _placed.Clear();
        if (ids != null) foreach (var id in ids) _placed.Add(id);
        _lastCellX = int.MinValue;
    }

    public void Tick(float dt, Vector3 playerPos)
    {
        _sinceCheck += dt;
        if (_sinceCheck < 1f) return;
        _sinceCheck = 0f;

        int pcx = (int)MathF.Floor(playerPos.X / Structures.CellSize), pcz = (int)MathF.Floor(playerPos.Z / Structures.CellSize);
        var near = new List<Structures.Plan>();
        for (int dz = -2; dz <= 2; dz++)
        for (int dx = -2; dx <= 2; dx++)
            if (_world.StructureAt(pcx + dx, pcz + dz) is { } np) near.Add(np);
        _nearby = near;
        // 3 x 3 cells around the player cover the range
        for (int dz = -1; dz <= 1; dz++)
        for (int dx = -1; dx <= 1; dx++)
        {
            int cx = pcx + dx, cz = pcz + dz;
            var plan = _world.StructureAt(cx, cz);
            if (plan is not { } p || _placed.Contains(p.Id)) continue;
            var c = p.Centre;
            float ddx = c.X - playerPos.X, ddz = c.Z - playerPos.Z;
            if (ddx * ddx + ddz * ddz > Range * Range) continue;
            Place(p, playerPos);
        }
    }

    private void Place(Structures.Plan plan, Vector3 playerPos)
    {
        _placed.Add(plan.Id);
        int seed = _world.Seed;
        var spots = Structures.LootSpots(plan, seed);
        for (int i = 0; i < spots.Count; i++)
        {
            float roll = TileNoise.Rand(plan.CellX * 31 + i, plan.CellZ * 17 + i, seed + 401);
            _ground.Drop(spots[i], Roll(roll, plan.Kind));
        }

        // The sick keep to the buildings: none in a shed, one in a cabin, one or two in a farmhouse; not on top of the player, and not too many at once
        int infected = plan.Kind switch { Structures.Kind.Shed => 0, Structures.Kind.Cabin => 1, _ => 1 + (int)(TileNoise.Rand(plan.CellX, plan.CellZ, seed + 402) * 2f) };
        var c = plan.Centre;
        float dx = c.X - playerPos.X, dz = c.Z - playerPos.Z;
        if (dx * dx + dz * dz < 25f * 25f) return;
        for (int i = 0; i < infected && _wildlife.InfectedCount < 12; i++)
        {
            var at = new Vector3(c.X + (i == 0 ? 0f : 1.5f), c.Y, c.Z);
            var e = _wildlife.Spawn(EntityKind.Infected, at, _world);
            e.Position = new Vector3(e.Position.X, plan.FloorY + 1f, e.Position.Z);
        }
    }

    // Poorer in a shed, better in a house
    private static InventoryItem Roll(float r, Structures.Kind kind)
    {
        if (kind == Structures.Kind.House) r = r * 0.8f + 0.2f * (1f - r) * 0.5f;
        return r switch
        {
            < 0.20f => new InventoryItem("consume.can_beans", "Canned Beans", 1, ItemCategory.Food),
            < 0.36f => new InventoryItem("consume.can_peaches", "Canned Peaches", 1, ItemCategory.Food),
            < 0.50f => new InventoryItem("consume.water", "Water", 1, ItemCategory.Drink),
            < 0.62f => new InventoryItem("med.bandage", "Bandage", 2, ItemCategory.Medical),
            < 0.70f => new InventoryItem("med.painkiller", "Painkiller", 1, ItemCategory.Medical),
            < 0.80f => new InventoryItem("material.cloth", "Cloth", 2, ItemCategory.Material),
            < 0.90f => new InventoryItem("material.rope", "Rope", 1, ItemCategory.Material),
            _ => new InventoryItem("material.wood", "Wood", 3, ItemCategory.Material),
        };
    }
}
