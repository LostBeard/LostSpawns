using System.Numerics;

namespace LostSpawns.Services;

/// <summary>
/// Kinds of wandering creatures. Drives minimap marker color + future loot
/// tables + future AI differentiation (rabbits flee, boars charge, etc).
/// </summary>
public enum EntityKind
{
    Rabbit,
    Boar,
    Crow,
    Wolf,
}

/// <summary>
/// How an entity is currently behaving. Idle means normal wander; Flee
/// pushes it directly away from LastAlertSource; Charge drives it directly
/// toward it. Both override the random wander vector until AlertTimer runs
/// out and the entity returns to idle wandering.
/// </summary>
public enum AlertMode
{
    Idle,
    Flee,
    Charge,
}

/// <summary>
/// Single mutable entity instance. Mutable for cheap tick updates - we're
/// iterating a small list every frame and creating new records would produce
/// garbage pressure. Position is world-space voxel coordinates.
/// </summary>
public sealed class WanderingEntity
{
    public int Id { get; init; }
    public EntityKind Kind { get; init; }
    public Vector3 Position;
    public Vector3 Velocity;
    public float Health = 1f;
    public float MaxHealth = 1f;
    public float WanderRetargetIn; // seconds until next random-direction pick
    public AlertMode Alert;
    public float AlertTimer;       // seconds remaining in current alert state
    public Vector3 LastAlertSource; // player pos at time of last hit
    public float HitFlashTimer;    // seconds remaining on the "just got hit" white flash
}

/// <summary>
/// Holds the active entity list + a simple random-walk tick. Game.razor
/// calls Tick(dt) in the active-play branch so wandering stops during pause
/// / menus / death. HudService reads the list each frame to upsert minimap
/// markers.
///
/// MVP scope: horizontal wander only, no chase / flee, no combat hooks.
/// 3D rendering deferred to a follow-up commit - for now entities exist as
/// positions on the minimap. Still gives the player "something else is
/// out there" feedback in the otherwise-empty world.
/// </summary>
public class EntityService
{
    private readonly Random _rng = new();
    private int _nextId = 1;

    public List<WanderingEntity> Entities { get; } = new();

    /// <summary>Speed in blocks/sec during an active wander step.</summary>
    public float WanderSpeed { get; set; } = 1.4f;

    /// <summary>Seconds an entity walks in one direction before picking a new one (jittered).</summary>
    public float WanderRetargetSeconds { get; set; } = 3f;

    /// <summary>Max active wandering entities - respawn tick stops once this is reached.</summary>
    public int MaxEntities { get; set; } = 5;

    /// <summary>Seconds between respawn attempts once the population is below cap.</summary>
    public float RespawnIntervalSeconds { get; set; } = 45f;

    /// <summary>How far around the player a respawned entity appears (min/max blocks).</summary>
    public (float Min, float Max) RespawnRange { get; set; } = (12f, 22f);

    private float _respawnTimer;

    /// <summary>
    /// Populate the list with the starter set of 5 entities near the given
    /// spawn point. Heights are samples of the heightmap; each entity gets a
    /// random initial wander direction. Called once from Game.razor after
    /// the world finishes its initial chunk generation.
    /// </summary>
    public void SpawnStarter(Vector3 near, WorldService world)
    {
        if (Entities.Count > 0) return;

        // Mix of kinds so the minimap reads as "wildlife" rather than clones.
        Spawn(EntityKind.Rabbit, near + new Vector3(4, 0, 4), world);
        Spawn(EntityKind.Rabbit, near + new Vector3(-6, 0, 3), world);
        Spawn(EntityKind.Boar,   near + new Vector3(8, 0, -5), world);
        Spawn(EntityKind.Boar,   near + new Vector3(-9, 0, -7), world);
        Spawn(EntityKind.Crow,   near + new Vector3(2, 0, 11), world);
    }

