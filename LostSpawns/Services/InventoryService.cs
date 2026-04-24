namespace LostSpawns.Services;

/// <summary>
/// A single inventory item instance. Intentionally minimal for MVP - once
/// PLAN-Survival-Needs / PLAN-Clothing-Storage land, this grows into a richer
/// record with weight, bulk, condition, stack cap, damage, etc.
/// </summary>
/// <summary>
/// High-level item categories. Drives HUD tinting so Water / Beans / Bandage are
/// visibly different at a glance in the inventory grid, without needing a full
/// sprite atlas. When real icons land, categories will still classify items for
/// sorting + filter UI.
/// </summary>
public enum ItemCategory
{
    /// <summary>Default / unknown. Light gray label.</summary>
    None,
    /// <summary>Solid food. Warm orange.</summary>
    Food,
    /// <summary>Drinkable. Cyan-blue.</summary>
    Drink,
    /// <summary>Medical (bandage, painkiller, etc). Red cross red.</summary>
    Medical,
    /// <summary>Tool or weapon. Muted gray.</summary>
    Tool,
    /// <summary>Raw material / crafting component. Sandy tan.</summary>
    Material,
    /// <summary>Marker item (map, flare). Warm yellow.</summary>
    Marker,
}

public record InventoryItem(string Id, string Name, int Count = 1, ItemCategory Category = ItemCategory.None, int? UsesRemaining = null);

/// <summary>
/// Payload carried by GameUIService.DragDropManager during an inventory drag.
/// FromHotbar=true means the drag started in a hotbar slot; false means backpack.
/// </summary>
public readonly record struct InventoryDragData(bool FromHotbar, int Index);

/// <summary>
/// What using an item restores. Deltas are normalized [0,1] and clamped at 1 by
/// PlayerStatsService setters, so piling up food past full just caps the bar.
/// DisplayVerb is shown in the "Used: X" toast (e.g. "Ate", "Drank", "Used").
/// </summary>
public readonly record struct ItemEffect(
    float Health = 0,
    float Hunger = 0,
    float Thirst = 0,
    float Stamina = 0,
    string DisplayVerb = "Used");

/// <summary>
/// Player inventory: 9-slot hotbar plus a backpack grid. Display-only for v1 -
/// drag/drop and move between containers come later. Hotbar items power the
/// on-screen quick-access bar; backpack items are only visible when the
/// inventory screen is open.
///
/// Slot convention: array index = display position. `null` means empty slot.
///
/// Firing OnInventoryChanged is a single notification surface; HudService listens
/// to refresh both the hotbar widget and the inventory grid when anything moves.
/// </summary>
public class InventoryService
{
    public const int HotbarSize = 9;
    public const int BackpackColumns = 8;
    public const int BackpackRows = 4;
    public const int BackpackSize = BackpackColumns * BackpackRows;

    private readonly PlayerStatsService _stats;

    public InventoryService(PlayerStatsService stats)
    {
        _stats = stats;
        // Starter load matches the pre-service hardcoded HUD: Axe on 1, Pick on 2, Bandage on 5, Map on 9.
        _hotbar[0] = new InventoryItem("tool.axe",  "Axe",  1, ItemCategory.Tool, UsesRemaining: 100);
        _hotbar[1] = new InventoryItem("tool.pick", "Pick", 1, ItemCategory.Tool, UsesRemaining: 100);
        _hotbar[4] = new InventoryItem("med.bandage",    "Bandage",   1, ItemCategory.Medical);
        _hotbar[8] = new InventoryItem("tool.map",       "Map",       1, ItemCategory.Marker);

        // Starter backpack so the inventory screen has something visible on first open.
        _backpack[0] = new InventoryItem("consume.water",    "Water",      2, ItemCategory.Drink);
        _backpack[1] = new InventoryItem("consume.beans",    "Beans",      3, ItemCategory.Food);
        _backpack[2] = new InventoryItem("material.cloth",   "Cloth",      5, ItemCategory.Material);
        _backpack[3] = new InventoryItem("material.rope",    "Rope",       1, ItemCategory.Material);
        _backpack[8] = new InventoryItem("tool.flare",       "Flare",      2, ItemCategory.Marker);
        _backpack[9] = new InventoryItem("med.painkiller",   "Painkiller", 1, ItemCategory.Medical);
    }

