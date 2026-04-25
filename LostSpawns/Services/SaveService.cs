using System.Text.Json;
using System.Text.Json.Serialization;
using SpawnDev.BlazorJS;
using SpawnDev.BlazorJS.JSObjects;

namespace LostSpawns.Services;

/// <summary>
/// Persists non-world game state (player stats, inventory, position, clock,
/// weather) to localStorage between sessions. Writes a versioned JSON blob;
/// future schema changes bump SaveVersion and older saves fall through to a
/// fresh-game start instead of crashing.
///
/// World state (placed + broken blocks) is NOT saved yet - chopping down a
/// tree, reloading, and finding the tree intact is the known MVP gap. A diff
/// layer goes in once we have persistence infrastructure that can handle the
/// volume.
///
/// Game.razor auto-saves every AutoSaveIntervalSeconds during active play
/// (same gameplay gate as the survival tick) and calls TryLoad once after
/// the initial chunk generation returns.
/// </summary>
public class SaveService
{
    public const int SaveVersion = 2;
    public const string SaveKey = "lost.save";
    public const float AutoSaveIntervalSeconds = 10f;

    private readonly BlazorJSRuntime _js;
    private readonly PlayerStatsService _stats;
    private readonly InventoryService _inventory;
    private readonly WorldTimeService _worldTime;
    private readonly WorldService _world;
    private readonly CampfireService _fires;
    private readonly GroundItemService _ground;

    private static readonly JsonSerializerOptions _json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public SaveService(BlazorJSRuntime js, PlayerStatsService stats, InventoryService inventory, WorldTimeService worldTime, WorldService world, CampfireService fires, GroundItemService ground)
    {
        _js = js;
        _stats = stats;
        _inventory = inventory;
        _worldTime = worldTime;
        _world = world;
        _fires = fires;
        _ground = ground;
    }

    /// <summary>
    /// Snapshot the full state to localStorage. Silent-fails on serialization or
    /// storage error so a transient hiccup doesn't crash the gameplay loop.
    /// </summary>
    /// <summary>Fires after a successful SaveNow write so HUD can show a brief "Saved" toast.</summary>
    public event Action? OnSaved;

