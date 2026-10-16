using System.Numerics;
using LostSpawns.Services;
using static LostSpawns.Rendering.TileNoise;

namespace LostSpawns.Rendering;

/// <summary>How a part moves: the legs swing in diagonal pairs as the creature walks, the head and tail follow its mood, wings flap.</summary>
public enum PartGait { None, FrontLegA, FrontLegB, HindLegA, HindLegB, Head, Tail, EarLeft, EarRight, WingLeft, WingRight }

/// <summary>What a part is painted as (which fur, where the features go).</summary>
public enum PartLook
{
    Body, Head, Snout, Ear, Leg, Tail, Scut, Beak, Wing, Antler, Tusk,
    // What is not fur
    Wood, Steel, SteelEdge, Rock, Skin, Sleeve, Leather, Cloth, Coals, MushroomCap, MushroomStalk, Leaf, Berry, MeatRaw, MeatCooked, Bone,
}

/// <summary>
/// One box of a creature. Model space is Minecraft's: pixels, 16 to a block, y pointing DOWN, the creature facing -z, its feet at y = 24
/// (the pose adds the lift that stands it on the ground). <see cref="Origin"/> is the box's minimum corner, <see cref="Size"/> whole pixels.
/// </summary>
public sealed class ModelPart
{
    public required string Name { get; init; }
    public required PartLook Look { get; init; }
    public PartGait Gait { get; init; } = PartGait.None;
    public required Vector3 Origin { get; init; }
    public required Vector3 Size { get; init; }
    /// <summary>The point the part turns about; default is the middle of the top of the box (a limb hangs from it).</summary>
    public Vector3? Pivot { get; init; }
    /// <summary>A fixed turn (radians about x, y, z) the part always has, before it moves.</summary>
    public Vector3 BaseRotation { get; init; }
    /// <summary>Index of the part this one is attached to (it moves with it), or -1.</summary>
    public int Parent { get; init; } = -1;
    /// <summary>Where the part's texture sits in the creature atlas (pixels), set when the atlas is packed.</summary>
    public Vector2 Uv { get; set; }

    public Vector3 PivotPoint => Pivot ?? new Vector3(Origin.X + Size.X * 0.5f, Origin.Y, Origin.Z + Size.Z * 0.5f);
    /// <summary>The part's footprint in the atlas, Minecraft's cross layout: 2 * (w + d) wide, d + h tall.</summary>
    public (int w, int h) Footprint => ((int)(2 * (Size.X + Size.Z)), (int)(Size.Z + Size.Y));
}

/// <summary>The colours a creature's fur is painted from.</summary>
public readonly record struct FurPalette(Vector3 Main, Vector3 Dark, Vector3 Light, Vector3 Accent, float Coarse = 0.12f);

public sealed class CreatureModel
{
    public required string Name { get; init; }
    public required ModelPart[] Parts { get; init; }
    public required FurPalette Palette { get; init; }
    /// <summary>World scale on top of 1/16 block a pixel.</summary>
    public float Scale { get; init; } = 1f;
    /// <summary>Gait: radians of leg swing at a full walk, and how fast the stride turns per block walked.</summary>
    public float Swing { get; init; } = 0.8f;
    public float StrideRate { get; init; } = 4.2f;
    /// <summary>The creature flies or hovers (no leg gait; wings flap and it stays where its position says).</summary>
    public bool Flies { get; init; }
}

/// <summary>The six creatures, built from boxes and painted procedurally: no image files.</summary>
public static class CreatureModels
{
    private static ModelPart P(string name, PartLook look, PartGait gait, float ox, float oy, float oz, float w, float h, float d,
        int parent = -1, Vector3? pivot = null, Vector3 baseRot = default) =>
        new() { Name = name, Look = look, Gait = gait, Origin = new Vector3(ox, oy, oz), Size = new Vector3(w, h, d), Parent = parent, Pivot = pivot, BaseRotation = baseRot };

    public static CreatureModel For(EntityKind kind) => kind switch
    {
        EntityKind.Wolf => Wolf,
        EntityKind.Deer => Deer,
        EntityKind.Boar => Boar,
        EntityKind.Bear => Bear,
        EntityKind.Rabbit => Rabbit,
        _ => Crow,
    };

    public static IEnumerable<CreatureModel> Creatures => new[] { Wolf, Deer, Boar, Bear, Rabbit, Crow };

    /// <summary>Every model that is painted into the atlas: the creatures, the props and the held tools.</summary>
    public static IEnumerable<CreatureModel> All => Creatures.Concat(PropModels.All).Concat(HeldModels.All);

