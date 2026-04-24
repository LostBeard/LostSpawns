namespace LostSpawns.Services;

/// <summary>
/// One crafting recipe: a list of required inputs (item Id + count) and
/// the output item produced. Display name shown on the craft button; the
/// raw recipe lines (ingredient counts + output name) are derived from
/// Inputs + Output when building the HUD row.
/// </summary>
public sealed record CraftingRecipe(
    string DisplayName,
    (string Id, int Count)[] Inputs,
    InventoryItem Output);

/// <summary>
/// Static recipe registry + craft-attempt helper. Kept tiny for MVP - a real
/// recipe system would live in content files with icons, categories, skill
/// gates, cook stations, etc. This is the minimum needed to turn gathered
/// materials into new usable items.
///
/// Pure DI singleton, no state: recipes are a readonly list, TryCraft only
/// touches InventoryService and relies on its OnInventoryChanged to refresh
/// the UI.
/// </summary>
public class CraftingService
{
    private readonly InventoryService _inventory;

    /// <summary>Fired after a successful craft. Arg = the recipe that produced it.</summary>
    public event Action<CraftingRecipe>? OnCrafted;

    public IReadOnlyList<CraftingRecipe> Recipes { get; }

    public CraftingService(InventoryService inventory)
    {
        _inventory = inventory;
        Recipes = new[]
        {
            new CraftingRecipe(
                "Torch",
                new[] { ("material.wood", 2), ("material.cloth", 1) },
                new InventoryItem("craft.torch", "Torch", 1, ItemCategory.Marker)),
            new CraftingRecipe(
                "Stew",
                new[] { ("consume.beans", 1), ("consume.water", 1) },
                new InventoryItem("craft.stew", "Stew", 1, ItemCategory.Food)),
            new CraftingRecipe(
                "Bandage",
                new[] { ("material.cloth", 1), ("material.rope", 1) },
                new InventoryItem("med.bandage", "Bandage", 1, ItemCategory.Medical)),
            new CraftingRecipe(
                "Campfire",
                new[] { ("material.wood", 4), ("material.leaves", 2) },
                new InventoryItem("place.campfire", "Campfire", 1, ItemCategory.Marker)),
        };

        // Register effects for any new outputs the crafting system introduces.
        // Bandage already has an entry from InventoryService's starter table;
        // Torch and Stew land here because they're crafting-exclusive today.
        _inventory.Effects["craft.torch"] = new ItemEffect(Stamina: 0.08f, DisplayVerb: "Lit");
        _inventory.Effects["craft.stew"]  = new ItemEffect(Hunger: 0.45f, Thirst: 0.45f, DisplayVerb: "Ate");
    }

    /// <summary>True if every input of the recipe is present in sufficient count.</summary>
    public bool CanCraft(CraftingRecipe recipe)
    {
        foreach (var (id, count) in recipe.Inputs)
            if (CountById(id) < count) return false;
        return true;
    }

    /// <summary>
    /// Try to consume the recipe's inputs and add its output. Returns true on
    /// success, false if any input was short OR the resulting item wouldn't
    /// fit in the inventory.
    /// </summary>
    public bool TryCraft(CraftingRecipe recipe)
    {
        if (!CanCraft(recipe)) return false;
        if (_inventory.FindFirstEmptySlot() < 0) return false;

        // Consume inputs in order. CountById -> Deduct keeps stacks balanced
        // across both hotbar and backpack transparently.
        foreach (var (id, needed) in recipe.Inputs)
            Deduct(id, needed);

        // Add a fresh copy of the output (new record, so Count is not shared).
        _inventory.TryAdd(recipe.Output with { });
        OnCrafted?.Invoke(recipe);
        return true;
    }

    private int CountById(string id)
    {
        int total = 0;
        for (int i = 0; i < InventoryService.HotbarSize; i++)
            if (_inventory.Hotbar[i]?.Id == id) total += _inventory.Hotbar[i]!.Count;
        for (int i = 0; i < InventoryService.BackpackSize; i++)
            if (_inventory.Backpack[i]?.Id == id) total += _inventory.Backpack[i]!.Count;
        return total;
    }

    private void Deduct(string id, int count)
    {
        // Drain hotbar slots first so the player sees their equipped materials
        // go first; then fall through to backpack stacks.
        for (int i = 0; i < InventoryService.HotbarSize && count > 0; i++)
        {
            var it = _inventory.Hotbar[i];
            if (it?.Id != id) continue;
            int take = Math.Min(count, it.Count);
            count -= take;
            _inventory.SetHotbar(i, take >= it.Count ? null : it with { Count = it.Count - take });
        }
        for (int i = 0; i < InventoryService.BackpackSize && count > 0; i++)
        {
            var it = _inventory.Backpack[i];
            if (it?.Id != id) continue;
            int take = Math.Min(count, it.Count);
            count -= take;
            _inventory.SetBackpack(i, take >= it.Count ? null : it with { Count = it.Count - take });
        }
    }
}
