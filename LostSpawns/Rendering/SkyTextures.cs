using System.Numerics;
using static LostSpawns.Rendering.TileNoise;

namespace LostSpawns.Rendering;

/// <summary>
/// The images of the sky and its weather, painted at start-up: one atlas holding the sun, the eight phases of the moon, the cloud
/// map, the rain streaks and the snow flakes. Each rect is x, y, width, height in atlas pixels; the moon's rect is the first
/// phase (the others follow to the right, four to a row).
/// </summary>
public static class SkyTextures
{
    public const int Width = 512;
    public const int Height = 640;

    public static readonly int[] Sun = { 0, 0, 128, 128 };
    public static readonly int[] Moon = { 0, 128, 128, 128 };
    public static readonly int[] Clouds = { 0, 384, 256, 256 };
    public static readonly int[] Rain = { 256, 384, 64, 128 };
    public static readonly int[] Snow = { 320, 384, 64, 64 };

    public static byte[] Build()
    {
        var rgba = new byte[Width * Height * 4];
        Paint(rgba, Sun, (u, v) => SunTexel(u, v));
        for (int phase = 0; phase < 8; phase++)
        {
            int p = phase;
            var rect = new[] { Moon[0] + (p % 4) * Moon[2], Moon[1] + (p / 4) * Moon[3], Moon[2], Moon[3] };
            Paint(rgba, rect, (u, v) => MoonTexel(u, v, p));
        }
        Paint(rgba, Clouds, CloudTexel);
        Paint(rgba, Rain, RainTexel);
        Paint(rgba, Snow, SnowTexel);
        return rgba;
    }

    private static void Paint(byte[] rgba, int[] rect, Func<float, float, Vector4> f)
    {
        for (int y = 0; y < rect[3]; y++)
        for (int x = 0; x < rect[2]; x++)
        {
            var c = f((x + 0.5f) / rect[2], (y + 0.5f) / rect[3]);
            int i = ((rect[1] + y) * Width + rect[0] + x) * 4;
            rgba[i] = (byte)Math.Clamp((int)(c.X * 255f + 0.5f), 0, 255);
            rgba[i + 1] = (byte)Math.Clamp((int)(c.Y * 255f + 0.5f), 0, 255);
            rgba[i + 2] = (byte)Math.Clamp((int)(c.Z * 255f + 0.5f), 0, 255);
            rgba[i + 3] = (byte)Math.Clamp((int)(c.W * 255f + 0.5f), 0, 255);
        }
    }

    // A bright disc with a soft rim and a wide corona; drawn additively over the sky
    private static Vector4 SunTexel(float u, float v)
    {
        float dx = u * 2f - 1f, dy = v * 2f - 1f;
        float r = MathF.Sqrt(dx * dx + dy * dy);
        float core = 1f - SmoothStep(0.38f, 0.46f, r);
        float corona = MathF.Pow(Math.Clamp(1f - r, 0f, 1f), 3f) * 0.55f;
        float a = Math.Clamp(core + corona, 0f, 1f);
        var color = Vector3.Lerp(new Vector3(1.0f, 0.78f, 0.45f), new Vector3(1.0f, 0.97f, 0.86f), core);
        return new Vector4(color, a);
    }

    // The moon: a sphere lit from the side the phase says, with maria and craters
    private static Vector4 MoonTexel(float u, float v, int phase)
    {
        float dx = u * 2f - 1f, dy = v * 2f - 1f;
        float r2 = dx * dx + dy * dy;
        const float radius = 0.62f;
        float rr = MathF.Sqrt(r2) / radius;
        if (rr >= 1.02f) return new Vector4(0, 0, 0, 0);
        float edge = 1f - SmoothStep(0.96f, 1.0f, rr);
        float z = MathF.Sqrt(MathF.Max(1f - rr * rr, 0f));
        var n = new Vector3(dx / radius, dy / radius, z);
        // Phase 0 is full (lit from the viewer's side), 4 is new (lit from behind); 1-3 wane from one side, 5-7 wax from the other
        float a = phase * MathF.PI / 4f;
        var light = new Vector3(MathF.Sin(a) * -1f, 0f, MathF.Cos(a));
        float lit = SmoothStep(-0.05f, 0.18f, Vector3.Dot(n, light));

        float maria = Fbm(u, v, 3, 3, 4, 301);
        float crater = 0f;
        var (f1, f2) = Cell(u, v, 7, 302);
        if (f1 < 0.18f) crater = 0.10f * (1f - f1 / 0.18f);
        float surf = 0.86f - 0.30f * SmoothStep(0.45f, 0.62f, maria) - crater + 0.05f * (f2 - f1);
        var color = new Vector3(surf, surf * 0.98f, surf * 0.92f);
        // The dark side is just visible against the sky (earthshine)
        float shade = 0.06f + 0.94f * lit;
        return new Vector4(color * shade, edge * (0.06f + 0.94f * MathF.Max(lit, 0.0f)) + edge * 0.04f);
    }

    // A map of clouds: one pixel is 12 blocks, the map repeats; alpha is how much of the sky the cloud takes
    private static Vector4 CloudTexel(float u, float v)
    {
        float n = Fbm(u, v, 5, 5, 5, 401);
        float detail = Value(u, v, 64, 64, 402);
        float d = SmoothStep(0.50f, 0.70f, n + (detail - 0.5f) * 0.10f);
        return new Vector4(1f, 1f, 1f, d);
    }

    // Falling rain: thin streaks of different lengths that fall down the quad
    private static Vector4 RainTexel(float u, float v)
    {
        int px = (int)(u * Rain[2]);
        int py = (int)(v * Rain[3]);
        // Each streak lives in one of 16 columns of 4 pixels, one pixel wide
        int col = px / 4;
        if (px % 4 != 1) return new Vector4(0, 0, 0, 0);
        float start = Rand(col, 0, 411) * Rain[3];
        float length = 22f + Rand(col, 1, 412) * 34f;
        float along = ((py - start) % Rain[3] + Rain[3]) % Rain[3];
        if (along > length) return new Vector4(0, 0, 0, 0);
        float a = (along / length);
        return new Vector4(0.72f, 0.78f, 0.88f, a * a * 0.8f);
    }

    // Snowflakes: soft dots
    private static Vector4 SnowTexel(float u, float v)
    {
        const int cells = 8;
        float x = u * cells, y = v * cells;
        int cx = (int)x, cy = (int)y;
        float fx = Rand(cx, cy, 421) * 0.6f + 0.2f, fy = Rand(cx, cy, 422) * 0.6f + 0.2f;
        float dx = x - cx - fx, dy = y - cy - fy;
        float r = MathF.Sqrt(dx * dx + dy * dy);
        float a = 1f - SmoothStep(0.10f, 0.22f, r);
        return new Vector4(1f, 1f, 1f, a * 0.9f);
    }
}