    public void SaveNow(System.Numerics.Vector3 cameraPos, float yaw, float pitch)
    {
        try
        {
            var state = new SaveState
            {
                Version = SaveVersion,
                PosX = cameraPos.X,
                PosY = cameraPos.Y,
                PosZ = cameraPos.Z,
                Yaw = yaw,
                Pitch = pitch,
                Health = _stats.Health,
                Stamina = _stats.Stamina,
                Hunger = _stats.Hunger,
                Thirst = _stats.Thirst,
                Temperature = _stats.Temperature,
                Experience = _stats.Experience,
                Kills = _stats.Kills,
                RabbitKills = _stats.RabbitKills,
                BoarKills = _stats.BoarKills,
                CrowKills = _stats.CrowKills,
                WolfKills = _stats.WolfKills,
                FirstKillAwarded = _stats.FirstKillAwarded,
                FirstFireAwarded = _stats.FirstFireAwarded,
                FirstCookAwarded = _stats.FirstCookAwarded,
                FirstWolfAwarded = _stats.FirstWolfAwarded,
                FirstSleepAwarded = _stats.FirstSleepAwarded,
                VeteranAwarded = _stats.VeteranAwarded,
                CenturionAwarded = _stats.CenturionAwarded,
                SurvivorAwarded = _stats.SurvivorAwarded,
                BowmanAwarded = _stats.BowmanAwarded,
                CompletionistAwarded = _stats.CompletionistAwarded,
                PlayTimeSeconds = _stats.PlayTimeSeconds,
                Hotbar = ToDtoArray(_inventory.Hotbar),
                Backpack = ToDtoArray(_inventory.Backpack),
                ActiveHotbarIndex = _inventory.ActiveHotbarIndex,
                DayFraction = _worldTime.DayFraction,
                DayNumber = _worldTime.DayNumber,
                WorldEdits = _world.GetEditsSnapshot(),
                Campfires = _fires.Fires.Select(f => new CampfireDto
                {
                    X = f.Position.X,
                    Y = f.Position.Y,
                    Z = f.Position.Z,
                    Radius = f.Radius,
                    Intensity = f.Intensity,
                    Fuel = f.Fuel,
                }).ToArray(),
                GroundItems = _ground.Items.Select(g => new GroundItemDto
                {
                    X = g.Position.X,
                    Y = g.Position.Y,
                    Z = g.Position.Z,
                    Item = new InventoryItemDto
                    {
                        Id = g.Payload.Id,
                        Name = g.Payload.Name,
                        Count = g.Payload.Count,
                        Category = (int)g.Payload.Category,
                        UsesRemaining = g.Payload.UsesRemaining,
                    },
                }).ToArray(),
            };
            string json = JsonSerializer.Serialize(state, _json);
            using var storage = _js.Get<Storage>("localStorage");
            storage.SetItem(SaveKey, json);
            OnSaved?.Invoke();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Save] serialize failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Load and apply saved state if present and schema version matches.
    /// Returns (posX, posY, posZ, yaw, pitch) when a save was applied so the
    /// caller can teleport the camera; returns null otherwise. Stats + inventory
    /// + world time are mutated in-place via their services.
    /// </summary>
    public (System.Numerics.Vector3 Position, float Yaw, float Pitch)? TryLoad()
    {
        try
        {
            using var storage = _js.Get<Storage>("localStorage");
            var raw = storage.GetItem(SaveKey);
            if (string.IsNullOrEmpty(raw)) return null;

            var state = JsonSerializer.Deserialize<SaveState>(raw, _json);
            if (state is null || state.Version != SaveVersion) return null;

            // Apply stats directly (setters clamp to [0,1]).
            _stats.Health = state.Health;
            _stats.Stamina = state.Stamina;
            _stats.Hunger = state.Hunger;
            _stats.Thirst = state.Thirst;
            _stats.Temperature = state.Temperature;
            if (state.Experience > _stats.Experience)
                _stats.AwardXp(state.Experience - _stats.Experience);
            // Seed kills + achievement flag directly so the FirstKill event
            // doesn't fire on every save-reload.
            _stats.SeedKillsFromSave(state.Kills, state.FirstKillAwarded);
            _stats.SeedKindKillsFromSave(
                state.RabbitKills, state.BoarKills, state.CrowKills, state.WolfKills);
            _stats.SeedAchievementsFromSave(
                state.FirstFireAwarded, state.FirstCookAwarded,
                state.FirstWolfAwarded, state.FirstSleepAwarded,
                state.VeteranAwarded, state.CenturionAwarded,
                state.SurvivorAwarded, state.BowmanAwarded,
                state.CompletionistAwarded);
            _stats.SeedPlayTimeFromSave(state.PlayTimeSeconds);

            // Apply inventory slot by slot. Nulls stay null.
            if (state.Hotbar != null)
            {
                for (int i = 0; i < Math.Min(state.Hotbar.Length, InventoryService.HotbarSize); i++)
                    _inventory.SetHotbar(i, FromDto(state.Hotbar[i]));
            }
            if (state.Backpack != null)
            {
                for (int i = 0; i < Math.Min(state.Backpack.Length, InventoryService.BackpackSize); i++)
                    _inventory.SetBackpack(i, FromDto(state.Backpack[i]));
            }
            _inventory.ActiveHotbarIndex = state.ActiveHotbarIndex;

            // WorldTime has no settable DayFraction; we nudge it by ticking so the
            // rest of the exposed derived values (PhaseName, Colors) follow. A
            // fresh setter would be cleaner; for now fast-forward from 0.
            // Simpler: just overwrite via reflection? No - add a setter. See
            // note in WorldTimeService.
            _worldTime.SetDayFraction(state.DayFraction);
            _worldTime.SetDayNumber(state.DayNumber > 0 ? state.DayNumber : 1);

            // Apply world edits ONTO already-loaded chunks; columns not yet cached
            // will get the overlay when they later generate (see
            // WorldService.GetOrGenerateBlocksAsync). Re-mesh the columns that
            // are loaded now so the visual matches the data immediately.
            if (state.WorldEdits != null && state.WorldEdits.Count > 0)
            {
                var touched = _world.ApplyEdits(state.WorldEdits);
                foreach (var (cx, cz) in touched)
                    _ = _world.ReMeshColumn(cx, cz);
            }

            // Campfires: wipe the current list (starter fire the service spawned
            // on load) and repopulate from the save so fuel / position are exact.
            // Game.razor's starter-fire spawn only runs when Fires.Count == 0, so
            // restoring here prevents a duplicate.
            if (state.Campfires != null)
            {
                _fires.Fires.Clear();
                foreach (var dto in state.Campfires)
                {
                    var f = _fires.Spawn(new System.Numerics.Vector3(dto.X, dto.Y, dto.Z), dto.Radius);
                    f.Intensity = dto.Intensity;
                    f.Fuel = dto.Fuel;
                }
            }

            // Restore ground loot so an interrupted cleanup resumes where the
            // player left it - walking to the bag from last session still
            // collects what was dropped.
            if (state.GroundItems != null)
            {
                _ground.Items.Clear();
                foreach (var dto in state.GroundItems)
                {
                    var payload = FromDto(dto.Item);
                    if (payload is null) continue;
                    _ground.Drop(new System.Numerics.Vector3(dto.X, dto.Y, dto.Z), payload);
                }
            }

            var pos = new System.Numerics.Vector3(state.PosX, state.PosY, state.PosZ);
            return (pos, state.Yaw, state.Pitch);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Save] deserialize failed: {ex.Message}");
            return null;
        }
    }

