using System.Numerics;

namespace LostSpawns.Rendering;

/// <summary>
/// Things that are not creatures, built from the same boxes: the campfire and the torch, loot lying on the ground, and the tools the
/// player holds. Same model space as <see cref="CreatureModels"/> (pixels, y down, feet at y = 24 for what stands on the ground);
/// the held tools are drawn from their grip, see <see cref="HeldModels"/>.
/// </summary>
public static class PropModels
{
    private static ModelPart P(string name, PartLook look, float ox, float oy, float oz, float w, float h, float d,
        Vector3? pivot = null, Vector3 baseRot = default, int parent = -1) =>
        new() { Name = name, Look = look, Origin = new Vector3(ox, oy, oz), Size = new Vector3(w, h, d), Pivot = pivot, BaseRotation = baseRot, Parent = parent };

    private static readonly FurPalette None = new(Vector3.One, Vector3.One, Vector3.One, Vector3.One, 0.1f);

    /// <summary>A ring of stones, four crossed logs and a bed of coals. The flames and the smoke are particles.</summary>
    public static readonly CreatureModel Campfire = new()
    {
        Name = "campfire", Palette = None,
        Parts = BuildCampfire(),
    };

    private static ModelPart[] BuildCampfire()
    {
        var parts = new List<ModelPart>();
        for (int i = 0; i < 8; i++)
        {
            float a = i * MathF.Tau / 8f + 0.2f;
            float x = MathF.Cos(a) * 8.5f, z = MathF.Sin(a) * 8.5f;
            parts.Add(P("stone" + i, PartLook.Rock, x - 1.5f, 21.5f, z - 1.5f, 3, 3, 3));
        }
        parts.Add(P("coals", PartLook.Coals, -5, 22.5f, -5, 10, 1, 10));
        // Two layers of logs, each pair at a right angle, the upper pair turned an eighth
        for (int i = 0; i < 4; i++)
        {
            float turn = (i < 2 ? 0f : MathF.PI / 4f) + (i % 2) * MathF.PI / 2f;
            float y = i < 2 ? 20.5f : 18.5f;
            parts.Add(P("log" + i, PartLook.Wood, -1, y, -5.5f, 2, 2, 11, pivot: new Vector3(0, y + 1, 0), baseRot: new Vector3(0, turn, 0)));
        }
        return parts.ToArray();
    }

    /// <summary>A stick in the ground with a glowing head.</summary>
    public static readonly CreatureModel Torch = new()
    {
        Name = "torch", Palette = None,
        Parts = new[]
        {
            P("stick", PartLook.Wood, -1, 12, -1, 2, 12, 2),
            P("head", PartLook.Coals, -1.5f, 9, -1.5f, 3, 3, 3),
        },
    };

    public static readonly CreatureModel Mushroom = new()
    {
        Name = "mushroom", Palette = None,
        Parts = new[]
        {
            P("stalk", PartLook.MushroomStalk, -1, 20, -1, 2, 4, 2),
            P("cap", PartLook.MushroomCap, -2.5f, 18, -2.5f, 5, 2, 5),
            P("stalk2", PartLook.MushroomStalk, 3, 22, 1, 1, 2, 1),
            P("cap2", PartLook.MushroomCap, 2, 21, 0, 3, 1, 3),
        },
    };

    public static readonly CreatureModel Berries = new()
    {
        Name = "berries", Palette = None,
        Parts = new[]
        {
            P("bush", PartLook.Leaf, -3, 20, -3, 6, 4, 6),
            P("bushTop", PartLook.Leaf, -2, 18, -2, 4, 2, 4),
            P("b1", PartLook.Berry, -3.5f, 21, -1, 1, 1, 1),
            P("b2", PartLook.Berry, 2.5f, 20, 1, 1, 1, 1),
            P("b3", PartLook.Berry, 0, 19, 2.5f, 1, 1, 1),
            P("b4", PartLook.Berry, -1, 17.5f, -1, 1, 1, 1),
            P("b5", PartLook.Berry, 1, 22, -3.5f, 1, 1, 1),
        },
    };

    public static CreatureModel Meat(bool cooked) => new()
    {
        Name = cooked ? "cookedMeat" : "rawMeat", Palette = None,
        Parts = new[]
        {
            P("meat", cooked ? PartLook.MeatCooked : PartLook.MeatRaw, -3, 21, -2, 6, 3, 4),
            P("bone", PartLook.Bone, -4.5f, 22, -0.5f, 2, 1, 1),
            P("boneEnd", PartLook.Bone, -5.5f, 21.5f, -1, 1, 2, 2),
        },
    };

    public static readonly CreatureModel Pelt = new()
    {
        Name = "pelt", Palette = None,
        Parts = new[] { P("pelt", PartLook.Leather, -4, 23, -3, 8, 1, 6) },
    };

    public static readonly CreatureModel Log = new()
    {
        Name = "log", Palette = None,
        Parts = new[] { P("log", PartLook.Wood, -1.5f, 21, -4.5f, 3, 3, 9) },
    };