    private readonly InventoryItem?[] _hotbar = new InventoryItem?[HotbarSize];
    private readonly InventoryItem?[] _backpack = new InventoryItem?[BackpackSize];
    private int _activeHotbarIndex;

    /// <summary>
    /// Effect table keyed by item Id. Unknown items are not usable. Exposed so
    /// gameplay code can extend it (e.g. cooking adds "cooked.meat" at runtime).
    /// </summary>
    public Dictionary<string, ItemEffect> Effects { get; } = new()
    {
        ["consume.water"]    = new(Thirst: 0.30f, DisplayVerb: "Drank"),
        ["consume.beans"]    = new(Hunger: 0.30f, DisplayVerb: "Ate"),
        ["med.bandage"]      = new(Health: 0.30f, DisplayVerb: "Applied"),
        ["med.painkiller"]   = new(Health: 0.20f, Stamina: 0.15f, DisplayVerb: "Took"),
        // Raw meat: restores hunger but much less than cooked would. Slightly
        // penalizes health to motivate cooking once the campfire lands. Boar
        // yields a bigger piece than rabbit so its bonus is bigger too.
        ["food.rabbit_meat"] = new(Hunger: 0.20f, Health: -0.05f, DisplayVerb: "Ate raw"),
        ["food.boar_meat"]   = new(Hunger: 0.35f, Health: -0.05f, DisplayVerb: "Ate raw"),
        // Cooked meat (campfire output): bigger hunger bonus, small HP heal.
        ["food.rabbit_meat_cooked"] = new(Hunger: 0.35f, Health: 0.05f, DisplayVerb: "Ate"),
        ["food.boar_meat_cooked"]   = new(Hunger: 0.55f, Health: 0.10f, DisplayVerb: "Ate"),
    };

    /// <summary>Fired whenever any slot changes (set, clear, move).</summary>
    public event Action? OnInventoryChanged;

    /// <summary>Fired whenever the active (equipped) hotbar slot changes.</summary>
    public event Action<int>? OnActiveHotbarChanged;

    /// <summary>
    /// Currently equipped hotbar slot index [0, HotbarSize). The UIHotbar widget
    /// tracks its own selection too; call Set here (not directly on the widget)
    /// so gameplay code, the inventory screen, and the HUD all stay in sync.
    /// </summary>
    public int ActiveHotbarIndex
    {
        get => _activeHotbarIndex;
        set
        {
            int clamped = Math.Clamp(value, 0, HotbarSize - 1);
            if (clamped == _activeHotbarIndex) return;
            _activeHotbarIndex = clamped;
            OnActiveHotbarChanged?.Invoke(clamped);
        }
    }

    /// <summary>Shorthand for the item currently in the active hotbar slot, or null.</summary>
    public InventoryItem? ActiveItem => _hotbar[_activeHotbarIndex];

    /// <summary>Read-only view of the hotbar slots (length = HotbarSize).</summary>
    public IReadOnlyList<InventoryItem?> Hotbar => _hotbar;

    /// <summary>Read-only view of the backpack slots (length = BackpackSize).</summary>
    public IReadOnlyList<InventoryItem?> Backpack => _backpack;

    /// <summary>Fired when an item is successfully consumed. Args: item name, verb, effect applied.</summary>
    public event Action<string, string, ItemEffect>? OnItemConsumed;

    /// <summary>Fired when an item is added via TryAdd. Arg: the item added (Count always 1 for MVP drops).</summary>
    public event Action<InventoryItem>? OnItemPickedUp;