    public WanderingEntity Spawn(EntityKind kind, Vector3 pos, WorldService world)
    {
        int groundY = world.GetHeightAt(pos.X, pos.Z) + 1;
        var e = new WanderingEntity
        {
            Id = _nextId++,
            Kind = kind,
            Position = new Vector3(pos.X, groundY, pos.Z),
            WanderRetargetIn = (float)_rng.NextDouble() * WanderRetargetSeconds,
            Health = MaxHealthForKind(kind),
            MaxHealth = MaxHealthForKind(kind),
        };
        PickRandomDirection(e);
        Entities.Add(e);
        return e;
    }

    /// <summary>
    /// Starting health for each kind. HP stays normalized [0, 1] for the UI
    /// (bar fills from Health / MaxHealthForKind on render) but that level's
    /// not needed by the game loop yet - combat simply subtracts absolute
    /// damage. Values chosen so Axe (0.55 dmg) drops a rabbit in 2 swings
    /// and a wolf in 4.
    /// </summary>
    private static float MaxHealthForKind(EntityKind k) => k switch
    {
        EntityKind.Rabbit => 1.0f,
        EntityKind.Crow   => 1.0f,
        EntityKind.Boar   => 1.5f,
        EntityKind.Wolf   => 1.8f,
        _                 => 1.0f,
    };

    /// <summary>
    /// Fires when a charging entity has closed to contact range with the
    /// player. Float payload is the damage amount. Game.razor subscribes to
    /// route this to PlayerStatsService.TakeDamage + a HUD shake / toast.
    /// </summary>
    public event Action<WanderingEntity, float>? OnContactAttack;

    /// <summary>How close a wolf has to be to the player before it auto-aggros.</summary>
    public float WolfAggroRange { get; set; } = 10f;

    /// <summary>Advance every entity. Call from the active-play branch only.</summary>
    public void Tick(float dt, WorldService world, Vector3 playerPos, bool isNight = false)
    {
        foreach (var e in Entities)
        {
            if (e.HitFlashTimer > 0) e.HitFlashTimer = MathF.Max(0, e.HitFlashTimer - dt);

            // Wolves auto-aggro when the player enters their sensing range,
            // even without being hit first. Once charging they follow the
            // normal charge logic below - chase the live player position,
            // drop back to Idle after a contact hit.
            if (e.Kind == EntityKind.Wolf && e.Alert == AlertMode.Idle)
            {
                float dx = playerPos.X - e.Position.X;
                float dz = playerPos.Z - e.Position.Z;
                if (dx * dx + dz * dz <= WolfAggroRange * WolfAggroRange)
                {
                    e.Alert = AlertMode.Charge;
                    e.AlertTimer = 6f; // long enough to actually reach the player
                    e.LastAlertSource = playerPos;
                }
            }

            if (e.Alert != AlertMode.Idle)
            {
                // Alert states override the wander vector until their timer
                // expires. Flee = away from attacker, Charge = toward attacker.
                e.AlertTimer -= dt;
                if (e.AlertTimer <= 0)
                {
                    e.Alert = AlertMode.Idle;
                    PickRandomDirection(e);
                    e.WanderRetargetIn = WanderRetargetSeconds;
                }
                else
                {
                    // A charging entity chases the player's current position,
                    // not where the player was when it got hit - so it doesn't
                    // blindly run past if the player has moved.
                    var chaseTarget = e.Alert == AlertMode.Charge ? playerPos : e.LastAlertSource;
                    var toward = new Vector3(
                        chaseTarget.X - e.Position.X,
                        0,
                        chaseTarget.Z - e.Position.Z);
                    float len = toward.Length();
                    if (len > 1e-3f)
                    {
                        var dir = toward / len;
                        float speed = AlertSpeedForKind(e.Kind);
                        // Flee flips the sign so the entity runs the other way.
                        if (e.Alert == AlertMode.Flee) dir = -dir;
                        e.Velocity = dir * speed;
                    }

                    // Contact-damage window for chargers. 1.5 blocks matches
                    // the player collision radius plus a small forgiveness.
                    // After a hit the entity drops back to Idle so we don't
                    // loop-damage every frame - a second strike requires
                    // player or AI re-engagement.
                    if (e.Alert == AlertMode.Charge && len < 1.5f)
                    {
                        OnContactAttack?.Invoke(e, ContactDamageForKind(e.Kind));
                        e.Alert = AlertMode.Idle;
                        e.AlertTimer = 0;
                        PickRandomDirection(e);
                        e.WanderRetargetIn = WanderRetargetSeconds;
                    }
                }
            }
            else
            {
                e.WanderRetargetIn -= dt;
                if (e.WanderRetargetIn <= 0)
                {
                    PickRandomDirection(e);
                    e.WanderRetargetIn = WanderRetargetSeconds * (0.6f + (float)_rng.NextDouble() * 0.8f);
                }
            }

            // Advance position along the (flat) wander vector.
            e.Position += e.Velocity * dt;

            // Snap Y to the heightmap so entities visually walk on terrain
            // rather than clipping through / floating above. Cheap because
            // GetHeightAt is direct heightmap lookup.
            int groundY = world.GetHeightAt(e.Position.X, e.Position.Z) + 1;
            e.Position = new Vector3(e.Position.X, groundY, e.Position.Z);
        }

        // Respawn tick - only advances while population is below cap so a
        // fully-stocked world doesn't accumulate progress and dump a wave
        // of new entities the instant the player kills one.
        if (Entities.Count < MaxEntities)
        {
            _respawnTimer += dt;
            if (_respawnTimer >= RespawnIntervalSeconds)
            {
                _respawnTimer = 0;
                RespawnOneNear(playerPos, world, isNight);
            }
        }
        else
        {
            _respawnTimer = 0;
        }
    }