    public static readonly CreatureModel Pebbles = new()
    {
        Name = "stone", Palette = None,
        Parts = new[]
        {
            P("a", PartLook.Rock, -3, 21, -2, 5, 3, 4),
            P("b", PartLook.Rock, 1, 22, 1, 3, 2, 3),
        },
    };

    public static readonly CreatureModel Pile = new()
    {
        Name = "pile", Palette = None,
        Parts = new[]
        {
            P("heap", PartLook.Leaf, -3, 21.5f, -3, 6, 2.5f, 6),
            P("top", PartLook.Leaf, -2, 20, -2, 4, 2, 4),
        },
    };

    /// <summary>Wrapped bundle: anything else a corpse leaves or a pocket holds.</summary>
    public static readonly CreatureModel Bundle = new()
    {
        Name = "bundle", Palette = None,
        Parts = new[]
        {
            P("sack", PartLook.Leather, -3, 19, -3, 6, 5, 6),
            P("cord", PartLook.Cloth, -3.2f, 21, -3.2f, 6.4f, 1, 6.4f),
        },
    };

    public static readonly CreatureModel Can = new()
    {
        Name = "can", Palette = None,
        Parts = new[]
        {
            P("tin", PartLook.Steel, -2, 19, -2, 4, 5, 4),
            P("label", PartLook.Berry, -2.2f, 20.4f, -2.2f, 4.4f, 2.2f, 4.4f),
        },
    };

    /// <summary>A bedroll laid out on the ground: a dull olive mat, a pillow, a strap.</summary>
    public static readonly CreatureModel Bedroll = new()
    {
        Name = "bedroll", Palette = None,
        Parts = new[]
        {
            P("mat", PartLook.Sleeve, -6, 22, -9, 12, 2, 18),
            P("pillow", PartLook.Cloth, -5, 20.6f, -8.5f, 10, 1.4f, 4),
            P("strap", PartLook.Leather, -6.2f, 20.9f, 2, 12.4f, 1.4f, 2),
        },
    };

    public static readonly CreatureModel Bandage = new()
    {
        Name = "bandage", Palette = None,
        Parts = new[]
        {
            P("roll", PartLook.Cloth, -3, 21, -2, 6, 3, 4),
            P("stain", PartLook.Berry, -1, 20.7f, -2.1f, 2, 1, 4.2f),
        },
    };

    /// <summary>The model that stands for a loot item on the ground, by the item's id.</summary>
    public static (CreatureModel model, float scale) ForItem(string id) => id switch
    {
        "food.mushroom" => (Mushroom, 1.0f),
        "food.berry" => (Berries, 1.0f),
        "material.wood" or "material.planks" => (Log, 0.9f),
        "material.stone" or "material.cobble" => (Pebbles, 0.9f),
        "material.leaves" or "material.dirt" or "material.sand" => (Pile, 0.9f),
        "material.pelt" => (Pelt, 1.0f),
        "material.cloth" => (Bandage, 0.9f),
        _ when id.StartsWith("consume.can") => (Can, 1.0f),
        _ when id.StartsWith("food.") && id.EndsWith("_cooked") => (CookedMeat, 0.9f),
        _ when id.StartsWith("food.") && id.Contains("meat") => (RawMeat, 0.9f),
        _ when id.StartsWith("med.") => (Bandage, 0.9f),
        _ => (Bundle, 0.9f),
    };

    private static readonly CreatureModel CookedMeat = Meat(true);
    private static readonly CreatureModel RawMeat = Meat(false);

    public static IEnumerable<CreatureModel> All => new[]
    {
        Campfire, Torch, Mushroom, Berries, RawMeat, CookedMeat, Pelt, Log, Pebbles, Pile, Bundle, Bandage, Can, Bedroll,
    };
}

/// <summary>The tools the player holds, in a space of their own: pixels, y down, the grip at the origin, the handle running toward +y, the working end toward -y.</summary>
public static class HeldModels
{
    private static ModelPart P(string name, PartLook look, float ox, float oy, float oz, float w, float h, float d,
        Vector3? pivot = null, Vector3 baseRot = default) =>
        new() { Name = name, Look = look, Origin = new Vector3(ox, oy, oz), Size = new Vector3(w, h, d), Pivot = pivot, BaseRotation = baseRot };

    private static readonly FurPalette None = new(Vector3.One, Vector3.One, Vector3.One, Vector3.One, 0.1f);

    public static readonly CreatureModel Axe = new()
    {
        Name = "axe", Palette = None,
        Parts = new[]
        {
            P("haft", PartLook.Wood, -1, -12, -1, 2, 18, 2),
            P("head", PartLook.Steel, -6, -13, -1.5f, 5, 6, 3),
            P("edge", PartLook.SteelEdge, -8, -14, -1, 2, 8, 2),
            P("poll", PartLook.Steel, 1, -12, -1.5f, 2, 3, 3),
            P("wrap", PartLook.Leather, -1.2f, 0, -1.2f, 2.4f, 5, 2.4f),
        },
    };