    /// <summary>
    /// Move the item between two slots. Source and target are encoded as
    /// (fromHotbar, index): fromHotbar==true means hotbar[index], false means backpack[index].
    /// If target already holds an item, the two are swapped. No-op if source is empty or
    /// indices are the same.
    /// </summary>
    public void MoveSlot(bool fromHotbar, int fromIndex, bool toHotbar, int toIndex)
    {
        if (fromHotbar == toHotbar && fromIndex == toIndex) return;

        var source = fromHotbar ? _hotbar : _backpack;
        var target = toHotbar ? _hotbar : _backpack;
        int sMax = fromHotbar ? HotbarSize : BackpackSize;
        int tMax = toHotbar ? HotbarSize : BackpackSize;
        if ((uint)fromIndex >= (uint)sMax || (uint)toIndex >= (uint)tMax) return;

        var moving = source[fromIndex];
        if (moving is null) return;

        var displaced = target[toIndex];
        target[toIndex] = moving;
        source[fromIndex] = displaced;
        OnInventoryChanged?.Invoke();
    }

    /// <summary>Get the hotbar item at an index, or null.</summary>
    public InventoryItem? GetHotbar(int index)
    {
        if ((uint)index >= (uint)HotbarSize) return null;
        return _hotbar[index];
    }

    /// <summary>Set the hotbar item at an index. Pass null to clear.</summary>
    public void SetHotbar(int index, InventoryItem? item)
    {
        if ((uint)index >= (uint)HotbarSize) return;
        if (_hotbar[index] == item) return;
        _hotbar[index] = item;
        OnInventoryChanged?.Invoke();
    }

    /// <summary>Get a backpack item at an index, or null.</summary>
    public InventoryItem? GetBackpack(int index)
    {
        if ((uint)index >= (uint)BackpackSize) return null;
        return _backpack[index];
    }

    /// <summary>Set a backpack item at an index. Pass null to clear.</summary>
    public void SetBackpack(int index, InventoryItem? item)
    {
        if ((uint)index >= (uint)BackpackSize) return;
        if (_backpack[index] == item) return;
        _backpack[index] = item;
        OnInventoryChanged?.Invoke();
    }

    /// <summary>Find the first empty slot anywhere (hotbar first, then backpack). Returns -1 if full.</summary>
    public int FindFirstEmptySlot()
    {
        for (int i = 0; i < HotbarSize; i++)
            if (_hotbar[i] is null) return i;
        for (int i = 0; i < BackpackSize; i++)
            if (_backpack[i] is null) return HotbarSize + i;
        return -1;
    }

    /// <summary>
    /// Try to use the item at the given slot. If the item's Id is in Effects, applies
    /// the effect to PlayerStats, decrements Count (clears slot at 0), fires
    /// OnItemConsumed, and returns true. Non-consumable items return false without
    /// touching state.
    /// </summary>
    public bool TryUseSlot(bool fromHotbar, int index)
    {
        var slots = fromHotbar ? _hotbar : _backpack;
        int max = fromHotbar ? HotbarSize : BackpackSize;
        if ((uint)index >= (uint)max) return false;
        var item = slots[index];
        if (item is null) return false;
        if (!Effects.TryGetValue(item.Id, out var effect)) return false;

        if (effect.Health > 0)      _stats.Heal(effect.Health);
        else if (effect.Health < 0) _stats.TakeDamage(-effect.Health);
        if (effect.Hunger  > 0) _stats.Hunger  = _stats.Hunger  + effect.Hunger;
        if (effect.Thirst  > 0) _stats.Thirst  = _stats.Thirst  + effect.Thirst;
        if (effect.Stamina > 0) _stats.Stamina = _stats.Stamina + effect.Stamina;

        // Decrement count; clear slot when exhausted.
        if (item.Count > 1)
            slots[index] = item with { Count = item.Count - 1 };
        else
            slots[index] = null;

        OnItemConsumed?.Invoke(item.Name, effect.DisplayVerb, effect);
        OnInventoryChanged?.Invoke();
        return true;
    }