    /// <summary>Wipe the save. Used by "New Game" flows once that UI exists.</summary>
    public void Clear()
    {
        try
        {
            using var storage = _js.Get<Storage>("localStorage");
            storage.RemoveItem(SaveKey);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Save] clear failed: {ex.Message}");
        }
    }

    private static InventoryItemDto?[] ToDtoArray(IReadOnlyList<InventoryItem?> items)
    {
        var result = new InventoryItemDto?[items.Count];
        for (int i = 0; i < items.Count; i++)
        {
            var it = items[i];
            result[i] = it is null ? null : new InventoryItemDto
            {
                Id = it.Id,
                Name = it.Name,
                Count = it.Count,
                Category = (int)it.Category,
                UsesRemaining = it.UsesRemaining,
            };
        }
        return result;
    }

    private static InventoryItem? FromDto(InventoryItemDto? dto)
    {
        if (dto is null) return null;
        return new InventoryItem(
            dto.Id ?? "",
            dto.Name ?? "",
            dto.Count,
            (ItemCategory)dto.Category,
            dto.UsesRemaining);
    }

    public sealed class SaveState
    {
        public int Version { get; set; }
        public float PosX { get; set; }
        public float PosY { get; set; }
        public float PosZ { get; set; }
        public float Yaw { get; set; }
        public float Pitch { get; set; }
        public float Health { get; set; }
        public float Stamina { get; set; }
        public float Hunger { get; set; }
        public float Thirst { get; set; }
        public float Temperature { get; set; }
        public int Experience { get; set; }
        public int Kills { get; set; }
        public int RabbitKills { get; set; }
        public int BoarKills { get; set; }
        public int CrowKills { get; set; }
        public int WolfKills { get; set; }
        public bool FirstKillAwarded { get; set; }
        public bool FirstFireAwarded { get; set; }
        public bool FirstCookAwarded { get; set; }
        public bool FirstWolfAwarded { get; set; }
        public bool FirstSleepAwarded { get; set; }
        public bool VeteranAwarded { get; set; }
        public bool CenturionAwarded { get; set; }
        public bool SurvivorAwarded { get; set; }
        public bool BowmanAwarded { get; set; }
        public bool CompletionistAwarded { get; set; }
        public float PlayTimeSeconds { get; set; }
        public InventoryItemDto?[]? Hotbar { get; set; }
        public InventoryItemDto?[]? Backpack { get; set; }
        public int ActiveHotbarIndex { get; set; }
        public float DayFraction { get; set; }
        public int DayNumber { get; set; }
        /// <summary>Sparse block edits per chunk. Key = "cx,cz"; inner dict = byte-index -> new block byte.</summary>
        public Dictionary<string, Dictionary<int, byte>>? WorldEdits { get; set; }
        public CampfireDto[]? Campfires { get; set; }
        public GroundItemDto[]? GroundItems { get; set; }
    }

    public sealed class InventoryItemDto
    {
        public string? Id { get; set; }
        public string? Name { get; set; }
        public int Count { get; set; }
        public int Category { get; set; }
        public int? UsesRemaining { get; set; }
    }

    public sealed class CampfireDto
    {
        public float X { get; set; }
        public float Y { get; set; }
        public float Z { get; set; }
        public float Radius { get; set; }
        public float Intensity { get; set; }
        public float Fuel { get; set; }
    }

    public sealed class GroundItemDto
    {
        public float X { get; set; }
        public float Y { get; set; }
        public float Z { get; set; }
        public InventoryItemDto? Item { get; set; }
    }
}
