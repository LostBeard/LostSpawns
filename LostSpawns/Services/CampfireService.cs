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
    /// <summary>Fuel level [0, 1]. Fire extinguishes at 0; feeding pushes it up to 1.</summary>
    public float Fuel = 1f;
    /// <summary>True after low-fuel warning fired so subsequent frames don't re-toast.</summary>
    internal bool LowFuelWarned;
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

    /// <summary>How long a full-fuel fire burns before going out (seconds).</summary>
    public float FuelLifetimeSeconds { get; set; } = 600f;

    /// <summary>How close the player has to be to feed a fire (blocks).</summary>
    public float InteractRange { get; set; } = 3.5f;

    /// <summary>Fuel value of one unit of wood / leaves when fed to a fire.</summary>
    public float WoodFuel { get; set; } = 0.40f;
    public float LeavesFuel { get; set; } = 0.15f;

    /// <summary>Current cook-timer progress, only advanced while the player is in range of any fire.</summary>
    private float _cookProgress;

    /// <summary>Exposes the current cook progress ratio [0,1] for HUD rendering. Returns 0 when not in range or no raw meat present.</summary>
    public float CookProgressRatio => _cookProgress / MathF.Max(0.001f, CookSeconds);

    /// <summary>Fired each time one raw item is converted to its cooked variant.</summary>
    public event Action<string>? OnCooked; // payload = cooked item display name

    /// <summary>Fires once when a campfire's fuel dips below 0.15 and hasn't warned yet.</summary>
    public event Action<Campfire>? OnLowFuel;

    /// <summary>Fired when a fire's fuel transitions from > 0 to 0 - the moment it goes out.</summary>
    public event Action<Campfire>? OnExtinguished;

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
            if (f.Fuel <= 0f) continue; // extinguished fires produce no warmth
            float dx = playerPos.X - f.Position.X;
            float dz = playerPos.Z - f.Position.Z;
            float distSq = dx * dx + dz * dz;
            float r = f.Radius;
            if (distSq > r * r) continue;

            // 1.0 at center, 0 at radius. Multiplied by fuel so a nearly-out
            // fire warms less than a freshly-fed one.
            float t = 1f - MathF.Sqrt(distSq) / r;
            bonus = MathF.Max(bonus, 0.5f * t * f.Intensity * f.Fuel);
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
    /// <summary>
    /// Multiplier applied to fuel decay this tick. Game.razor sets this to
    /// ~2.5 during heavy rain so open fires burn out visibly faster in bad
    /// weather. Reset by the caller each frame (no hysteresis in-service).
    /// </summary>
    public float FuelDecayMultiplier { get; set; } = 1f;

    public void Tick(float dt, Vector3 playerPos, InventoryService inventory)
    {
        // Fuel decay runs regardless of player proximity - fires burn even
        // when you walk away. Extinguished fires (Fuel <= 0) skip cook and
        // warmth but stay in the list so the player can re-feed them.
        // FuelDecayMultiplier from callers (e.g. rain) scales the rate.
        float decay = dt / FuelLifetimeSeconds * FuelDecayMultiplier;
        foreach (var f in Fires)
        {
            float prevFuel = f.Fuel;
            f.Fuel = MathF.Max(0f, f.Fuel - decay);
            // Extinguish detection: caught the moment fuel drops from > 0
            // to 0 so the consumer (Game.razor) can play a sizzle sound +
            // toast. Re-arming on re-feed handled in the >0.5 branch below
            // so a fed-back-to-life fire can fire OnExtinguished again.
            if (prevFuel > 0f && f.Fuel == 0f)
                OnExtinguished?.Invoke(f);
            if (f.Fuel < 0.15f && !f.LowFuelWarned)
            {
                f.LowFuelWarned = true;
                OnLowFuel?.Invoke(f);
            }
            // Re-feeding past the threshold re-arms the warning for the
            // next time fuel drops - players who keep feeding get fresh
            // warnings on each cycle.
            if (f.Fuel > 0.5f) f.LowFuelWarned = false;
        }

        if (GetWarmthBonusAt(playerPos) <= 0f)
        {
            _cookProgress = 0;
            return;
        }

        // Bigger fires cook faster. Bonfires (radius 12) get 2x the cook
        // rate vs a campfire (radius 6); torches (2.5) cook at half rate.
        // Find the nearest fire whose aura the player is in and scale by
        // its radius. Linear scale to keep it predictable.
        var nearest = FindNearest(playerPos);
        float cookSpeed = 1f;
        if (nearest is not null)
        {
            float dx2 = nearest.Position.X - playerPos.X;
            float dz2 = nearest.Position.Z - playerPos.Z;
            if (dx2 * dx2 + dz2 * dz2 <= nearest.Radius * nearest.Radius)
                cookSpeed = nearest.Radius / 6f; // 6 = baseline campfire radius
        }
        _cookProgress += dt * cookSpeed;
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
            return;
        }
        if (inventory.TryConvertOne("food.deer_meat",
            new InventoryItem("food.deer_meat_cooked", "Deer Meat (Cooked)", 1, ItemCategory.Food)))
        {
            OnCooked?.Invoke("Deer Meat (Cooked)");
        }
    }

    /// <summary>
    /// Fired when the player successfully feeds a fire. Payload is the item
    /// display name consumed ("Wood" / "Leaves") so the HUD can toast it.
    /// </summary>
    public event Action<string>? OnFireFed;

    /// <summary>
    /// Find the closest fire within InteractRange of playerPos. Returns null
    /// if none - callers use that to gate interaction prompts + hotkeys.
    /// </summary>
    public Campfire? FindNearest(Vector3 playerPos)
    {
        Campfire? best = null;
        float bestDistSq = float.PositiveInfinity;
        foreach (var f in Fires)
        {
            float dx = playerPos.X - f.Position.X;
            float dz = playerPos.Z - f.Position.Z;
            float d = dx * dx + dz * dz;
            if (d < bestDistSq && d <= InteractRange * InteractRange)
            {
                bestDistSq = d;
                best = f;
            }
        }
        return best;
    }

    /// <summary>
    /// Try to feed the nearest-in-range fire with a unit of fuel from the
    /// active hotbar slot. Wood counts for more fuel than leaves; anything
    /// else fails silently so the player's axe doesn't get eaten. Returns
    /// true on a successful feed; caller toasts on false for "No fire near"
    /// vs "Hold wood/leaves" feedback.
    /// </summary>
    public bool TryFeedNearest(Vector3 playerPos, InventoryService inventory, out string reason)
    {
        reason = "";
        var fire = FindNearest(playerPos);
        if (fire is null) { reason = "No fire in range"; return false; }

        var active = inventory.ActiveItem;
        if (active is null) { reason = "Hold Wood or Leaves"; return false; }

        float gain;
        string fuelName;
        if (active.Id == "material.wood") { gain = WoodFuel; fuelName = "Wood"; }
        else if (active.Id == "material.leaves") { gain = LeavesFuel; fuelName = "Leaves"; }
        else { reason = "Hold Wood or Leaves"; return false; }

        // Decrement one unit of the active fuel item. Going through
        // SetHotbar keeps OnInventoryChanged firing so the UI repaints.
        int slot = inventory.ActiveHotbarIndex;
        var next = active.Count > 1 ? active with { Count = active.Count - 1 } : null;
        inventory.SetHotbar(slot, next);

        fire.Fuel = MathF.Min(1f, fire.Fuel + gain);
        OnFireFed?.Invoke(fuelName);
        return true;
    }
}