    /// <summary>
    /// Try to place `item` in the first empty slot (hotbar first, then backpack).
    /// Returns true if placed, false if the inventory is completely full.
    /// </summary>
    public bool TryAdd(InventoryItem item)
    {
        int slot = FindFirstEmptySlot();
        if (slot < 0) return false;
        if (slot < HotbarSize) _hotbar[slot] = item;
        else _backpack[slot - HotbarSize] = item;
        OnInventoryChanged?.Invoke();
        OnItemPickedUp?.Invoke(item);
        return true;
    }

    /// <summary>
    /// Convert one stack-of-1 from the source item id into the target item.
    /// Used by the campfire cook tick: decrement the raw meat count in the
    /// first matching slot, then stack one of the cooked variant onto an
    /// existing stack or drop it into a free slot. Returns true only when
    /// the conversion actually happened (source found AND target fit). On
    /// failure the source count is preserved so the player doesn't lose
    /// food to a full inventory.
    /// </summary>
    public bool TryConvertOne(string fromId, InventoryItem cookedOne)
    {
        int sourceSlot = FindSlotById(fromId);
        if (sourceSlot < 0) return false;

        // Check up front that we can place the cooked item SOMEWHERE, else
        // roll back the conversion (don't burn raw food if we can't receive).
        int targetSlot = FindSlotById(cookedOne.Id);
        if (targetSlot < 0) targetSlot = FindFirstEmptySlot();
        if (targetSlot < 0) return false;

        // Decrement source.
        var srcArr = sourceSlot < HotbarSize ? _hotbar : _backpack;
        int srcIdx = sourceSlot < HotbarSize ? sourceSlot : sourceSlot - HotbarSize;
        var srcItem = srcArr[srcIdx]!;
        srcArr[srcIdx] = srcItem.Count > 1 ? srcItem with { Count = srcItem.Count - 1 } : null;

        // Increment / place target.
        var dstArr = targetSlot < HotbarSize ? _hotbar : _backpack;
        int dstIdx = targetSlot < HotbarSize ? targetSlot : targetSlot - HotbarSize;
        var dstItem = dstArr[dstIdx];
        if (dstItem is not null && dstItem.Id == cookedOne.Id)
            dstArr[dstIdx] = dstItem with { Count = dstItem.Count + 1 };
        else
            dstArr[dstIdx] = cookedOne;

        OnInventoryChanged?.Invoke();
        return true;
    }

    /// <summary>
    /// If the active hotbar item has a UsesRemaining counter, decrement by
    /// one and return (brokeNow, toolName). `brokeNow` means the tool just
    /// ran out and has been cleared from the slot; caller toasts accordingly.
    /// Items with null UsesRemaining (no durability) are ignored - hands +
    /// any non-tool item degrade nothing.
    /// </summary>
    public (bool BrokeNow, string? ToolName) DegradeActiveTool()
    {
        var item = ActiveItem;
        if (item is null || item.UsesRemaining is null) return (false, null);

        int next = item.UsesRemaining.Value - 1;
        if (next <= 0)
        {
            _hotbar[_activeHotbarIndex] = null;
            OnInventoryChanged?.Invoke();
            return (true, item.Name);
        }
        _hotbar[_activeHotbarIndex] = item with { UsesRemaining = next };
        OnInventoryChanged?.Invoke();
        return (false, item.Name);
    }

    private int FindSlotById(string id)
    {
        for (int i = 0; i < HotbarSize; i++)
            if (_hotbar[i]?.Id == id) return i;
        for (int i = 0; i < BackpackSize; i++)
            if (_backpack[i]?.Id == id) return HotbarSize + i;
        return -1;
    }
}