    // Wolf: lean, grey, bushy tail carried low
    public static readonly CreatureModel Wolf = new()
    {
        Name = "wolf",
        Palette = new FurPalette(new Vector3(0.52f, 0.50f, 0.48f), new Vector3(0.24f, 0.23f, 0.24f), new Vector3(0.84f, 0.82f, 0.78f), new Vector3(0.08f, 0.07f, 0.08f)),
        Scale = 1.0f, Swing = 0.85f,
        Parts = new[]
        {
            P("body", PartLook.Body, PartGait.None, -3, 10, -5, 6, 6, 11),
            P("head", PartLook.Head, PartGait.Head, -3, 7, -9, 6, 6, 4, pivot: new Vector3(0, 12, -5)),
            P("snout", PartLook.Snout, PartGait.None, -1.5f, 10.5f, -12, 3, 3, 3, parent: 1),
            P("earL", PartLook.Ear, PartGait.EarLeft, 0.5f, 5, -7, 2, 2, 1, parent: 1),
            P("earR", PartLook.Ear, PartGait.EarRight, -2.5f, 5, -7, 2, 2, 1, parent: 1),
            P("tail", PartLook.Tail, PartGait.Tail, -1, 10, 6, 2, 2, 7, pivot: new Vector3(0, 11, 6), baseRot: new Vector3(-0.75f, 0, 0)),
            P("legFL", PartLook.Leg, PartGait.FrontLegA, 1, 16, -4, 2, 8, 2),
            P("legFR", PartLook.Leg, PartGait.FrontLegB, -3, 16, -4, 2, 8, 2),
            P("legHL", PartLook.Leg, PartGait.HindLegB, 1, 16, 3, 2, 8, 2),
            P("legHR", PartLook.Leg, PartGait.HindLegA, -3, 16, 3, 2, 8, 2),
        },
    };

    // Deer: long legs, slender neck, antlers, white tail
    public static readonly CreatureModel Deer = new()
    {
        Name = "deer",
        Palette = new FurPalette(new Vector3(0.58f, 0.40f, 0.24f), new Vector3(0.38f, 0.25f, 0.15f), new Vector3(0.88f, 0.80f, 0.68f), new Vector3(0.78f, 0.72f, 0.60f)),
        Scale = 0.85f, Swing = 0.7f,
        Parts = new[]
        {
            P("body", PartLook.Body, PartGait.None, -3, 2, -6, 6, 8, 14),
            P("neck", PartLook.Body, PartGait.None, -1.5f, -5, -8, 3, 9, 3, baseRot: new Vector3(0.35f, 0, 0), pivot: new Vector3(0, 3, -6)),
            P("head", PartLook.Head, PartGait.Head, -2, -9, -12, 4, 4, 7, pivot: new Vector3(0, -5, -8)),
            P("antlerL", PartLook.Antler, PartGait.None, 1, -15, -9, 1, 6, 1, parent: 2),
            P("antlerLb", PartLook.Antler, PartGait.None, 1, -15, -12, 1, 1, 3, parent: 2),
            P("antlerR", PartLook.Antler, PartGait.None, -2, -15, -9, 1, 6, 1, parent: 2),
            P("antlerRb", PartLook.Antler, PartGait.None, -2, -15, -12, 1, 1, 3, parent: 2),
            P("earL", PartLook.Ear, PartGait.EarLeft, 1.5f, -10, -8, 2, 3, 1, parent: 2),
            P("earR", PartLook.Ear, PartGait.EarRight, -3.5f, -10, -8, 2, 3, 1, parent: 2),
            P("tail", PartLook.Tail, PartGait.Tail, -1, 2, 8, 2, 3, 1, pivot: new Vector3(0, 2, 8)),
            P("legFL", PartLook.Leg, PartGait.FrontLegA, 1, 10, -5, 2, 14, 2),
            P("legFR", PartLook.Leg, PartGait.FrontLegB, -3, 10, -5, 2, 14, 2),
            P("legHL", PartLook.Leg, PartGait.HindLegB, 1, 10, 5, 2, 14, 2),
            P("legHR", PartLook.Leg, PartGait.HindLegA, -3, 10, 5, 2, 14, 2),
        },
    };

