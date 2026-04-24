using System.Numerics;

namespace LostSpawns.Services;

/// <summary>
/// World-space campfire. Not a voxel block - kept as a separate entity so we
/// don't have to plumb a new BlockType through the mesh pipeline just to put
/// a fire on the ground. Position is world coords (voxel space), Radius is
/// how far the warmth aura reaches, Intensity scales particle density + the
/// strength of the warmth bonus.
/// </summary>
public sealed class Campfire
{
    public int Id { get; init; }
    public Vector3 Position;
    public float Radius = 6f;
    public float Intensity = 1f;
}

/// <summary>
/// Tracks active campfires in the world and exposes warmth + lookup helpers.
/// HudService renders them as billboards; PlayerStatsService consults the
/// warmth bonus each tick; Game.razor spawns a starter fire near the player
/// spawn point.
///
/// Scope for this pass: one fire at a fixed world position, immutable
/// lifetime. Follow-ups: place-at-cursor via inventory item, cook interaction,
/// fuel consumption that puts the fire out.
/// </summary>
public class CampfireService
{
    private int _nextId = 1;
    public List<Campfire> Fires { get; } = new();

    /// <summary>Seconds of continuous proximity required to cook one raw meat.</summary>
    public float CookSeconds { get; set; } = 5f;

    /// <summary>Current cook-timer progress, only advanced while the player is in range of any fire.</summary>
    private float _cookProgress;

    /// <summary>Fired each time one raw item is converted to its cooked variant.</summary>
    public event Action<string>? OnCooked; // payload = cooked item display name

    public Campfire Spawn(Vector3 position, float radius = 6f)
    {
        var f = new Campfire
        {
            Id = _nextId++,
            Position = position,
            Radius = radius,
        };
        Fires.Add(f);
        return f;
    }

    /// <summary>
    /// Warmth bonus applied to the PlayerStats target temperature this tick.
    /// Falls off linearly with distance so the effect feels physical - stand
    /// too close and you cook, stand too far and you freeze. Returns 0 when
    /// the player isn't in range of any fire so callers can short-circuit.
    /// </summary>
    public float GetWarmthBonusAt(Vector3 playerPos)
    {
        float bonus = 0f;
        foreach (var f in Fires)
        {
            float dx = playerPos.X - f.Position.X;
            float dz = playerPos.Z - f.Position.Z;
            float distSq = dx * dx + dz * dz;
            float r = f.Radius;
            if (distSq > r * r) continue;

            // 1.0 at center, 0 at radius. Multiplied by intensity so low-fuel
            // fires warm less.
            float t = 1f - MathF.Sqrt(distSq) / r;
            // Max bonus 0.5 (matches ambient temp range so a close fire brings
            // a frozen player back to comfortable).
            bonus = MathF.Max(bonus, 0.5f * t * f.Intensity);
        }
        return bonus;
    }

    /// <summary>
    /// Per-frame cook tick. While the player is within any fire's radius,
    /// advance the cook progress and - on reaching CookSeconds - convert one
    /// raw meat to its cooked variant. Cooking prefers rabbit (lower tier)
    /// first so the player always moves up the food chain. Fires OnCooked
    /// with the display name so the HUD can toast it.
    ///
    /// Timer resets on a successful cook AND on losing proximity, so
    /// walking away mid-cook doesn't bank progress.
    /// </summary>
    public void Tick(float dt, Vector3 playerPos, InventoryService inventory)
    {
        if (GetWarmthBonusAt(playerPos) <= 0f)
        {
            _cookProgress = 0;
            return;
        }

        _cookProgress += dt;
        if (_cookProgress < CookSeconds) return;
        _cookProgress = 0;

        // Try rabbit first, fall through to boar. Cooked item is stack-of-1;
        // InventoryService.TryConvertOne stacks onto an existing cooked slot
        // if one exists.
        if (inventory.TryConvertOne("food.rabbit_meat",
            new InventoryItem("food.rabbit_meat_cooked", "Rabbit Meat (Cooked)", 1, ItemCategory.Food)))
        {
            OnCooked?.Invoke("Rabbit Meat (Cooked)");
            return;
        }
        if (inventory.TryConvertOne("food.boar_meat",
            new InventoryItem("food.boar_meat_cooked", "Boar Meat (Cooked)", 1, ItemCategory.Food)))
        {
            OnCooked?.Invoke("Boar Meat (Cooked)");
        }
    }
}
