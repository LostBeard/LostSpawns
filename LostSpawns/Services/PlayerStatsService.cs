namespace LostSpawns.Services;

/// <summary>
/// Runtime player stats (survival + vitals). Gameplay systems write values here,
/// HudService reads them to drive the on-screen bars. All values are normalized [0,1].
///
/// Default values intentionally match the hardcoded placeholders HudService used
/// before this service existed, so adding PlayerStatsService is visually a no-op
/// until gameplay systems start writing real values.
///
/// Fire OnStatsChanged after any set to give subscribers (HUD, save system, etc.)
/// a single notification point. Setters only fire the event when the value actually
/// changes, so spam-writing the same value does not wake up listeners.
/// </summary>
public class PlayerStatsService
{
    private float _health = 0.85f;
    private float _stamina = 0.6f;
    private float _hunger = 0.45f;
    private float _thirst = 0.7f;
    private float _temperature = 0.5f;

    /// <summary>Fired whenever any stat changes to a new value.</summary>
    public event Action? OnStatsChanged;

    /// <summary>Fired when TakeDamage runs with a positive amount. Arg = damage actually applied [0,1].</summary>
    public event Action<float>? OnDamageTaken;

    /// <summary>Fired when Heal runs with a positive amount. Arg = health actually restored [0,1].</summary>
    public event Action<float>? OnHealed;

    /// <summary>Fired on the frame Health first crosses to 0. Not fired again until the player respawns above 0.</summary>
    public event Action? OnDied;

    /// <summary>True while Health is 0. Consumers (Game.razor) use this to freeze gameplay.</summary>
    public bool IsDead => _health <= 0f;

    private bool _deathFired;

    /// <summary>Current health [0,1]. 0 = dead, 1 = pristine.</summary>
    public float Health
    {
        get => _health;
        set { if (Set(ref _health, value)) OnStatsChanged?.Invoke(); }
    }

    /// <summary>Stamina [0,1]. Depletes from sprint, jumping, heavy carry; regens at rest.</summary>
    public float Stamina
    {
        get => _stamina;
        set { if (Set(ref _stamina, value)) OnStatsChanged?.Invoke(); }
    }

    /// <summary>Hunger [0,1]. 1 = full, 0 = starving.</summary>
    public float Hunger
    {
        get => _hunger;
        set { if (Set(ref _hunger, value)) OnStatsChanged?.Invoke(); }
    }

    /// <summary>Thirst [0,1]. 1 = hydrated, 0 = parched.</summary>
    public float Thirst
    {
        get => _thirst;
        set { if (Set(ref _thirst, value)) OnStatsChanged?.Invoke(); }
    }

    /// <summary>Core temperature comfort [0,1]. 0 = hypothermic, 0.5 = comfortable, 1 = heat-stroke.</summary>
    public float Temperature
    {
        get => _temperature;
        set { if (Set(ref _temperature, value)) OnStatsChanged?.Invoke(); }
    }

    /// <summary>
    /// Apply damage, clamped at zero. Fires OnStatsChanged if Health actually dropped,
    /// then fires OnDamageTaken with the amount actually applied (0 if already dead).
    /// Fires OnDied once on the frame Health first hits 0.
    /// </summary>
    public void TakeDamage(float amount)
    {
        if (amount <= 0) return;
        float before = _health;
        Health = MathF.Max(0f, _health - amount);
        float applied = before - _health;
        if (applied > 0) OnDamageTaken?.Invoke(applied);
        if (_health <= 0f && !_deathFired)
        {
            _deathFired = true;
            OnDied?.Invoke();
        }
    }

    /// <summary>
    /// Restore health (bandage, food-over-time, rest), clamped at 1. Fires OnHealed with
    /// the amount actually restored (0 if already at full health).
    /// </summary>
    public void Heal(float amount)
    {
        if (amount <= 0) return;
        float before = _health;
        Health = MathF.Min(1f, _health + amount);
        float applied = _health - before;
        if (applied > 0) OnHealed?.Invoke(applied);
    }

    /// <summary>Hunger drain per real-world second while Tick is called.</summary>
    public float HungerDecayRate { get; set; } = 0.003f;

    /// <summary>Thirst drain per real-world second. Slightly faster than hunger per PLAN-Survival-Needs.</summary>
    public float ThirstDecayRate { get; set; } = 0.004f;