    // Boar: low and heavy, tusks, bristly dark coat
    public static readonly CreatureModel Boar = new()
    {
        Name = "boar",
        Palette = new FurPalette(new Vector3(0.32f, 0.25f, 0.20f), new Vector3(0.14f, 0.11f, 0.10f), new Vector3(0.50f, 0.40f, 0.34f), new Vector3(0.46f, 0.36f, 0.33f), Coarse: 0.2f),
        Scale = 0.9f, Swing = 0.75f, StrideRate = 5.0f,
        Parts = new[]
        {
            P("body", PartLook.Body, PartGait.None, -4, 10, -6, 8, 8, 12),
            P("head", PartLook.Head, PartGait.Head, -3, 11, -11, 6, 6, 5, pivot: new Vector3(0, 14, -6)),
            P("snout", PartLook.Snout, PartGait.None, -2, 14, -14, 4, 3, 3, parent: 1),
            P("tuskL", PartLook.Tusk, PartGait.None, 1.5f, 15.5f, -14, 1, 2, 1, parent: 1),
            P("tuskR", PartLook.Tusk, PartGait.None, -2.5f, 15.5f, -14, 1, 2, 1, parent: 1),
            P("earL", PartLook.Ear, PartGait.EarLeft, 1.5f, 9, -9, 2, 2, 1, parent: 1),
            P("earR", PartLook.Ear, PartGait.EarRight, -3.5f, 9, -9, 2, 2, 1, parent: 1),
            P("tail", PartLook.Tail, PartGait.Tail, -0.5f, 11, 6, 1, 3, 1, pivot: new Vector3(0, 11, 6)),
            P("legFL", PartLook.Leg, PartGait.FrontLegA, 1, 18, -5, 3, 6, 3),
            P("legFR", PartLook.Leg, PartGait.FrontLegB, -4, 18, -5, 3, 6, 3),
            P("legHL", PartLook.Leg, PartGait.HindLegB, 1, 18, 3, 3, 6, 3),
            P("legHR", PartLook.Leg, PartGait.HindLegA, -4, 18, 3, 3, 6, 3),
        },
    };

    // Bear: big, brown, heavy-legged
    public static readonly CreatureModel Bear = new()
    {
        Name = "bear",
        Palette = new FurPalette(new Vector3(0.30f, 0.20f, 0.13f), new Vector3(0.16f, 0.11f, 0.08f), new Vector3(0.46f, 0.34f, 0.24f), new Vector3(0.10f, 0.08f, 0.07f)),
        Scale = 1.15f, Swing = 0.6f, StrideRate = 3.6f,
        Parts = new[]
        {
            P("body", PartLook.Body, PartGait.None, -5, 6, -8, 10, 10, 16),
            P("head", PartLook.Head, PartGait.Head, -3.5f, 5, -14, 7, 6, 6, pivot: new Vector3(0, 10, -8)),
            P("snout", PartLook.Snout, PartGait.None, -2, 9, -17, 4, 3, 3, parent: 1),
            P("earL", PartLook.Ear, PartGait.EarLeft, 1.5f, 3, -12, 2, 2, 1, parent: 1),
            P("earR", PartLook.Ear, PartGait.EarRight, -3.5f, 3, -12, 2, 2, 1, parent: 1),
            P("tail", PartLook.Tail, PartGait.None, -1, 7, 8, 2, 2, 1),
            P("legFL", PartLook.Leg, PartGait.FrontLegA, 1, 16, -7, 4, 8, 4),
            P("legFR", PartLook.Leg, PartGait.FrontLegB, -5, 16, -7, 4, 8, 4),
            P("legHL", PartLook.Leg, PartGait.HindLegB, 1, 16, 4, 4, 8, 4),
            P("legHR", PartLook.Leg, PartGait.HindLegA, -5, 16, 4, 4, 8, 4),
        },
    };

    // Rabbit: small, long ears, big hind legs, white scut
    public static readonly CreatureModel Rabbit = new()
    {
        Name = "rabbit",
        Palette = new FurPalette(new Vector3(0.58f, 0.50f, 0.42f), new Vector3(0.36f, 0.30f, 0.26f), new Vector3(0.90f, 0.86f, 0.80f), new Vector3(0.95f, 0.93f, 0.90f), Coarse: 0.1f),
        Scale = 1.0f, Swing = 0.95f, StrideRate = 6.5f,
        Parts = new[]
        {
            P("body", PartLook.Body, PartGait.None, -2, 17, -3, 4, 4, 7),
            P("head", PartLook.Head, PartGait.Head, -1.5f, 15, -6, 3, 3, 4, pivot: new Vector3(0, 18, -3)),
            P("earL", PartLook.Ear, PartGait.EarLeft, 0, 10, -4, 1, 5, 1, parent: 1),
            P("earR", PartLook.Ear, PartGait.EarRight, -1, 10, -4, 1, 5, 1, parent: 1),
            P("tail", PartLook.Scut, PartGait.None, -1, 17, 4, 2, 2, 2),
            P("legFL", PartLook.Leg, PartGait.FrontLegA, 0.5f, 21, -3, 1, 3, 1),
            P("legFR", PartLook.Leg, PartGait.FrontLegA, -1.5f, 21, -3, 1, 3, 1),
            P("legHL", PartLook.Leg, PartGait.HindLegB, 1, 19, 2, 1, 5, 3),
            P("legHR", PartLook.Leg, PartGait.HindLegB, -2, 19, 2, 1, 5, 3),
        },
    };

