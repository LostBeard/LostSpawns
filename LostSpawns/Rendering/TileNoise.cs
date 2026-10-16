namespace LostSpawns.Rendering;

/// <summary>
/// Seamlessly tileable noise for the procedural textures. Every function takes (u, v) in [0, 1) and a lattice
/// period, and wraps at 1, so a tile repeats across a merged quad with no seam.
/// </summary>
internal static class TileNoise
{
    private static uint Hash(int x, int y, int seed)
    {
        uint h = (uint)x * 374761393u + (uint)y * 668265263u + (uint)seed * 2246822519u;
        h = (h ^ (h >> 13)) * 1274126177u;
        return h ^ (h >> 16);
    }

    /// <summary>A repeatable random value in [0, 1) for a lattice point.</summary>
    public static float Rand(int x, int y, int seed) => (Hash(x, y, seed) & 0xFFFFFF) / 16777216f;

    private static float Smooth(float t) => t * t * (3f - 2f * t);

    private static int Wrap(int i, int n) => ((i % n) + n) % n;

    /// <summary>Value noise in [0, 1], periodic: <paramref name="px"/> x <paramref name="py"/> lattice cells across the tile.</summary>
    public static float Value(float u, float v, int px, int py, int seed)
    {
        float x = u * px, y = v * py;
        int x0 = (int)MathF.Floor(x), y0 = (int)MathF.Floor(y);
        float fx = Smooth(x - x0), fy = Smooth(y - y0);
        int xa = Wrap(x0, px), xb = Wrap(x0 + 1, px), ya = Wrap(y0, py), yb = Wrap(y0 + 1, py);
        float a = Rand(xa, ya, seed), b = Rand(xb, ya, seed), c = Rand(xa, yb, seed), d = Rand(xb, yb, seed);
        return (a + (b - a) * fx) + ((c + (d - c) * fx) - (a + (b - a) * fx)) * fy;
    }

    /// <summary>Fractal sum of value noise (each octave doubles the lattice), in [0, 1].</summary>
    public static float Fbm(float u, float v, int px, int py, int octaves, int seed, float gain = 0.5f)
    {
        float sum = 0f, amp = 1f, norm = 0f;
        for (int o = 0; o < octaves; o++)
        {
            sum += amp * Value(u, v, px << o, py << o, seed + o * 31);
            norm += amp;
            amp *= gain;
        }
        return sum / norm;
    }

    /// <summary>
    /// Cellular (Worley) noise on a periodic <paramref name="p"/> x <paramref name="p"/> grid: the distances to the nearest
    /// and second nearest feature point, in cell units. f2 - f1 is near zero along the borders of the cells (cracks).
    /// </summary>
    public static (float f1, float f2) Cell(float u, float v, int p, int seed)
    {
        float x = u * p, y = v * p;
        int cx = (int)MathF.Floor(x), cy = (int)MathF.Floor(y);
        float f1 = 9f, f2 = 9f;
        for (int j = -1; j <= 1; j++)
        for (int i = -1; i <= 1; i++)
        {
            int gx = cx + i, gy = cy + j;
            int wx = Wrap(gx, p), wy = Wrap(gy, p);
            float px = gx + Rand(wx, wy, seed), py = gy + Rand(wx, wy, seed + 7);
            float dx = px - x, dy = py - y;
            float d = MathF.Sqrt(dx * dx + dy * dy);
            if (d < f1) { f2 = f1; f1 = d; }
            else if (d < f2) f2 = d;
        }
        return (f1, f2);
    }

    public static float SmoothStep(float e0, float e1, float x)
    {
        float t = Math.Clamp((x - e0) / (e1 - e0), 0f, 1f);
        return t * t * (3f - 2f * t);
    }

    public static float Lerp(float a, float b, float t) => a + (b - a) * t;
}