    /// <summary>
    /// Pick a random EntityKind and a random position in a ring around the
    /// player (so new critters don't materialize on top of the camera) and
    /// spawn one. Kind distribution favors rabbits, which is also how the
    /// starter set skews. Position uses polar sampling + uniform angle so
    /// spawn direction is evenly spread rather than biased to an axis.
    /// </summary>
    private void RespawnOneNear(Vector3 playerPos, WorldService world, bool isNight)
    {
        // Day: 40% rabbit, 30% boar, 30% crow (no wolves - they're shade-of-night threats).
        // Night: 20% rabbit, 15% boar, 15% crow, 50% wolf (wolves dominate night spawns).
        double roll = _rng.NextDouble();
        EntityKind kind;
        if (isNight)
        {
            kind = roll < 0.20 ? EntityKind.Rabbit
                 : roll < 0.35 ? EntityKind.Boar
                 : roll < 0.50 ? EntityKind.Crow
                               : EntityKind.Wolf;
        }
        else
        {
            kind = roll < 0.40 ? EntityKind.Rabbit
                 : roll < 0.70 ? EntityKind.Boar
                               : EntityKind.Crow;
        }

        double angle = _rng.NextDouble() * Math.PI * 2;
        float dist = RespawnRange.Min
                   + (float)_rng.NextDouble() * (RespawnRange.Max - RespawnRange.Min);
        float dx = (float)Math.Cos(angle) * dist;
        float dz = (float)Math.Sin(angle) * dist;
        var pos = new Vector3(playerPos.X + dx, 0, playerPos.Z + dz);
        Spawn(kind, pos, world);
    }

    /// <summary>
    /// How fast an entity moves during an alert state (flee / charge). Boars
    /// charge faster than rabbits flee because crowd-pleaser physics - a
    /// wounded boar should feel genuinely scary.
    /// </summary>
    private static float AlertSpeedForKind(EntityKind k) => k switch
    {
        EntityKind.Boar   => 3.0f,
        EntityKind.Rabbit => 2.8f,
        EntityKind.Crow   => 2.4f,
        EntityKind.Wolf   => 3.5f, // wolves are the fastest chasers
        _                 => 2.0f,
    };

