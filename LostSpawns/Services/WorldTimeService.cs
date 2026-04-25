using System.Numerics;

namespace LostSpawns.Services;

/// <summary>
/// Game time-of-day clock. Drives sky / fog / ambient colors for the renderer
/// and the HUD clock. DayFraction cycles 0..1 over DayLengthSeconds and wraps.
///
/// Keyframes define sky/fog/ambient at anchor times through the day; current
/// colors are piecewise-linear interpolated. Keyframes table is readable as
/// intent without a shader: dawn is warm, day is muted gray-blue, dusk is
/// red-orange, night is dark blue-black.
///
/// Tick(dt) is the only write; call from the gameplay branch of the game loop
/// so time stops when paused / in menus.
/// </summary>
public class WorldTimeService
{
    /// <summary>Real-world seconds per in-game day. Test-fast default; tune up for prod.</summary>
    public float DayLengthSeconds { get; set; } = 120f;

    /// <summary>Fraction of day [0,1). 0 = midnight-ish (start of dawn), 0.5 = late afternoon.</summary>
    public float DayFraction { get; private set; } = 0.10f; // start at dawn so first load looks nice

    /// <summary>Day counter - increments each time DayFraction wraps 1 -> 0.</summary>
    public int DayNumber { get; private set; } = 1;

    /// <summary>Seed DayNumber from save. Skips the wrap-count path so loading doesn't desync.</summary>
    public void SetDayNumber(int day) => DayNumber = Math.Max(1, day);

    /// <summary>Advance time by `dt` real-world seconds. Wraps at 1.0 and bumps DayNumber.</summary>
    public void Tick(float dt)
    {
        if (dt <= 0 || DayLengthSeconds <= 0) return;
        float prev = DayFraction;
        DayFraction = (prev + dt / DayLengthSeconds) % 1f;
        if (DayFraction < 0) DayFraction += 1f;
        // Wrap detection: any frame where DayFraction < prev means a day passed.
        if (DayFraction < prev) DayNumber++;
    }

    /// <summary>
    /// Jump the clock to a specific fraction. Used by save/load to restore
    /// time-of-day across sessions. Clamps to [0,1).
    /// </summary>
    public void SetDayFraction(float fraction)
    {
        float f = fraction % 1f;
        if (f < 0) f += 1f;
        DayFraction = f;
    }

    /// <summary>Sky + fog clear color for the current fraction.</summary>
    public Vector3 FogColor => Sample(_keyframes, DayFraction, static k => k.Fog);

    /// <summary>Ambient light for the current fraction.</summary>
    public Vector3 AmbientColor => Sample(_keyframes, DayFraction, static k => k.Ambient);

    /// <summary>Fog density for the current fraction. Thicker at night, thinner at day.</summary>
    public float FogDensity => Sample(_keyframes, DayFraction, static k => k.FogDensity);

    /// <summary>0..1 sun brightness (for future lighting - unused in the solid-color pipeline).</summary>
    public float SunIntensity => Sample(_keyframes, DayFraction, static k => k.SunIntensity);

    /// <summary>Human-readable phase name for HUD display.</summary>
    public string PhaseName
    {
        get
        {
            float t = DayFraction;
            if (t < 0.12f) return "Dawn";
            if (t < 0.48f) return "Day";
            if (t < 0.58f) return "Dusk";
            return "Night";
        }
    }

    /// <summary>True during the dark portion of the cycle. Used by night-only
    /// spawners (e.g. wolves) and any gameplay code that cares about visibility.</summary>
    public bool IsNight => DayFraction >= 0.58f || DayFraction < 0.08f;

    /// <summary>
    /// Target core-temperature comfort [0,1] that the player drifts toward based on
    /// time of day. 0.5 = comfortable; lower = colder ambient; higher = hotter.
    /// Survival code reads this and gently pulls the actual Temperature stat toward
    /// it so nights feel cold and days feel warm.
    /// </summary>
    public float TargetTemperature
    {
        get
        {
            float t = DayFraction;
            if (t < 0.12f) return 0.42f; // Dawn: cool, warming up
            if (t < 0.48f) return 0.52f; // Day: comfortable
            if (t < 0.58f) return 0.40f; // Dusk: cooling
            return 0.22f;                // Night: cold
        }
    }

    /// <summary>
    /// DayFraction mapped to a 24-hour clock string "HH:MM" where 0.00 = 06:00
    /// (start of day) for a more readable display. Purely cosmetic.
    /// </summary>
    public string ClockString
    {
        get
        {
            float hours24 = (DayFraction * 24f + 6f) % 24f;
            int h = (int)hours24;
            int m = (int)((hours24 - h) * 60f);
            return $"{h:D2}:{m:D2}";
        }
    }

    private readonly record struct Keyframe(
        float T,
        Vector3 Fog,
        Vector3 Ambient,
        float FogDensity,
        float SunIntensity);

    // Muted / DayZ-style palette. Indexed by DayFraction; must be sorted ascending.
    // Last entry at T=1.0 mirrors T=0.0 for seamless wraparound.
    private static readonly Keyframe[] _keyframes = new Keyframe[]
    {
        new(0.00f, new(0.18f, 0.15f, 0.22f), new(0.12f, 0.10f, 0.14f), 0.009f, 0.10f), // pre-dawn
        new(0.08f, new(0.80f, 0.60f, 0.50f), new(0.45f, 0.35f, 0.32f), 0.007f, 0.55f), // dawn peak (warm)
        new(0.20f, new(0.55f, 0.65f, 0.75f), new(0.30f, 0.32f, 0.38f), 0.005f, 0.90f), // morning
        new(0.40f, new(0.50f, 0.58f, 0.68f), new(0.26f, 0.28f, 0.32f), 0.005f, 1.00f), // midday
        new(0.52f, new(0.70f, 0.55f, 0.45f), new(0.34f, 0.28f, 0.26f), 0.006f, 0.75f), // late afternoon
        new(0.58f, new(0.85f, 0.40f, 0.30f), new(0.40f, 0.24f, 0.22f), 0.008f, 0.40f), // dusk peak
        new(0.68f, new(0.20f, 0.18f, 0.30f), new(0.14f, 0.13f, 0.18f), 0.010f, 0.15f), // early night
        new(0.85f, new(0.05f, 0.07f, 0.14f), new(0.06f, 0.07f, 0.12f), 0.012f, 0.05f), // deep night
        new(1.00f, new(0.18f, 0.15f, 0.22f), new(0.12f, 0.10f, 0.14f), 0.009f, 0.10f), // wrap to 0.00
    };

    // Single-parameter interpolator - generic accessor to reuse the keyframe search.
    private static T Sample<T>(Keyframe[] keyframes, float t, Func<Keyframe, T> select)
        where T : struct
    {
        // Find segment: keyframes[i].T <= t < keyframes[i+1].T
        for (int i = 0; i < keyframes.Length - 1; i++)
        {
            var a = keyframes[i];
            var b = keyframes[i + 1];
            if (t >= a.T && t <= b.T)
            {
                float span = b.T - a.T;
                float local = span > 0 ? (t - a.T) / span : 0f;
                return Lerp(select(a), select(b), local);
            }
        }
        return select(keyframes[^1]);
    }

    private static T Lerp<T>(T a, T b, float t) where T : struct
    {
        if (a is Vector3 va && b is Vector3 vb) return (T)(object)Vector3.Lerp(va, vb, t);
        if (a is float fa && b is float fb) return (T)(object)(fa + (fb - fa) * t);
        return a; // unsupported type
    }
}