    // Crow: black, short legs, wings that flap
    public static readonly CreatureModel Crow = new()
    {
        Name = "crow",
        Palette = new FurPalette(new Vector3(0.10f, 0.10f, 0.13f), new Vector3(0.04f, 0.04f, 0.06f), new Vector3(0.18f, 0.18f, 0.24f), new Vector3(0.30f, 0.28f, 0.26f), Coarse: 0.08f),
        Scale = 1.2f, Flies = true,
        Parts = new[]
        {
            P("body", PartLook.Body, PartGait.None, -2, 17, -3, 4, 3, 7),
            P("head", PartLook.Head, PartGait.Head, -1.5f, 14, -6, 3, 3, 3, pivot: new Vector3(0, 17, -3)),
            P("beak", PartLook.Beak, PartGait.None, -0.5f, 15.5f, -9, 1, 1, 3, parent: 1),
            P("wingL", PartLook.Wing, PartGait.WingLeft, 2, 17, -2, 8, 1, 5, pivot: new Vector3(2, 17.5f, 0)),
            P("wingR", PartLook.Wing, PartGait.WingRight, -10, 17, -2, 8, 1, 5, pivot: new Vector3(-2, 17.5f, 0)),
            P("tail", PartLook.Tail, PartGait.Tail, -1.5f, 17, 4, 3, 1, 4),
            P("legL", PartLook.Leg, PartGait.None, 0.5f, 20, -1, 1, 3, 1),
            P("legR", PartLook.Leg, PartGait.None, -1.5f, 20, -1, 1, 3, 1),
        },
    };
}

/// <summary>Packs every part of every creature into one atlas and paints their fur.</summary>
public static class CreatureTextures
{
    public const int Width = 256;

    public static int Height { get; private set; }

    /// <summary>Lay the parts out (shelf packing), set each part's <see cref="ModelPart.Uv"/>, and return the RGBA atlas.</summary>
    public static byte[] Build()
    {
        // Pack
        int x = 0, y = 0, rowH = 0;
        foreach (var model in CreatureModels.All)
        foreach (var part in model.Parts)
        {
            var (w, h) = part.Footprint;
            if (x + w > Width) { x = 0; y += rowH; rowH = 0; }
            part.Uv = new Vector2(x, y);
            x += w;
            rowH = Math.Max(rowH, h);
        }
        Height = y + rowH;
        var rgba = new byte[Width * Height * 4];

        int seed = 1;
        foreach (var model in CreatureModels.All)
        {
            foreach (var part in model.Parts)
                PaintPart(rgba, model, part, seed++);
        }
        return rgba;
    }

    // The faces of a box in Minecraft's layout: (name, atlas x, atlas y, width, height) relative to the part's uv
    private static void PaintPart(byte[] rgba, CreatureModel model, ModelPart part, int seed)
    {
        int w = (int)part.Size.X, h = (int)part.Size.Y, d = (int)part.Size.Z;
        int u0 = (int)part.Uv.X, v0 = (int)part.Uv.Y;
        // right (+x side seen from the left), front (-z), left, back (+z), top, bottom
        PaintFace(rgba, model, part, seed, Face.Top, u0 + d, v0, w, d);
        PaintFace(rgba, model, part, seed + 101, Face.Bottom, u0 + d + w, v0, w, d);
        PaintFace(rgba, model, part, seed + 202, Face.SideA, u0, v0 + d, d, h);
        PaintFace(rgba, model, part, seed + 303, Face.Front, u0 + d, v0 + d, w, h);
        PaintFace(rgba, model, part, seed + 404, Face.SideB, u0 + d + w, v0 + d, d, h);
        PaintFace(rgba, model, part, seed + 505, Face.Back, u0 + d + w + d, v0 + d, w, h);
    }

