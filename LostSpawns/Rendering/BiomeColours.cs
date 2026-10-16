using System.Numerics;
using SpawnDev.VoxelEngine.Rendering;
using static LostSpawns.Rendering.TileNoise;

namespace LostSpawns.Rendering;

/// <summary>
/// The colour of grass, leaves and water across the world: three 256 x 256 maps, one texel for every 4 x 4 blocks, which repeat
/// every 1,024 blocks. Painted from large-scale noise so the land is not one green: dark spruce floor, lush meadow, dry olive
/// grass, a little rust in the leaves, and clear teal water that deepens in patches. The blocks multiply these into their
/// grayscale textures, and the pipeline blends between texels, so the borders are soft.
/// </summary>
public static class BiomeColours
{
    private const int N = BiomeTintMap.CellsAcross;

    public static void Fill(BiomeTintMap map)
    {
        var grass = new byte[N * N * 4];
        var foliage = new byte[N * N * 4];
        var water = new byte[N * N * 4];
        for (int y = 0; y < N; y++)
        for (int x = 0; x < N; x++)
        {
            float u = (x + 0.5f) / N, v = (y + 0.5f) / N;
            float region = Fbm(u, v, 3, 3, 4, 501);          // patches a few hundred blocks across
            float fine = Fbm(u, v, 12, 12, 3, 502);          // down to a few cells
            float wet = Fbm(u, v, 4, 4, 3, 503);

            var dark = new Vector3(0.26f, 0.37f, 0.20f);     // under the spruce
            var lush = new Vector3(0.33f, 0.46f, 0.22f);     // meadow
            var dry = new Vector3(0.50f, 0.52f, 0.29f);      // dry olive grass
            var g = region < 0.45f ? Vector3.Lerp(dark, lush, SmoothStep(0.30f, 0.45f, region))
                                   : Vector3.Lerp(lush, dry, SmoothStep(0.52f, 0.68f, region));
            g *= 0.92f + 0.16f * fine;

            var spruce = new Vector3(0.21f, 0.35f, 0.19f);
            var fresh = new Vector3(0.33f, 0.46f, 0.20f);
            var rust = new Vector3(0.54f, 0.40f, 0.17f);
            var f = Vector3.Lerp(spruce, fresh, SmoothStep(0.35f, 0.65f, region));
            f = Vector3.Lerp(f, rust, 0.55f * SmoothStep(0.66f, 0.80f, fine));

            var shallow = new Vector3(0.22f, 0.45f, 0.52f);
            var deep = new Vector3(0.10f, 0.28f, 0.42f);
            var w = Vector3.Lerp(shallow, deep, SmoothStep(0.35f, 0.65f, wet));

            int i = (y * N + x) * 4;
            Put(grass, i, g);
            Put(foliage, i, f);
            Put(water, i, w);
        }
        map.Grass.WriteRegion(0, 0, N, N, grass);
        map.Foliage.WriteRegion(0, 0, N, N, foliage);
        map.Water.WriteRegion(0, 0, N, N, water);
    }

    private static void Put(byte[] px, int i, Vector3 c)
    {
        px[i] = (byte)Math.Clamp((int)(c.X * 255f + 0.5f), 0, 255);
        px[i + 1] = (byte)Math.Clamp((int)(c.Y * 255f + 0.5f), 0, 255);
        px[i + 2] = (byte)Math.Clamp((int)(c.Z * 255f + 0.5f), 0, 255);
        px[i + 3] = 255;
    }
}
