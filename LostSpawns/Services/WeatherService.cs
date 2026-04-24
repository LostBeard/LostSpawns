namespace LostSpawns.Services;

/// <summary>
/// World weather clock. Alternates between clear and raining on a repeating
/// timer with smooth intensity ramp in/out so weather fades rather than pops.
/// Tick(dt) is the only write; call from the gameplay branch of the game
/// loop so weather stops advancing when the player is paused / in menus.
///
/// The actual rain visual is drawn by HudService (2D screen-space particles
/// on the UI renderer). The gameplay hook is that RainIntensity shifts the
/// day-night temperature target colder while raining - damp nights bite.
/// </summary>
public class WeatherService
{
    /// <summary>Target intensity the ramp interpolates toward. 0 = clear, 1 = heavy rain.</summary>
    public float TargetIntensity { get; private set; }

    /// <summary>Current rain intensity [0,1]. Smoothly tracks TargetIntensity.</summary>
    public float RainIntensity { get; private set; }

    /// <summary>True while rain is actively falling (intensity above a small threshold).</summary>
    public bool IsRaining => RainIntensity > 0.05f;

    /// <summary>Seconds per weather phase (base). Randomized by +/- 30% each switch.</summary>
    public float PhaseSeconds { get; set; } = 40f;

    /// <summary>How fast intensity ramps toward the target (per second).</summary>
    public float RampRate { get; set; } = 0.25f;

    private readonly Random _rng;
    private float _phaseTimeLeft;
    private bool _rainPhase;
    private float _lightningCountdown;

    /// <summary>Fired when a lightning strike should flash the screen (heavy rain only).</summary>
    public event Action? OnLightningStrike;

    /// <summary>Rain intensity above which lightning can trigger.</summary>
    public float LightningThreshold { get; set; } = 0.55f;

    public WeatherService()
    {
        // Seed from system clock so sessions vary, but a consumer could pass
        // an explicit seed through a ctor overload for replay / testing.
        _rng = new Random();
        ScheduleNextPhase(clearStart: true);
    }

    /// <summary>Advance weather by dt seconds. Switches phases when the timer expires.</summary>
    public void Tick(float dt)
    {
        if (dt <= 0) return;

        // Ramp current intensity toward target.
        float delta = TargetIntensity - RainIntensity;
        float step = MathF.Sign(delta) * MathF.Min(MathF.Abs(delta), dt * RampRate);
        RainIntensity = Math.Clamp(RainIntensity + step, 0f, 1f);

        // Count down phase. When it expires, flip.
        _phaseTimeLeft -= dt;
        if (_phaseTimeLeft <= 0)
            ScheduleNextPhase(clearStart: false);

        // Lightning: while intensity is above threshold, tick a per-strike countdown.
        // When it hits zero, fire a strike and reseed. Timer resets whenever rain
        // falls below threshold so the first strike of a storm has a random delay.
        if (RainIntensity < LightningThreshold)
        {
            _lightningCountdown = NextLightningDelay();
        }
        else
        {
            _lightningCountdown -= dt;
            if (_lightningCountdown <= 0)
            {
                OnLightningStrike?.Invoke();
                _lightningCountdown = NextLightningDelay();
            }
        }
    }

    private float NextLightningDelay() => 12f + (float)_rng.NextDouble() * 16f;

    private void ScheduleNextPhase(bool clearStart)
    {
        if (clearStart)
        {
            _rainPhase = false;
        }
        else
        {
            _rainPhase = !_rainPhase;
        }

        // Randomize target intensity across rain phases so not every storm is
        // identical - a light drizzle one round, a downpour the next.
        TargetIntensity = _rainPhase ? (0.4f + (float)_rng.NextDouble() * 0.6f) : 0f;

        float jitter = 0.7f + (float)_rng.NextDouble() * 0.6f; // 0.7 - 1.3x
        _phaseTimeLeft = PhaseSeconds * jitter;
    }
}
