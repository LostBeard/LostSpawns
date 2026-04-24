namespace LostSpawns.Services;

/// <summary>
/// A single inventory item instance. Intentionally minimal for MVP - once
/// PLAN-Survival-Needs / PLAN-Clothing-Storage land, this grows into a richer
/// record with weight, bulk, condition, stack cap, damage, etc.
/// </summary>
public record InventoryItem(string Id, string Name, int Count = 1);

/// <summary>
/// Payload carried by GameUIService.DragDropManager during an inventory drag.
/// FromHotbar=true means the drag started in a hotbar slot; false means backpack.
/// </summary>
public readonly record struct InventoryDragData(bool FromHotbar, int Index);

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

    private readonly InventoryItem?[] _hotbar = new InventoryItem?[HotbarSize];
    private readonly InventoryItem?[] _backpack = new InventoryItem?[BackpackSize];

    /// <summary>Fired whenever any slot changes (set, clear, move).</summary>
    public event Action? OnInventoryChanged;

    /// <summary>Read-only view of the hotbar slots (length = HotbarSize).</summary>
    public IReadOnlyList<InventoryItem?> Hotbar => _hotbar;

    /// <summary>Read-only view of the backpack slots (length = BackpackSize).</summary>
    public IReadOnlyList<InventoryItem?> Backpack => _backpack;

    public InventoryService()
    {
        // Starter load matches the pre-service hardcoded HUD: Axe on 1, Pick on 2, Bandage on 5, Map on 9.
        // Index 0 is key "1" on screen; UIHotbar labels slots 1..9 left to right.
        _hotbar[0] = new InventoryItem("tool.axe", "Axe");
        _hotbar[1] = new InventoryItem("tool.pick", "Pick");
        _hotbar[4] = new InventoryItem("med.bandage", "Bandage");
        _hotbar[8] = new InventoryItem("tool.map", "Map");

        // Starter backpack so the inventory screen has something visible on first open.
        // Gives the player a reason to open inventory + immediate drag-drop targets.
        _backpack[0] = new InventoryItem("consume.water", "Water", 2);
        _backpack[1] = new InventoryItem("consume.beans", "Beans", 3);
        _backpack[2] = new InventoryItem("material.cloth", "Cloth", 5);
        _backpack[3] = new InventoryItem("material.rope", "Rope");
        _backpack[8] = new InventoryItem("tool.flare", "Flare", 2);
        _backpack[9] = new InventoryItem("med.painkiller", "Painkiller");
    }

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
        return true;
    }
}
