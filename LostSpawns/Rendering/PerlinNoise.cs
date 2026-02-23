namespace LostSpawns.Rendering;

/// <summary>
/// Classic 2D Perlin noise with octave support. Seeded for deterministic world generation.
/// </summary>
public class PerlinNoise
{
    private readonly int[] _perm;

    /// <summary>The 512-entry permutation table for GPU upload.</summary>
    public int[] PermTable => _perm;

    public PerlinNoise(int seed)
    {
        var rng = new Random(seed);
        _perm = new int[512];
        var p = Enumerable.Range(0, 256).ToArray();
        for (int i = 255; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (p[i], p[j]) = (p[j], p[i]);
        }
        for (int i = 0; i < 512; i++)
            _perm[i] = p[i & 255];
    }

    private static float Fade(float t) => t * t * t * (t * (t * 6 - 15) + 10);
    private static float Lerp(float a, float b, float t) => a + t * (b - a);

    private static float Grad(int hash, float x, float y)
    {
        int h = hash & 3;
        float u = h < 2 ? x : y;
        float v = h < 2 ? y : x;
        return ((h & 1) == 0 ? u : -u) + ((h & 2) == 0 ? v : -v);
    }

    /// <summary>Returns noise value in roughly [-1, 1] range.</summary>
    public float Noise2D(float x, float y)
    {
        int xi = (int)MathF.Floor(x) & 255;
        int yi = (int)MathF.Floor(y) & 255;
        float xf = x - MathF.Floor(x);
        float yf = y - MathF.Floor(y);

        float u = Fade(xf);
        float v = Fade(yf);

        int aa = _perm[_perm[xi] + yi];
        int ab = _perm[_perm[xi] + yi + 1];
        int ba = _perm[_perm[xi + 1] + yi];
        int bb = _perm[_perm[xi + 1] + yi + 1];

        return Lerp(
            Lerp(Grad(aa, xf, yf), Grad(ba, xf - 1, yf), u),
            Lerp(Grad(ab, xf, yf - 1), Grad(bb, xf - 1, yf - 1), u),
            v);
    }

    /// <summary>Multi-octave noise for natural terrain variation.</summary>
    public float OctaveNoise(float x, float y, int octaves, float persistence = 0.5f, float lacunarity = 2f)
    {
        float total = 0, amplitude = 1, frequency = 1, maxValue = 0;
        for (int i = 0; i < octaves; i++)
        {
            total += Noise2D(x * frequency, y * frequency) * amplitude;
            maxValue += amplitude;
            amplitude *= persistence;
            frequency *= lacunarity;
        }
        return total / maxValue;
    }
}
