using System.Numerics;

namespace LostSpawns.Rendering;

/// <summary>
/// The light of one moment: where the sun is, what colour it and the ambient light are, what the fog and the sky look like.
/// Computed from the game clock (<see cref="WorldTimeService.DayFraction"/>) and the weather, and handed to the block pipeline
/// and the sky pipeline, so the ground, the creatures and the sky are lit by the same numbers.
/// </summary>
public readonly record struct Atmosphere(
    Vector3 SunDirection, Vector3 SunColor, float SunStrength, Vector3 Ambient,
    Vector3 Fog, Vector3 Zenith, float Daylight, float Night, Vector3 CloudColor)
{
    /// <summary>
    /// The game's clock starts a day at the first light (sunrise at 0.08, sunset at 0.58); the sky model counts 0 = midnight,
    /// 0.25 = sunrise, 0.5 = noon, 0.75 = sunset. Shifting by 0.17 puts the two on top of each other.
    /// </summary>
    public static float SkyTime(float dayFraction) => (dayFraction + 0.17f) % 1f;

    public static Atmosphere At(float dayFraction, float rain, float thunder)
    {
        float t = SkyTime(dayFraction);
        float angle = t * MathF.Tau;
        float elevation = -MathF.Cos(angle);            // 1 at noon, -1 at midnight
        float sx = MathF.Sin(angle) * 0.7f;
        float len = MathF.Sqrt(sx * sx + elevation * elevation + 0.35f * 0.35f);
        var sun = new Vector3(sx / len, elevation / len, 0.35f / len);

        float dayFactor = Math.Clamp(elevation * 2f + 0.5f, 0f, 1f);
        float dawnFactor = Math.Clamp(1f - MathF.Abs(elevation) * 4f, 0f, 1f);

        float ar = Lerp(0.05f, 0.30f, dayFactor); ar = Lerp(ar, 0.25f, dawnFactor * 0.6f);
        float ag = Lerp(0.06f, 0.32f, dayFactor); ag = Lerp(ag, 0.18f, dawnFactor * 0.6f);
        float ab = Lerp(0.12f, 0.38f, dayFactor); ab = Lerp(ab, 0.12f, dawnFactor * 0.6f);

        float sr = Lerp(0.15f, 1.0f, dayFactor); sr = Lerp(sr, 1.0f, dawnFactor * 0.8f);
        float sg = Lerp(0.18f, 0.95f, dayFactor); sg = Lerp(sg, 0.55f, dawnFactor * 0.8f);
        float sb = Lerp(0.35f, 0.85f, dayFactor); sb = Lerp(sb, 0.25f, dawnFactor * 0.8f);

        float fr = Lerp(0.04f, 0.62f, dayFactor); fr = Lerp(fr, 0.85f, dawnFactor * 0.7f);
        float fg = Lerp(0.05f, 0.76f, dayFactor); fg = Lerp(fg, 0.50f, dawnFactor * 0.7f);
        float fb = Lerp(0.10f, 0.92f, dayFactor); fb = Lerp(fb, 0.30f, dawnFactor * 0.7f);
        float sunStrength = Math.Clamp(dayFactor * 0.62f + 0.05f, 0.05f, 0.62f);

        float daylight = Smooth(-0.12f, 0.28f, elevation);
        float night = 1f - Smooth(-0.3f, 0.02f, elevation);
        float dawn = Math.Clamp(1f - MathF.Abs(elevation) * 5f, 0f, 1f);
        var zenith = Vector3.Lerp(new Vector3(0.012f, 0.018f, 0.045f), new Vector3(0.40f, 0.60f, 0.98f), daylight);
        zenith = Vector3.Lerp(zenith, new Vector3(0.30f, 0.34f, 0.58f), dawn * 0.55f);
        var cloud = Vector3.Lerp(new Vector3(0.10f, 0.10f, 0.14f), Vector3.One, daylight);
        cloud = Vector3.Lerp(cloud, new Vector3(1.0f, 0.72f, 0.6f), dawn * 0.5f);

        var sunColor = new Vector3(sr, sg, sb);
        var ambient = new Vector3(ar, ag, ab);
        var fog = new Vector3(fr, fg, fb);

        rain = Math.Clamp(rain, 0f, 1f);
        thunder = Math.Clamp(thunder, 0f, 1f) * rain;
        if (rain > 0f)
        {
            // How bright the day is decides how bright the cloud is: a rainy night is still night
            float day = Math.Clamp(sunStrength / 0.6f, 0f, 1f);
            float shade = (0.12f + 0.88f * daylight) * (1f - 0.45f * thunder);
            var cover = new Vector3(0.52f, 0.54f, 0.58f) * (0.18f + 0.82f * day) * (1f - 0.45f * thunder);
            fog = Vector3.Lerp(fog, cover, 0.8f * rain);
            sunStrength = Math.Max(sunStrength * (1f - 0.62f * rain - 0.2f * thunder), 0.03f);
            sunColor = Vector3.Lerp(sunColor, new Vector3(0.7f, 0.72f, 0.76f), 0.7f * rain);
            ambient *= 1f - 0.18f * rain - 0.12f * thunder;
            zenith = Vector3.Lerp(zenith, new Vector3(0.42f, 0.44f, 0.48f) * shade, 0.85f * rain);
            cloud = Vector3.Lerp(cloud, new Vector3(0.56f, 0.58f, 0.62f) * shade, 0.85f * rain);
        }
        return new Atmosphere(sun, sunColor, sunStrength, ambient, fog, zenith, daylight, night, cloud);
    }

    private static float Smooth(float edge0, float edge1, float x)
    {
        float t = Math.Clamp((x - edge0) / (edge1 - edge0), 0f, 1f);
        return t * t * (3f - 2f * t);
    }

    private static float Lerp(float a, float b, float t) => a + (b - a) * t;
}
