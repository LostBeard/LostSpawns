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

    /// <summary>Accumulated XP - gained from kills, chops, crafts.</summary>
    public int Experience { get; private set; }

    /// <summary>Lifetime kills tally - incremented once per downed entity.</summary>
    public int Kills { get; private set; }

    /// <summary>Per-kind kill tallies for detailed lifetime stats.</summary>
    public int RabbitKills { get; private set; }
    public int BoarKills { get; private set; }
    public int CrowKills { get; private set; }
    public int WolfKills { get; private set; }

    /// <summary>Record a kill by kind name for per-kind tallies. Kind strings match EntityKind.ToString().</summary>
    public void RecordKindKill(string kind)
    {
        switch (kind)
        {
            case "Rabbit": RabbitKills++; break;
            case "Boar":   BoarKills++;   break;
            case "Crow":   CrowKills++;   break;
            case "Wolf":   WolfKills++;   break;
        }
        OnStatsChanged?.Invoke();
    }

    /// <summary>Seed per-kind kills directly - used by save load.</summary>
    public void SeedKindKillsFromSave(int rabbit, int boar, int crow, int wolf)
    {
        RabbitKills = rabbit;
        BoarKills = boar;
        CrowKills = crow;
        WolfKills = wolf;
        OnStatsChanged?.Invoke();
    }

    /// <summary>Cumulative seconds played across all lives. Tick increments this by dt.</summary>
    public float PlayTimeSeconds { get; private set; }

    /// <summary>Flags for one-shot first-time achievements. Each toggles to true the first time the named event happens and stays true across respawn + save/load.</summary>
    public bool FirstKillAwarded { get; private set; }
    public bool FirstFireAwarded => _firstFire;
    public bool FirstCookAwarded => _firstCook;
    public bool FirstWolfAwarded => _firstWolf;
    public bool FirstSleepAwarded => _firstSleep;

    /// <summary>Fires once per first-time achievement. Arg = display name of the achievement.</summary>
    public event Action<string>? OnAchievement;

    /// <summary>Fires once when the player scores their first kill.</summary>
    public event Action? OnFirstKill;

    /// <summary>
    /// Try to claim a one-shot achievement by name. Sets the matching flag
    /// and fires OnAchievement once. Returns false if already claimed.
    /// </summary>
    public bool TryAwardAchievement(string name)
    {
        bool Fire(ref bool flag)
        {
            if (flag) return false;
            flag = true;
            return true;
        }
        bool fired = name switch
        {
            "First Fire"    => Fire(ref _firstFire),
            "First Cook"    => Fire(ref _firstCook),
            "First Wolf"    => Fire(ref _firstWolf),
            "First Sleep"   => Fire(ref _firstSleep),
            "Veteran"       => Fire(ref _veteran),
            "Centurion"     => Fire(ref _centurion),
            "Survivor"      => Fire(ref _survivor),
            "Bowman"        => Fire(ref _bowman),
            "Hunter"        => Fire(ref _hunter),
            "Completionist" => Fire(ref _completionist),
            _               => false,
        };
        if (!fired) return false;
        OnAchievement?.Invoke(name);
        OnStatsChanged?.Invoke();
        // After any award, check if all the others are now done and fire
        // the meta-achievement once. The recursion guard via the Completionist
        // flag itself stops infinite loops.
        if (!_completionist
            && FirstKillAwarded && _firstFire && _firstCook && _firstWolf
            && _firstSleep && _veteran && _centurion && _survivor && _bowman
            && _hunter)
        {
            TryAwardAchievement("Completionist");
        }
        return true;
    }

    // Backing fields for TryAwardAchievement - public props are read-only
    // snapshots so consumers can't bypass the event path.
    private bool _firstFire;
    private bool _firstCook;
    private bool _firstWolf;
    private bool _firstSleep;
    private bool _veteran;
    private bool _centurion;
    private bool _survivor;
    private bool _bowman;
    private bool _hunter;
    private bool _completionist;

    public bool VeteranAwarded => _veteran;
    public bool CenturionAwarded => _centurion;
    public bool SurvivorAwarded => _survivor;
    public bool BowmanAwarded => _bowman;
    public bool HunterAwarded => _hunter;
    public bool CompletionistAwarded => _completionist;

    /// <summary>Record one entity kill. Survives respawn like XP does.</summary>
    public void RecordKill()
    {
        Kills++;
        OnStatsChanged?.Invoke();
        if (!FirstKillAwarded)
        {
            FirstKillAwarded = true;
            OnFirstKill?.Invoke();
        }
        if (Kills == 100) TryAwardAchievement("Centurion");
    }

    /// <summary>Seed state from a loaded save - bypasses the OnFirstKill event so reloads don't spam the toast.</summary>
    public void SeedKillsFromSave(int kills, bool firstKillAwarded)
    {
        Kills = kills;
        FirstKillAwarded = firstKillAwarded;
        OnStatsChanged?.Invoke();
    }

    /// <summary>Seed the other achievement flags from save. Bypasses OnAchievement so reloads don't spam.</summary>
    public void SeedAchievementsFromSave(bool fire, bool cook, bool wolf, bool sleep, bool veteran = false, bool centurion = false, bool survivor = false, bool bowman = false, bool completionist = false, bool hunter = false)
    {
        _firstFire = fire;
        _firstCook = cook;
        _firstWolf = wolf;
        _firstSleep = sleep;
        _veteran = veteran;
        _centurion = centurion;
        _survivor = survivor;
        _bowman = bowman;
        _completionist = completionist;
        _hunter = hunter;
        OnStatsChanged?.Invoke();
    }

    /// <summary>Seed lifetime play time from save.</summary>
    public void SeedPlayTimeFromSave(float seconds)
    {
        PlayTimeSeconds = seconds;
    }

    /// <summary>
    /// Player level derived from accumulated XP. Formula: floor(sqrt(XP /
    /// 50)) + 1 so level 1 = 0-49 XP, level 2 = 50-199, level 3 = 200-449,
    /// etc. Keeps early progression fast then slows as the player scales
    /// past the survival basics. Future gates (recipe unlocks, stat caps)
    /// read this property.
    /// </summary>
    public int Level => (int)MathF.Floor(MathF.Sqrt(Experience / 50f)) + 1;

    /// <summary>Grant XP and fire OnStatsChanged + OnLevelUp if a threshold is crossed.</summary>
    public void AwardXp(int amount)
    {
        if (amount <= 0) return;
        int before = Level;
        Experience += amount;
        int after = Level;
        OnStatsChanged?.Invoke();
        if (after > before) OnLevelUp?.Invoke(after);
        if (after >= 5 && before < 5) TryAwardAchievement("Veteran");
    }

    /// <summary>Fires once per level-up. Arg = the new level reached.</summary>
    public event Action<int>? OnLevelUp;

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
    /// When true, stamina regen is skipped this tick. Set by Game.razor when a
    /// hostile entity is in combat proximity so the player can't just stand
    /// still catching their breath with a wolf charging them.
    /// </summary>
    public bool StaminaRegenBlocked { get; set; }

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
        PlayTimeSeconds += dt;
        Hunger = _hunger - dt * HungerDecayRate;
        Thirst = _thirst - dt * ThirstDecayRate;
        // Sprint drains stamina; otherwise it regens. Mutually exclusive so the
        // player can't cheese a fractional-frame micro-sprint to keep regen going.
        if (sprinting)
        {
            Stamina = _stamina - dt * SprintDrainRate;
            // Extra thirst drain during sprint: running hot sweats water out
            // faster than walking. Small per-tick but adds up over a chase.
            Thirst = _thirst - dt * ThirstDecayRate * 1.5f;
        }
        else if (!StaminaRegenBlocked)
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
        // XP is deliberately preserved across respawn - it represents lifetime
        // progress, not current-life vitality. A future "tier" system will
        // unlock new recipes based on total XP earned.
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