    /// <summary>Stamina regen per real-world second while not sprinting (clamped to 1.0).</summary>
    public float StaminaRegenRate { get; set; } = 0.10f;

    /// <summary>Stamina drain per real-world second while sprinting. Must exceed regen to actually deplete.</summary>
    public float SprintDrainRate { get; set; } = 0.20f;

    /// <summary>HP drain per real-world second while hunger or thirst is at 0.</summary>
    public float StarvationDamageRate { get; set; } = 0.005f;

    /// <summary>HP regen per second while all survival stats are healthy (see Tick).</summary>
    public float HealthRegenRate { get; set; } = 0.008f;

    /// <summary>
    /// Multiplier applied to HP regen this tick. Game.razor sets this to 2.5
    /// when the player is inside a campfire's warmth aura so camping speeds
    /// up recovery. Reset by the caller each frame (no decay in-service).
    /// </summary>
    public float HealthRegenMultiplier { get; set; } = 1f;

    /// <summary>
    /// How fast body temperature drifts toward its ambient target. Lower = slower
    /// response; warm clothing / gear effectively reduces this on the consuming side.
    /// </summary>
    public float TemperatureAdaptRate { get; set; } = 0.015f;

    /// <summary>Hypothermia HP drain per second when Temperature &lt; 0.15.</summary>
    public float HypothermiaDamageRate { get; set; } = 0.004f;

    /// <summary>Heatstroke HP drain per second when Temperature &gt; 0.85.</summary>
    public float HeatstrokeDamageRate { get; set; } = 0.004f;

    /// <summary>
    /// Advance the survival simulation by `dt` seconds. Drains hunger + thirst,
    /// regens stamina (or drains while sprinting), damages HP when starving /
    /// dehydrated / freezing / overheating, drifts Temperature toward the given
    /// ambient target (use WorldTimeService's TargetTemperature for the default
    /// day-night coupling). Call from the game loop only while unpaused.
    /// </summary>
    public void Tick(float dt, float ambientTarget = 0.5f, bool sprinting = false)
    {
        if (dt <= 0) return;
        Hunger = _hunger - dt * HungerDecayRate;
        Thirst = _thirst - dt * ThirstDecayRate;
        // Sprint drains stamina; otherwise it regens. Mutually exclusive so the
        // player can't cheese a fractional-frame micro-sprint to keep regen going.
        if (sprinting)
            Stamina = _stamina - dt * SprintDrainRate;
        else
            Stamina = _stamina + dt * StaminaRegenRate;

        // Temperature drifts toward the ambient target. Step size is clamped so
        // we never overshoot within a single tick.
        float tempDelta = ambientTarget - _temperature;
        float tempStep = MathF.Sign(tempDelta) * MathF.Min(MathF.Abs(tempDelta), dt * TemperatureAdaptRate);
        Temperature = _temperature + tempStep;

        // Starvation / dehydration / hypothermia / heatstroke HP drain. Multiple
        // conditions stack multiplicatively - if you're starving AND freezing, you
        // die roughly twice as fast.
        if (_hunger <= 0f) TakeDamage(dt * StarvationDamageRate);
        if (_thirst <= 0f) TakeDamage(dt * StarvationDamageRate);
        if (_temperature < 0.15f) TakeDamage(dt * HypothermiaDamageRate);
        if (_temperature > 0.85f) TakeDamage(dt * HeatstrokeDamageRate);

        // Passive HP regen when every survival stat is in its healthy band.
        // Gives the player a reason to maintain hunger/thirst/warmth beyond
        // avoiding immediate death - recovery from damage flows through food
        // + water + shelter instead of requiring scarce bandages.
        if (_health < 1f &&
            _hunger > 0.5f && _thirst > 0.5f && _stamina > 0.5f &&
            _temperature > 0.30f && _temperature < 0.75f)
        {
            Heal(dt * HealthRegenRate * HealthRegenMultiplier);
        }
    }

    /// <summary>Reset all stats to full (for respawn, new character, test harness).</summary>
    public void ResetToDefaults()
    {
        _health = 1f;
        _stamina = 1f;
        _hunger = 1f;
        _thirst = 1f;
        _temperature = 0.5f;
        _deathFired = false;
        OnStatsChanged?.Invoke();
    }

    private static bool Set(ref float field, float value)
    {
        float clamped = Math.Clamp(value, 0f, 1f);
        if (clamped == field) return false;
        field = clamped;
        return true;
    }
}
