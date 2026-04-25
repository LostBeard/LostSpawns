namespace LostSpawns.Services;

/// <summary>
/// What killed the player on the most recent damage tick. Frozen onto
/// LastCauseOfDeath the first frame Health hits 0 so the death screen
/// can show "Killed by Wolf" / "Fell" / "Starved" instead of generic
/// "wasteland claimed you" filler.
/// </summary>
public enum DamageCause
{
    Unknown,
    Wolf,
    Bear,
    Boar,
    Fall,
    Starvation,
    Thirst,
    Hypothermia,
    Heatstroke,
    Bleed,
}

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
    public int DeerKills { get; private set; }
    public int BearKills { get; private set; }

    /// <summary>Record a kill by kind name for per-kind tallies. Kind strings match EntityKind.ToString().</summary>
    public void RecordKindKill(string kind)
    {
        switch (kind)
        {
            case "Rabbit": RabbitKills++; break;
            case "Boar":   BoarKills++;   break;
            case "Crow":   CrowKills++;   break;
            case "Wolf":   WolfKills++;   break;
            case "Deer":   DeerKills++;   break;
            case "Bear":   BearKills++;   break;
        }
        OnStatsChanged?.Invoke();
    }

    /// <summary>Seed per-kind kills directly - used by save load.</summary>
    public void SeedKindKillsFromSave(int rabbit, int boar, int crow, int wolf, int deer = 0, int bear = 0)
    {
        RabbitKills = rabbit;
        BoarKills = boar;
        CrowKills = crow;
        WolfKills = wolf;
        DeerKills = deer;
        BearKills = bear;
        OnStatsChanged?.Invoke();
    }

    /// <summary>Cumulative seconds played across all lives. Tick increments this by dt.</summary>
    public float PlayTimeSeconds { get; private set; }

    /// <summary>Cumulative blocks-of-distance walked across all lives. Game.razor adds horizontal frame deltas.</summary>
    public float DistanceTraveled { get; private set; }

    /// <summary>Seconds survived in the current life - reset on respawn, ticked by Tick.</summary>
    public float CurrentLifeSeconds { get; private set; }

    /// <summary>Kills made on the current life. Reset on respawn.</summary>
    public int CurrentLifeKills { get; private set; }

    /// <summary>Bump current-life kills. Called from RecordKindKill caller path.</summary>
    public void RecordCurrentLifeKill()
    {
        CurrentLifeKills++;
        OnStatsChanged?.Invoke();
    }

    /// <summary>Longest single-life duration ever, in seconds. Persists across deaths + saves.</summary>
    public float LongestLifeSeconds { get; private set; }

    /// <summary>Seed longest life from save load.</summary>
    public void SeedLongestLifeFromSave(float seconds) => LongestLifeSeconds = seconds;

    /// <summary>Add a horizontal-distance increment to the lifetime traveled total.</summary>
    public void AddDistance(float blocks)
    {
        if (blocks <= 0) return;
        DistanceTraveled += blocks;
        OnStatsChanged?.Invoke();
        // Marathon at 10km lifetime. Idempotent guard inside TryAwardAchievement.
        if (DistanceTraveled >= 10000f) TryAwardAchievement("Marathon");
    }

    /// <summary>Seed lifetime distance from save load.</summary>
    public void SeedDistanceFromSave(float blocks)
    {
        DistanceTraveled = blocks;
        OnStatsChanged?.Invoke();
    }

    /// <summary>Total successful raw-to-cooked conversions across all lives.</summary>
    public int CookCount { get; private set; }

    /// <summary>Total deaths across all lives. Bumped from HudService on OnDied.</summary>
    public int Deaths { get; private set; }

    /// <summary>Highest kill-streak combo ever reached. Persists across deaths + loads.</summary>
    public int BestCombo { get; private set; }

    /// <summary>Update BestCombo if the current streak exceeds it. Idempotent.</summary>
    public void RecordCombo(int streak)
    {
        if (streak > BestCombo)
        {
            BestCombo = streak;
            OnStatsChanged?.Invoke();
        }
    }

    /// <summary>Seed BestCombo from save.</summary>
    public void SeedBestComboFromSave(int best) => BestCombo = best;

    /// <summary>Increment death count + check for the Resilient achievement at 3.</summary>
    public void RecordDeath()
    {
        Deaths++;
        // Longest-life snapshot taken before reset so a long final run gets
        // its bragging rights even if the player dies just after a hard win.
        if (CurrentLifeSeconds > LongestLifeSeconds)
            LongestLifeSeconds = CurrentLifeSeconds;
        CurrentLifeSeconds = 0;
        CurrentLifeKills = 0;
        OnStatsChanged?.Invoke();
        if (Deaths == 3) TryAwardAchievement("Resilient");
    }

    public void SeedDeathsFromSave(int count)
    {
        Deaths = count;
        OnStatsChanged?.Invoke();
    }

    /// <summary>Increment cook count + check for the Gourmet achievement at 10.</summary>
    public void RecordCook()
    {
        CookCount++;
        OnStatsChanged?.Invoke();
        if (CookCount == 10) TryAwardAchievement("Gourmet");
    }

    /// <summary>Seed cook count from save load.</summary>
    public void SeedCookCountFromSave(int count)
    {
        CookCount = count;
        OnStatsChanged?.Invoke();
    }

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
            "Gourmet"       => Fire(ref _gourmet),
            "Resilient"     => Fire(ref _resilient),
            "First Aid"     => Fire(ref _firstAid),
            "Pack Hunter"   => Fire(ref _packHunter),
            "Bear Slayer"   => Fire(ref _bearSlayer),
            "Marathon"      => Fire(ref _marathon),
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
            && _hunter && _gourmet && _resilient && _firstAid && _packHunter
            && _bearSlayer && _marathon)
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
    private bool _gourmet;
    private bool _resilient;
    private bool _firstAid;
    private bool _packHunter;
    private bool _bearSlayer;
    private bool _marathon;
    private bool _completionist;

    public bool VeteranAwarded => _veteran;
    public bool CenturionAwarded => _centurion;
    public bool SurvivorAwarded => _survivor;
    public bool BowmanAwarded => _bowman;
    public bool HunterAwarded => _hunter;
    public bool GourmetAwarded => _gourmet;
    public bool ResilientAwarded => _resilient;
    public bool FirstAidAwarded => _firstAid;
    public bool PackHunterAwarded => _packHunter;
    public bool BearSlayerAwarded => _bearSlayer;
    public bool MarathonAwarded => _marathon;
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
    public void SeedAchievementsFromSave(bool fire, bool cook, bool wolf, bool sleep, bool veteran = false, bool centurion = false, bool survivor = false, bool bowman = false, bool completionist = false, bool hunter = false, bool gourmet = false, bool resilient = false, bool firstAid = false, bool packHunter = false, bool bearSlayer = false, bool marathon = false)
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
        _gourmet = gourmet;
        _resilient = resilient;
        _firstAid = firstAid;
        _packHunter = packHunter;
        _bearSlayer = bearSlayer;
        _marathon = marathon;
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

    /// <summary>Most recent cause passed to TakeDamage. Reset on respawn.</summary>
    public DamageCause LastDamageCause { get; private set; } = DamageCause.Unknown;

    /// <summary>The cause of death frozen on the frame Health first hit 0. Read by the death screen.</summary>
    public DamageCause LastCauseOfDeath { get; private set; } = DamageCause.Unknown;

    /// <summary>
    /// Apply damage, clamped at zero. Fires OnStatsChanged if Health actually dropped,
    /// then fires OnDamageTaken with the amount actually applied (0 if already dead).
    /// Fires OnDied once on the frame Health first hits 0. The cause is recorded
    /// in LastDamageCause on every hit and frozen onto LastCauseOfDeath on death.
    /// </summary>
    public void TakeDamage(float amount, DamageCause cause = DamageCause.Unknown)
    {
        if (amount <= 0) return;
        // Always remember the most recent attribution - the death screen reads
        // whichever cause was active at the moment HP crossed zero.
        LastDamageCause = cause;
        float before = _health;
        // Last Stand: above 10% HP this hit can drop you to 0; below 10%
        // single-hit damage caps at leaving you with 0.01 (1%) HP. Forces
        // a second hit to actually kill you when you're clinging to life.
        // Skipped for environmental DoT (bleed, starve, etc.) so those can
        // still finish a downed player; only carnivore + fall hits clamp.
        bool clamp = before <= 0.10f
            && (cause == DamageCause.Wolf || cause == DamageCause.Bear
                || cause == DamageCause.Boar || cause == DamageCause.Fall);
        if (clamp && amount >= before)
        {
            Health = 0.01f;
        }
        else
        {
            Health = MathF.Max(0f, _health - amount);
        }
        float applied = before - _health;
        if (applied > 0) OnDamageTaken?.Invoke(applied);
        if (_health <= 0f && !_deathFired)
        {
            _deathFired = true;
            LastCauseOfDeath = cause;
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
    /// Seconds of bleed remaining. While &gt; 0 the player loses HP at
    /// BleedDamageRate per second AND HP regen is suppressed. Cleared by
    /// applying a bandage. Wolves apply ~6s on contact bite.
    /// </summary>
    public float BleedSecondsRemaining { get; private set; }

    /// <summary>HP drained per second of bleed. Slow enough that bandages aren't mandatory but a clean fight matters.</summary>
    public float BleedDamageRate { get; set; } = 0.02f;

    /// <summary>Apply (or extend) a bleed for the given duration. Takes the max of current vs requested so a fresh wolf bite never shortens an active bleed.</summary>
    public void ApplyBleed(float seconds)
    {
        if (seconds <= 0) return;
        if (BleedSecondsRemaining < seconds) BleedSecondsRemaining = seconds;
    }

    /// <summary>Clear any active bleed (bandage / painkiller use).</summary>
    public void ClearBleed() => BleedSecondsRemaining = 0;

    /// <summary>Reduce active bleed by `seconds` (clamped at 0). Used by
    /// hot food / minor remedies that help but don't fully cure.</summary>
    public void ReduceBleed(float seconds)
    {
        if (seconds <= 0) return;
        BleedSecondsRemaining = MathF.Max(0, BleedSecondsRemaining - seconds);
    }

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
        // Current-life clock only ticks while alive. Death resets it; the
        // Longest snapshot is taken at RecordDeath time.
        if (!IsDead) CurrentLifeSeconds += dt;
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
        {
            // Hunger and thirst gate stamina recovery - a starving body
            // can't replenish energy. Multiplier scales linearly: full
            // food/water = 1.0x regen, 0% = 0.3x. Doesn't fully zero so
            // the player can always crawl out of the hole, just slowly.
            float hungerMul = 0.3f + 0.7f * MathF.Min(_hunger, _thirst);
            Stamina = _stamina + dt * StaminaRegenRate * hungerMul;
        }

        // Temperature drifts toward the ambient target. Step size is clamped so
        // we never overshoot within a single tick.
        float tempDelta = ambientTarget - _temperature;
        float tempStep = MathF.Sign(tempDelta) * MathF.Min(MathF.Abs(tempDelta), dt * TemperatureAdaptRate);
        Temperature = _temperature + tempStep;

        // Starvation / dehydration / hypothermia / heatstroke HP drain. Multiple
        // conditions stack multiplicatively - if you're starving AND freezing, you
        // die roughly twice as fast.
        if (_hunger <= 0f) TakeDamage(dt * StarvationDamageRate, DamageCause.Starvation);
        if (_thirst <= 0f) TakeDamage(dt * StarvationDamageRate, DamageCause.Thirst);
        if (_temperature < 0.15f) TakeDamage(dt * HypothermiaDamageRate, DamageCause.Hypothermia);
        if (_temperature > 0.85f) TakeDamage(dt * HeatstrokeDamageRate, DamageCause.Heatstroke);

        // Bleed damage-over-time. Active while the timer is positive; bandages
        // clear it. Suppresses passive regen below so a player can't tank a
        // fresh wolf bite by sitting on full hunger/thirst.
        bool bleeding = BleedSecondsRemaining > 0;
        if (bleeding)
        {
            BleedSecondsRemaining = MathF.Max(0, BleedSecondsRemaining - dt);
            TakeDamage(dt * BleedDamageRate, DamageCause.Bleed);
        }

        // Passive HP regen when every survival stat is in its healthy band.
        // Gives the player a reason to maintain hunger/thirst/warmth beyond
        // avoiding immediate death - recovery from damage flows through food
        // + water + shelter instead of requiring scarce bandages.
        if (!bleeding && _health < 1f &&
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
        BleedSecondsRemaining = 0;
        // Reset cause attribution so the next death screen doesn't show a
        // stale label if the player bandages off a wolf bite right before
        // dying to fall damage.
        LastDamageCause = DamageCause.Unknown;
        LastCauseOfDeath = DamageCause.Unknown;
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