    public static readonly CreatureModel Pick = new()
    {
        Name = "pick", Palette = None,
        Parts = new[]
        {
            P("haft", PartLook.Wood, -1, -12, -1, 2, 18, 2),
            P("bar", PartLook.Steel, -8, -13, -1, 16, 2, 2),
            P("tipL", PartLook.SteelEdge, -10, -11, -1, 2, 2, 2),
            P("tipR", PartLook.SteelEdge, 8, -11, -1, 2, 2, 2),
            P("wrap", PartLook.Leather, -1.2f, 0, -1.2f, 2.4f, 5, 2.4f),
        },
    };

    public static readonly CreatureModel Bow = new()
    {
        Name = "bow", Palette = None,
        Parts = new[]
        {
            P("grip", PartLook.Leather, -1.2f, -3, -1.2f, 2.4f, 6, 2.4f),
            P("upper", PartLook.Wood, -1, -13, -1, 2, 10, 2, pivot: new Vector3(0, -3, 0), baseRot: new Vector3(0, 0, -0.22f)),
            P("lower", PartLook.Wood, -1, 3, -1, 2, 10, 2, pivot: new Vector3(0, 3, 0), baseRot: new Vector3(0, 0, 0.22f)),
            P("string", PartLook.Cloth, 4, -12, -0.5f, 1, 24, 1),
        },
    };

    public static readonly CreatureModel Torch = new()
    {
        Name = "heldTorch", Palette = None,
        Parts = new[]
        {
            P("stick", PartLook.Wood, -1, -4, -1, 2, 14, 2),
            P("head", PartLook.Coals, -1.5f, -7, -1.5f, 3, 3, 3),
        },
    };

    public static readonly CreatureModel Roll = new()
    {
        Name = "heldBandage", Palette = None,
        Parts = new[]
        {
            P("roll", PartLook.Cloth, -2, -3, -2, 4, 5, 4),
            P("stain", PartLook.Berry, -2.1f, -1, -1, 4.2f, 1, 2),
        },
    };

    /// <summary>The player's right arm (a sleeve and a hand), drawn with whatever is held: running down from the grip toward the corner of the screen.</summary>
    public static readonly CreatureModel Arm = new()
    {
        Name = "arm", Palette = None,
        Parts = new[]
        {
            P("hand", PartLook.Skin, -2.2f, -1.5f, -2.2f, 4.4f, 5, 4.4f),
            P("sleeve", PartLook.Sleeve, -2.5f, 3.5f, -2.5f, 5, 16, 5),
        },
    };

    /// <summary>
    /// The player's forearm and fist in the controller's own space (1 px = 1 cm at the scale it is drawn with; y down, as every model; the grip at the origin, the forearm
    /// running back along +z toward the elbow): rigid with the controller, so the hand and the elbow move as one.
    /// </summary>
    public static readonly CreatureModel Forearm = new()
    {
        Name = "forearm", Palette = None,
        Parts = new[]
        {
            P("fist", PartLook.Skin, -4f, -3.5f, -5f, 8f, 7f, 9f),
        },
    };

    /// <summary>The sleeve of the forearm (and its cuff), in the same frame: it is tilted about the wrist, since a forearm does not run along the way a controller points.</summary>
    public static readonly CreatureModel Sleeve = new()
    {
        Name = "sleeve", Palette = None,
        Parts = new[]
        {
            P("sleeve", PartLook.Sleeve, -3.8f, -3.6f, 4f, 7.6f, 7.2f, 24f),
            P("cuff", PartLook.Leather, -4.1f, -3.9f, 4f, 8.2f, 7.8f, 2.5f),
        },
    };

    /// <summary>A wrist device strapped on top of the left forearm (the controller's +Y side), its face looking up out of it.</summary>
    public static readonly CreatureModel WristDevice = new()
    {
        Name = "wristdevice", Palette = None,
        Parts = new[]
        {
            P("strap", PartLook.Leather, -4.4f, -4.9f, 8f, 8.8f, 1.6f, 12.5f),
            P("body", PartLook.Rock, -5.4f, -8.3f, 8.3f, 10.8f, 3.4f, 11.9f),
            P("bezel", PartLook.Leather, -5.7f, -8.6f, 8.1f, 11.4f, 0.4f, 12.3f),
        },
    };

    public static CreatureModel? For(string itemId) => itemId switch
    {
        "tool.axe" or "tool.stone_axe" => Axe,
        "tool.pick" or "tool.stone_pick" => Pick,
        "tool.bow" => Bow,
        _ when itemId.Contains("torch") || itemId == "tool.flare" => Torch,
        _ when itemId.StartsWith("med.") => Roll,
        _ => null,
    };

    public static IEnumerable<CreatureModel> All => new[] { Axe, Pick, Bow, Torch, Roll, Arm };
}
