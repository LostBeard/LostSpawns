using System.Numerics;
using static LostSpawns.Rendering.TileNoise;

namespace LostSpawns.Rendering;

/// <summary>Tile numbers in the block atlas (row-major, as <c>BlockMaterial</c> faces name them).</summary>
public static class BlockTiles
{
    public const int GrassTop = 0, GrassSide = 1, Dirt = 2, Stone = 3, Sand = 4, LogSide = 5, LogTop = 6, Leaves = 7;
    /// <summary>Eight frames of the water surface, a seamless loop.</summary>
    public const int Water0 = 8, WaterFrames = 8;
    public const int Gravel = 16, Granite = 17, TallGrass = 18, Fern = 19, Dandelion = 20, Aster = 21;
    public const int Count = 22;
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
        Paint(rgba, BlockTiles.Gravel, (u, v) => new Vector4(Gravel(u, v), 1f));
        Paint(rgba, BlockTiles.Granite, (u, v) => new Vector4(Granite(u, v), 1f));
        Paint(rgba, BlockTiles.TallGrass, GrassTuft);
        Paint(rgba, BlockTiles.Fern, FernFrond);
        Paint(rgba, BlockTiles.Dandelion, (u, v) => Flower(u, v, 601, new Vector3(1.0f, 0.85f, 0.15f), new Vector3(0.95f, 0.62f, 0.05f), 9, 0.10f));
        Paint(rgba, BlockTiles.Aster, (u, v) => Flower(u, v, 602, new Vector3(0.72f, 0.52f, 0.92f), new Vector3(1.0f, 0.86f, 0.20f), 12, 0.115f));
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
        float l = 0.74f + 0.55f * (Value(u, v, 12, 12, 84) - 0.4f);
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

    // --- rock and pebbles ---

    private static Vector3 Gravel(float u, float v)
    {
        // Pebbles: each cell of a cellular pattern is one stone with its own grey-brown tone
        var (f1, f2) = Cell(u, v, 7, 701);
        float edge = SmoothStep(0.0f, 0.10f, f2 - f1);
        float tone = Rand((int)(u * 7 + f1 * 3), (int)(v * 7 + f2 * 3), 702);
        var stone = Mix(new Vector3(0.40f, 0.38f, 0.36f), new Vector3(0.66f, 0.62f, 0.56f), tone);
        float lit = 0.80f + 0.30f * (1f - f1);
        var c = stone * lit * (0.55f + 0.45f * edge);
        c += new Vector3((Value(u, v, 64, 64, 703) - 0.5f) * 0.08f);
        return c;
    }

    private static Vector3 Granite(float u, float v)
    {
        // Pinkish-grey coastal granite: coarse speckle of dark and light grains, broad blotches, a few cracks
        float broad = Fbm(u, v, 3, 3, 4, 711);
        var baseColor = Mix(new Vector3(0.50f, 0.47f, 0.46f), new Vector3(0.62f, 0.55f, 0.52f), Fbm(u, v, 2, 2, 2, 712));
        var c = baseColor * (0.70f + 0.55f * broad);
        float grain = Value(u, v, 40, 40, 713);
        if (grain > 0.80f) c = Mix(c, new Vector3(0.80f, 0.72f, 0.68f), 0.6f);
        else if (grain < 0.16f) c = Mix(c, new Vector3(0.16f, 0.16f, 0.18f), 0.55f);
        var (f1, f2) = Cell(u, v, 3, 714);
        c *= 0.58f + 0.42f * SmoothStep(0.0f, 0.05f, f2 - f1);
        float lichen = Fbm(u, v, 4, 4, 3, 715);
        if (lichen > 0.66f) c = Mix(c, new Vector3(0.50f, 0.55f, 0.34f), (lichen - 0.66f) * 2.0f);
        return c;
    }

    // --- plants: crossed quads showing a sprite on a transparent tile ---

    // Distance from point p to the segment a-b
    private static float SegmentDistance(Vector2 p, Vector2 a, Vector2 b)
    {
        var ab = b - a;
        float t = Math.Clamp(Vector2.Dot(p - a, ab) / MathF.Max(Vector2.Dot(ab, ab), 1e-6f), 0f, 1f);
        return Vector2.Distance(p, a + ab * t);
    }