    private enum Face { Top, Bottom, SideA, SideB, Front, Back }

    private static void PaintFace(byte[] rgba, CreatureModel model, ModelPart part, int seed, Face face, int fx, int fy, int fw, int fh)
    {
        for (int py = 0; py < fh; py++)
        for (int px = 0; px < fw; px++)
        {
            float u = (px + 0.5f) / fw, v = (py + 0.5f) / fh;
            var c = Shade(model.Palette, part, face, u, v, px, py, fw, fh, seed);
            int i = ((fy + py) * Width + fx + px) * 4;
            rgba[i] = ToByte(c.X); rgba[i + 1] = ToByte(c.Y); rgba[i + 2] = ToByte(c.Z); rgba[i + 3] = 255;
        }
    }

    private static byte ToByte(float v) => (byte)Math.Clamp((int)(v * 255f + 0.5f), 0, 255);

    private static Vector3 Mix(Vector3 a, Vector3 b, float t) => a + (b - a) * Math.Clamp(t, 0f, 1f);

    private static Vector3 Shade(FurPalette pal, ModelPart part, Face face, float u, float v, int px, int py, int fw, int fh, int seed)
    {
        // Wood, steel and food are not fur: their own cases below set the colour and the grain; the fur coarseness stays small for them
        // Fur: a base tone, a little noise per texel and a coarser streak, so no face is flat
        float jitter = (Rand(px, py, seed) - 0.5f) * 2f * pal.Coarse;
        var color = pal.Main;
        switch (part.Look)
        {
            case PartLook.Body:
            case PartLook.Tail:
            case PartLook.Wing:
                // Dark along the back, light underneath
                if (face == Face.Top) color = pal.Dark;
                else if (face == Face.Bottom) color = pal.Light;
                else color = Mix(Mix(pal.Dark, pal.Main, SmoothStep(0f, 0.45f, v)), pal.Light, SmoothStep(0.6f, 1f, v));
                if (part.Look == PartLook.Tail && face == Face.Back) color = Mix(color, pal.Light, 0.4f);
                break;
            case PartLook.Scut:
                color = pal.Accent;                    // the white tail of a rabbit
                break;
            case PartLook.Head:
                color = face == Face.Top ? pal.Dark : face == Face.Bottom ? pal.Light : Mix(pal.Main, pal.Light, SmoothStep(0.55f, 1f, v) * 0.6f);
                if (face == Face.Front)
                {
                    // Eyes: a dark texel each side, a third of the way down
                    int eyeY = Math.Max(1, (int)(fh * 0.35f));
                    int eyeInset = fw >= 5 ? 1 : 0;
                    if (py == eyeY && (px == eyeInset || px == fw - 1 - eyeInset)) color = new Vector3(0.04f, 0.035f, 0.03f);
                    else if (py == eyeY - 1 && (px == eyeInset || px == fw - 1 - eyeInset) && fh >= 4) color = Mix(color, pal.Dark, 0.5f);
                }
                break;
            case PartLook.Snout:
                color = face == Face.Top ? pal.Main : Mix(pal.Light, pal.Accent, 0.25f);
                if (face == Face.Front)
                {
                    // The nose: dark in the middle of the top edge
                    int cx = fw / 2;
                    if (py <= Math.Max(fh / 2 - 1, 0) && Math.Abs(px - cx) <= (fw >= 4 ? 1 : 0)) color = new Vector3(0.05f, 0.045f, 0.045f);
                }
                break;
            case PartLook.Ear:
                color = Mix(pal.Dark, pal.Main, 0.35f);
                if (face == Face.Front) color = Mix(color, pal.Accent, 0.45f);   // the inside of the ear
                break;
            case PartLook.Leg:
                // Socks: the foot is darker than the thigh
                color = Mix(pal.Main, pal.Dark, SmoothStep(0.5f, 0.95f, v));
                if (face == Face.Bottom) color = pal.Dark;
                break;
            case PartLook.Beak:
                color = new Vector3(0.22f, 0.21f, 0.22f);
                break;
            case PartLook.Wood:
            {
                // Grain along the long side of the piece
                float grain = Value(u, v, 10, 3, seed) * 0.5f + Value(u, v, 3, 10, seed + 5) * 0.3f;
                color = new Vector3(0.50f, 0.35f, 0.21f) * (0.65f + 0.7f * grain);
                if (face == Face.Top || face == Face.Bottom) color *= 0.85f;
                pal = new FurPalette(pal.Main, pal.Dark, pal.Light, pal.Accent, 0.06f);
                break;
            }
            case PartLook.Steel:
                color = new Vector3(0.60f, 0.62f, 0.67f) * (0.8f + 0.35f * Value(u, v, 5, 5, seed));
                if (px == 0 || py == 0 || px == fw - 1 || py == fh - 1) color *= 0.72f;
                break;
            case PartLook.SteelEdge:
                color = new Vector3(0.82f, 0.85f, 0.90f) * (0.85f + 0.2f * Value(u, v, 4, 6, seed));
                break;
            case PartLook.Rock:
            {
                float n = Value(u, v, 4, 4, seed) * 0.6f + Value(u, v, 9, 9, seed + 3) * 0.4f;
                color = new Vector3(0.46f, 0.45f, 0.47f) * (0.65f + 0.7f * n);
                if (face == Face.Top) color *= 1.15f;
                break;
            }
            case PartLook.Skin:
                color = new Vector3(0.82f, 0.64f, 0.52f) * (0.92f + 0.12f * Value(u, v, 4, 4, seed));
                break;
            case PartLook.Sleeve:
                color = new Vector3(0.25f, 0.30f, 0.22f) * (0.8f + 0.4f * Value(u, v, 6, 8, seed));
                if (v > 0.92f) color = new Vector3(0.18f, 0.22f, 0.16f);   // the cuff
                break;
            case PartLook.Leather:
                color = new Vector3(0.36f, 0.24f, 0.14f) * (0.75f + 0.5f * Value(u, v, 6, 6, seed));
                break;
            case PartLook.Cloth:
                color = new Vector3(0.88f, 0.86f, 0.80f) * (0.88f + 0.18f * Value(u, v, 8, 8, seed));
                break;
            case PartLook.Coals:
            {
                // Black char with glowing cracks, the bright ones above all
                float glow = Value(u, v, 5, 5, seed);
                float spark = Rand(px, py, seed + 11);
                color = new Vector3(0.10f, 0.08f, 0.07f);
                if (glow > 0.62f || spark > 0.9f) color = Vector3.Lerp(new Vector3(0.9f, 0.32f, 0.06f), new Vector3(1.4f, 0.8f, 0.2f), spark);
                break;
            }
            case PartLook.MushroomCap:
                color = new Vector3(0.74f, 0.13f, 0.11f);
                if (Rand(px, py, seed + 21) > 0.82f) color = new Vector3(0.95f, 0.92f, 0.86f);   // the white flecks
                if (face == Face.Bottom) color = new Vector3(0.88f, 0.82f, 0.70f);
                break;
            case PartLook.MushroomStalk:
                color = new Vector3(0.90f, 0.86f, 0.76f) * (0.9f + 0.15f * Value(u, v, 3, 6, seed));
                break;
            case PartLook.Leaf:
                color = new Vector3(0.18f, 0.40f, 0.14f) * (0.7f + 0.6f * Value(u, v, 6, 6, seed));
                break;
            case PartLook.Berry:
                color = new Vector3(0.55f, 0.05f, 0.18f) * (0.85f + 0.3f * Rand(px, py, seed + 31));
                break;
            case PartLook.MeatRaw:
                color = new Vector3(0.72f, 0.16f, 0.18f) * (0.85f + 0.25f * Value(u, v, 6, 6, seed));
                if (Value(u, v, 8, 8, seed + 7) > 0.7f) color = Vector3.Lerp(color, new Vector3(0.95f, 0.75f, 0.72f), 0.6f);   // marbling
                break;
            case PartLook.MeatCooked:
                color = new Vector3(0.46f, 0.25f, 0.13f) * (0.8f + 0.3f * Value(u, v, 6, 6, seed));
                if (face == Face.Top) color *= 0.75f;
                break;
            case PartLook.Bone:
                color = new Vector3(0.90f, 0.87f, 0.78f);
                break;
            case PartLook.Antler:
                color = Mix(new Vector3(0.52f, 0.43f, 0.32f), new Vector3(0.74f, 0.66f, 0.52f), SmoothStep(0.3f, 1f, 1f - v));
                break;
            case PartLook.Tusk:
                color = new Vector3(0.88f, 0.84f, 0.72f);
                break;
        }
        float streak = (Value(u, v, 4, 6, seed) - 0.5f) * pal.Coarse * 1.5f;
        float tone = 1f + jitter + streak;
        return color * tone;
    }
}
