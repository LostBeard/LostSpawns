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
    public float WanderRetargetIn; // seconds until next random-direction pick
    public AlertMode Alert;
    public float AlertTimer;       // seconds remaining in current alert state
    public Vector3 LastAlertSource; // player pos at time of last hit
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
        };
        PickRandomDirection(e);
        Entities.Add(e);
        return e;
    }

    /// <summary>Advance every entity. Call from the active-play branch only.</summary>
    public void Tick(float dt, WorldService world)
    {
        foreach (var e in Entities)
        {
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
                    var toward = new Vector3(
                        e.LastAlertSource.X - e.Position.X,
                        0,
                        e.LastAlertSource.Z - e.Position.Z);
                    float len = toward.Length();
                    if (len > 1e-3f)
                    {
                        var dir = toward / len;
                        float speed = AlertSpeedForKind(e.Kind);
                        // Flee flips the sign so the entity runs the other way.
                        if (e.Alert == AlertMode.Flee) dir = -dir;
                        e.Velocity = dir * speed;
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
        _                 => 2.0f,
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
        e.Alert = e.Kind == EntityKind.Boar ? AlertMode.Charge : AlertMode.Flee;
        e.AlertTimer = 3.0f;
        return false;
    }
}
