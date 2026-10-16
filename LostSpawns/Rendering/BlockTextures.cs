using System.Numerics;
using static LostSpawns.Rendering.TileNoise;

namespace LostSpawns.Rendering;

/// <summary>Tile numbers in the block atlas (row-major, as <c>BlockMaterial</c> faces name them).</summary>
public static class BlockTiles
{
    public const int GrassTop = 0, GrassSide = 1, Dirt = 2, Stone = 3, Sand = 4, LogSide = 5, LogTop = 6, Leaves = 7;
    /// <summary>Eight frames of the water surface, a seamless loop.</summary>
    public const int Water0 = 8, WaterFrames = 8;
    public const int Count = Water0 + WaterFrames;
}

/// <summary>
/// The block atlas, painted at start-up from tileable noise: no image files, nothing licensed. Grayscale-ish tiles (grass top, leaves,
/// water) are multiplied by the biome tint maps; the rest are baked in the muted, weathered palette of the game.
/// </summary>
public static class BlockTextures
{
    public const int TileSize = 64;
    public const int TilesPerRow = 8;

    public static int AtlasWidth => TilesPerRow * TileSize;
    public static int AtlasHeight => ((BlockTiles.Count + TilesPerRow - 1) / TilesPerRow) * TileSize;

    /// <summary>The RGBA8 atlas image, <see cref="AtlasWidth"/> x <see cref="AtlasHeight"/>.</summary>
    public static byte[] Build()
    {
        var rgba = new byte[AtlasWidth * AtlasHeight * 4];
        Paint(rgba, BlockTiles.GrassTop, GrassTop);
        Paint(rgba, BlockTiles.GrassSide, GrassSide);
        Paint(rgba, BlockTiles.Dirt, (u, v) => new Vector4(Dirt(u, v), 1f));
        Paint(rgba, BlockTiles.Stone, (u, v) => new Vector4(Stone(u, v), 1f));
        Paint(rgba, BlockTiles.Sand, (u, v) => new Vector4(Sand(u, v), 1f));
        Paint(rgba, BlockTiles.LogSide, (u, v) => new Vector4(LogSide(u, v), 1f));
        Paint(rgba, BlockTiles.LogTop, (u, v) => new Vector4(LogTop(u, v), 1f));
        Paint(rgba, BlockTiles.Leaves, Leaves);
        for (int k = 0; k < BlockTiles.WaterFrames; k++)
        {
            int frame = k;
            Paint(rgba, BlockTiles.Water0 + k, (u, v) => new Vector4(Water(u, v, frame), 1f));
        }
        return rgba;
    }

    /// <summary>The animation of the water tile: eight frames, three ticks each (20 ticks a second).</summary>
    public static int[] WaterTimeline()
    {
        var t = new int[BlockTiles.WaterFrames * 3];
        for (int i = 0; i < t.Length; i++) t[i] = BlockTiles.Water0 + i / 3;
        return t;
    }

    private static void Paint(byte[] rgba, int tile, Func<float, float, Vector4> f)
    {
        int tx = (tile % TilesPerRow) * TileSize, ty = (tile / TilesPerRow) * TileSize;
        for (int y = 0; y < TileSize; y++)
        for (int x = 0; x < TileSize; x++)
        {
            var c = f((x + 0.5f) / TileSize, (y + 0.5f) / TileSize);
            int i = ((ty + y) * AtlasWidth + tx + x) * 4;
            rgba[i] = ToByte(c.X);
            rgba[i + 1] = ToByte(c.Y);
            rgba[i + 2] = ToByte(c.Z);
            rgba[i + 3] = ToByte(c.W);
        }
    }

    private static byte ToByte(float v) => (byte)Math.Clamp((int)(v * 255f + 0.5f), 0, 255);

    private static Vector3 Mix(Vector3 a, Vector3 b, float t) => a + (b - a) * Math.Clamp(t, 0f, 1f);

    // --- grass: top is grayscale (the biome tint colours it), the side carries a baked fringe ---

    private static float GrassLuma(float u, float v)
    {
        float broad = Fbm(u, v, 4, 4, 4, 11);
        float blades = Value(u, v, 48, 48, 12) * 0.6f + Value(u, v, 24, 24, 13) * 0.4f;
        float l = 0.56f + 0.34f * broad + 0.20f * (blades - 0.5f);
        if (Value(u, v, 8, 8, 14) > 0.72f) l *= 0.84f;          // darker clumps
        float tip = Value(u, v, 64, 64, 15);
        if (tip > 0.93f) l += 0.12f;                              // sunlit blade tips
        return l;
    }

    private static Vector4 GrassTop(float u, float v)
    {
        float l = GrassLuma(u, v);
        return new Vector4(l, l, l * 0.9f, 1f);
    }

    private static Vector4 GrassSide(float u, float v)
    {
        // The fringe edge wanders along the tile, with blades hanging lower in places
        float edge = 0.17f + 0.13f * Value(u, 0.31f, 16, 1, 21) + 0.10f * Value(u, 0.6f, 64, 1, 22);
        float luma = GrassLuma(u, v);
        // Baked colour near what the tinted top comes to, so the fringe and the top read as one grass
        var grass = new Vector3(0.40f, 0.52f, 0.24f) * (0.62f + 0.62f * luma);
        var dirt = Dirt(u, v);
        // A soft shadow under the grass
        float under = 1f - 0.28f * (1f - SmoothStep(edge, edge + 0.16f, v));
        if (v < edge) return new Vector4(grass, 1f);
        return new Vector4(dirt * under, 1f);
    }