    private static Vector4 GrassTuft(float u, float v)
    {
        // Blades rooted along the bottom edge: each a tapering, leaning curve of its own height and tone. Grayscale: the biome tint colours it.
        float best = 0f;
        bool hit = false;
        for (int i = 0; i < 15; i++)
        {
            float bx = 0.10f + 0.80f * Rand(i, 0, 801);
            float h = 0.45f + 0.50f * Rand(i, 1, 802);
            float lean = (Rand(i, 2, 803) - 0.5f) * 0.38f;
            float t = (1f - v) / h;                // 0 at the ground, 1 at the tip
            if (t < 0f || t > 1f) continue;
            float xc = bx + lean * t * t;
            float half = 0.030f * (1f - t) + 0.004f;
            if (MathF.Abs(u - xc) > half) continue;
            float tone = 0.72f + 0.55f * t + 0.25f * (Rand(i, 3, 804) - 0.5f);
            if (!hit || tone > best) best = tone;
            hit = true;
        }
        if (!hit) return new Vector4(0f, 0f, 0f, 0f);
        float l = Math.Clamp(best, 0.5f, 1.25f);
        return new Vector4(l, l, l * 0.9f, 1f);
    }

    private static Vector4 FernFrond(float u, float v)
    {
        var p = new Vector2(u, v);
        var root = new Vector2(0.5f, 1.0f);
        float bestD = 9f;
        float shade = 0f;
        for (int f = 0; f < 6; f++)
        {
            // Fronds fan out from the root, each arching over to a side
            float ang = (-0.95f + 1.9f * f / 5f) + (Rand(f, 0, 811) - 0.5f) * 0.18f;
            float len = 0.62f + 0.22f * Rand(f, 1, 812);
            var dir = new Vector2(MathF.Sin(ang), -MathF.Cos(ang));
            var bend = new Vector2((ang < 0f ? -1f : 1f) * 0.12f, 0.10f);
            Vector2 At(float t) => root + dir * len * t + bend * t * t * len;
            // The midrib
            Vector2 prev = root;
            for (int seg = 1; seg <= 8; seg++)
            {
                var q = At(seg / 8f);
                float d = SegmentDistance(p, prev, q) - 0.011f;
                if (d < bestD) { bestD = d; shade = 0.55f + 0.2f * (seg / 8f); }
                prev = q;
            }
            // Leaflets on both sides, shorter toward the tip
            for (int k = 1; k <= 8; k++)
            {
                float t = k / 9f;
                var c = At(t);
                var next = At(MathF.Min(t + 0.05f, 1f));
                var tangent = Vector2.Normalize(next - c);
                var normal = new Vector2(-tangent.Y, tangent.X);
                float size = 0.17f * (1f - t * 0.75f);
                for (int side = -1; side <= 1; side += 2)
                {
                    var tip = c + (normal * side + tangent * 0.55f) * size;
                    float d = SegmentDistance(p, c, tip) - 0.016f * (1f - t * 0.5f);
                    if (d < bestD) { bestD = d; shade = 0.70f + 0.35f * t; }
                }
            }
        }
        if (bestD > 0f) return new Vector4(0f, 0f, 0f, 0f);
        float l = shade;
        return new Vector4(l, l, l * 0.9f, 1f);
    }

    private static Vector4 Flower(float u, float v, int seed, Vector3 petals, Vector3 center, int petalCount, float headRadius)
    {
        var p = new Vector2(u, v);
        float sway = (Rand(1, 1, seed) - 0.5f) * 0.10f;
        var root = new Vector2(0.5f, 1.0f);
        var head = new Vector2(0.5f + sway, 0.40f);
        // The stem and two leaves
        float d = SegmentDistance(p, root, head) - 0.013f;
        if (d < 0f) return new Vector4(0.24f, 0.45f, 0.16f, 1f);
        for (int side = -1; side <= 1; side += 2)
        {
            var from = Vector2.Lerp(root, head, 0.25f);
            var to = from + new Vector2(side * 0.22f, -0.13f);
            if (SegmentDistance(p, from, to) < 0.018f) return new Vector4(0.28f, 0.50f, 0.18f, 1f);
        }
        // The head: petals around a centre
        var rel = p - head;
        float r = rel.Length();
        if (r < headRadius * 0.45f) return new Vector4(center, 1f);
        if (r < headRadius * 1.6f)
        {
            float a = MathF.Atan2(rel.Y, rel.X);
            float lobe = 0.5f + 0.5f * MathF.Cos(a * petalCount);
            float edge = headRadius * (1.0f + 0.6f * lobe);
            if (r < edge) return new Vector4(petals * (0.85f + 0.2f * lobe), 1f);
        }
        return new Vector4(0f, 0f, 0f, 0f);
    }
}