    /// <summary>
    /// Damage dealt to the player on a successful contact hit. Rabbits / crows
    /// don't charge so they never call this, but the switch returns a small
    /// nonzero for them so any future "rabid rabbit" variant has a bite.
    /// </summary>
    private static float ContactDamageForKind(EntityKind k) => k switch
    {
        EntityKind.Boar   => 0.20f,
        EntityKind.Rabbit => 0.05f,
        EntityKind.Crow   => 0.05f,
        EntityKind.Wolf   => 0.25f, // wolves bite harder than boars
        _                 => 0.05f,
    };

    private void PickRandomDirection(WanderingEntity e)
    {
        // Random horizontal direction, no Y component (gravity handled by ground snap).
        double angle = _rng.NextDouble() * Math.PI * 2;
        float vx = (float)Math.Cos(angle) * WanderSpeed;
        float vz = (float)Math.Sin(angle) * WanderSpeed;
        e.Velocity = new Vector3(vx, 0, vz);
    }

    /// <summary>
    /// Fires once when an attack lands (before the kill check). HudService can
    /// hook this for a red flash / hit indicator, and AI hooks will use it
    /// later for flee/aggro reactions.
    /// </summary>
    public event Action<WanderingEntity>? OnEntityHit;

    /// <summary>
    /// Fires once when an entity's health drops to zero and it has been
    /// removed from the active list. Game.razor listens so it can award loot
    /// drops to the player's inventory.
    /// </summary>
    public event Action<WanderingEntity>? OnEntityKilled;

    /// <summary>
    /// Scan all wandering entities and return the closest one that falls
    /// inside the aim cone (minDot) and within maxDist of origin. Returns null
    /// if nothing's in range - Game.razor falls through to block-break in that
    /// case.
    ///
    /// We compare a horizontal-biased aim vector so the test doesn't require
    /// the player to aim pixel-perfect at the billboard's vertical center.
    /// </summary>
    public WanderingEntity? FindTargetInCone(Vector3 origin, Vector3 forward, float maxDist, float minDot)
    {
        WanderingEntity? best = null;
        float bestDist = float.PositiveInfinity;

        float fwdLen = forward.Length();
        if (fwdLen < 1e-4f) return null;
        var fwdN = forward / fwdLen;

        foreach (var e in Entities)
        {
            // Aim at the billboard's chest, not its feet (matches the +0.8 offset
            // HudService uses when it projects the billboard).
            var target = new Vector3(e.Position.X, e.Position.Y + 0.8f, e.Position.Z);
            var delta = target - origin;
            float d = delta.Length();
            if (d > maxDist || d < 1e-3f) continue;

            var dirN = delta / d;
            float dot = Vector3.Dot(fwdN, dirN);
            if (dot < minDot) continue;

            if (d < bestDist)
            {
                bestDist = d;
                best = e;
            }
        }
        return best;
    }

    /// <summary>
    /// Apply damage to an entity from a specific attacker position. Fires
    /// OnEntityHit, then OnEntityKilled + removes from the active list if
    /// health hits zero. On non-fatal hits the entity enters the alert state
    /// appropriate to its kind (rabbit/crow flee, boar charges) for a few
    /// seconds so combat feels reactive. Returns true when the hit was fatal.
    /// </summary>
    public bool ApplyDamage(WanderingEntity e, float amount, Vector3 attackerPos)
    {
        if (amount <= 0) return false;
        e.Health = Math.Max(0f, e.Health - amount);
        e.HitFlashTimer = 0.15f;
        OnEntityHit?.Invoke(e);

        if (e.Health <= 0f)
        {
            Entities.Remove(e);
            OnEntityKilled?.Invoke(e);
            return true;
        }

        // Non-fatal: enter alert state so the player sees a reaction. Flee
        // duration is intentionally long enough that a single axe swing
        // actually moves the rabbit out of immediate swing range.
        e.LastAlertSource = attackerPos;
        e.Alert = (e.Kind == EntityKind.Boar || e.Kind == EntityKind.Wolf)
            ? AlertMode.Charge
            : AlertMode.Flee;
        e.AlertTimer = 3.0f;
        return false;
    }
}