    // --- earth ---

    private static Vector3 Dirt(float u, float v)
    {
        var baseColor = new Vector3(0.37f, 0.27f, 0.18f);
        float broad = Fbm(u, v, 4, 4, 4, 31);
        float grain = Value(u, v, 64, 64, 32) - 0.5f;
        var c = baseColor * (0.66f + 0.60f * broad) + new Vector3(grain * 0.10f);
        var (f1, _) = Cell(u, v, 9, 33);
        if (f1 < 0.12f) c = Mix(c, new Vector3(0.52f, 0.47f, 0.40f), 0.8f * (1f - f1 / 0.12f)); // small stones
        if (Value(u, v, 24, 24, 34) > 0.9f) c *= 0.55f;                                         // roots, rot
        return c;
    }

    private static Vector3 Stone(float u, float v)
    {
        var baseColor = new Vector3(0.46f, 0.46f, 0.48f);
        float broad = Fbm(u, v, 3, 3, 5, 41);
        var c = baseColor * (0.62f + 0.72f * broad);
        var (f1, f2) = Cell(u, v, 4, 42);
        float crack = SmoothStep(0.0f, 0.08f, f2 - f1);
        c *= 0.50f + 0.50f * crack;                                                              // cracks between the plates
        float fleck = Value(u, v, 64, 64, 43);
        if (fleck > 0.86f) c += new Vector3(0.10f);                                              // mica
        else if (fleck < 0.08f) c *= 0.7f;
        float lichen = Fbm(u, v, 3, 3, 3, 44);
        if (lichen > 0.62f) c = Mix(c, new Vector3(0.38f, 0.45f, 0.30f), (lichen - 0.62f) * 2.4f); // lichen
        return c;
    }

    private static Vector3 Sand(float u, float v)
    {
        var baseColor = new Vector3(0.68f, 0.61f, 0.45f);
        float grain = Value(u, v, 64, 64, 51) - 0.5f;
        float wave = MathF.Sin((u * 3f + v * 2f + 0.35f * Fbm(u, v, 4, 4, 3, 52)) * MathF.Tau) * 0.5f + 0.5f;
        var c = baseColor * (0.88f + 0.10f * wave) + new Vector3(grain * 0.12f);
        if (Value(u, v, 40, 40, 53) > 0.93f) c *= 0.7f;
        return c;
    }

    // --- wood ---

    private static Vector3 LogSide(float u, float v)
    {
        // Bark: long vertical ridges and grooves
        float ridge = Fbm(u, v, 10, 2, 4, 61);
        float groove = Value(u, v, 14, 3, 62);
        var c = new Vector3(0.34f, 0.25f, 0.17f) * (0.55f + 0.85f * ridge);
        if (groove < 0.28f) c *= 0.55f;
        float moss = Fbm(u, v, 3, 3, 3, 63);
        if (moss > 0.66f) c = Mix(c, new Vector3(0.28f, 0.36f, 0.20f), (moss - 0.66f) * 2.2f);
        return c;
    }

    private static Vector3 LogTop(float u, float v)
    {
        float dx = u - 0.5f, dy = v - 0.5f;
        float d = MathF.Sqrt(dx * dx + dy * dy);
        if (d > 0.45f)
        {
            // Bark around the cut face
            return LogSide(u, v) * 0.9f;
        }
        float wobble = 0.05f * Fbm(u, v, 3, 3, 3, 71);
        float rings = MathF.Sin((d + wobble) * MathF.Tau * 6.5f) * 0.5f + 0.5f;
        var light = new Vector3(0.66f, 0.51f, 0.32f);
        var dark = new Vector3(0.48f, 0.35f, 0.20f);
        var c = Mix(light, dark, rings * rings);
        return c * (0.9f + 0.2f * Value(u, v, 48, 48, 72));
    }

    // --- foliage: a cutout (holes let light through), grayscale for the foliage tint ---

    private static Vector4 Leaves(float u, float v)
    {
        float clump = Value(u, v, 8, 8, 81) * 0.55f + Value(u, v, 16, 16, 82) * 0.30f + Value(u, v, 32, 32, 83) * 0.15f;
        if (clump < 0.36f) return new Vector4(0f, 0f, 0f, 0f);
        float l = 0.52f + 0.55f * (Value(u, v, 12, 12, 84) - 0.4f);
        // Edges of a clump are in shadow, its middle catches the light
        l *= 0.72f + 0.55f * SmoothStep(0.36f, 0.66f, clump);
        return new Vector4(l, l * 1.0f, l * 0.9f, 1f);
    }

    // --- water: a seamless loop of caustic ripples ---

    private static Vector3 Water(float u, float v, int frame)
    {
        // The lowest octave has 4 cells across: moving 1/4 of the tile in 8 frames is one whole period, so frame 8 is frame 0
        float shift = frame / 32f;
        float a = Fbm(u + shift, v, 4, 4, 3, 91);
        // The second layer drifts the other way, also a whole period over the loop (any other factor would break the seam)
        float b = Fbm(u, v - shift, 4, 4, 3, 92);
        float n = (a + b) * 0.5f;
        float ridge = 1f - MathF.Abs(2f * n - 1f);
        float caustic = MathF.Pow(ridge, 6f);
        float l = 0.66f + 0.20f * n + 0.30f * caustic;
        return new Vector3(l * 0.95f, l, l);
    }
}
