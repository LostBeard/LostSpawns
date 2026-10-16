using System.Numerics;
using LostSpawns.Services;
using SpawnDev.SpawnJS.JSObjects;
using SpawnDev.VoxelEngine.Rendering;

namespace LostSpawns.Rendering;

/// <summary>
/// The wildlife, drawn as models of textured boxes by the engine's <see cref="EntityRenderPipeline"/>: every part of every creature in
/// one instanced draw, lit and fogged by the same frame uniforms as the ground, so a wolf in the shade of a spruce is dark like the
/// ground beside it. A walk cycle runs from how far each one has walked, the head and tail follow its mood.
/// </summary>
public sealed class CreatureRenderer : IDisposable
{
    private const float Deg = MathF.PI / 180f;

    private readonly EntityRenderPipeline _pipe = new();
    private EntityTextureAtlas? _atlas;
    private EntityPartInstance[] _parts = new EntityPartInstance[2048];
    private Matrix4x4[] _scratch = new Matrix4x4[16];
    private readonly Dictionary<int, State> _states = new();
    private readonly List<int> _stale = new();
    private int _frame;
    private bool _disposed;

    private sealed class State
    {
        public float Yaw;          // degrees, Minecraft's yaw (0 = south, 90 = west)
        public float Phase;        // radians of stride
        public double LastTime;
        public bool HasYaw;
        public int LastFrame;
    }

    /// <summary>Parts drawn by the last <see cref="Prepare"/>.</summary>
    public int PartCount { get; private set; }
    /// <summary>Creatures drawn by the last <see cref="Prepare"/>.</summary>
    public int CreatureCount { get; private set; }

    public void Init(GPUDevice device, GPUQueue queue, string colorFormat, GPUBuffer frameBuffer)
    {
        _pipe.Init(device, queue, colorFormat, frameBuffer);
        _atlas = EntityTextureAtlas.FromRgba(device, queue, CreatureTextures.Build(), CreatureTextures.Width, CreatureTextures.Height);
        _pipe.SetTextures(_atlas);
    }

    /// <summary>The matrix that takes model pixels to world blocks for a creature at (x, y, z) facing <paramref name="yawDegrees"/> (the game's renderer order).</summary>
    private static Matrix4x4 Root(float x, float y, float z, float yawDegrees, float scale) =>
        Matrix4x4.CreateScale(1f / 16f)
        * Matrix4x4.CreateTranslation(0f, -1.501f, 0f)
        * Matrix4x4.CreateScale(scale)
        * Matrix4x4.CreateScale(-1f, -1f, 1f)
        * Matrix4x4.CreateRotationY((180f - yawDegrees) * Deg)
        * Matrix4x4.CreateTranslation(x, y, z);

    private static float TurnToward(float from, float to, float amount)
    {
        float d = ((to - from) % 360f + 540f) % 360f - 180f;
        return from + d * amount;
    }

    /// <summary>Pose every creature within <paramref name="maxDistance"/> of the camera and upload the boxes. Call before the pass.</summary>
    public void Prepare(IReadOnlyList<WanderingEntity> entities, Vector3 camera, double time, float maxDistance)
    {
        if (!_pipe.IsReady) return;
        _frame++;
        int count = 0, creatures = 0;
        float maxSq = maxDistance * maxDistance;
        foreach (var e in entities)
        {
            float dx = e.Position.X - camera.X, dz = e.Position.Z - camera.Z;
            if (dx * dx + dz * dz > maxSq) continue;

            if (!_states.TryGetValue(e.Id, out var st))
            {
                st = new State { LastTime = time };
                _states[e.Id] = st;
            }
            st.LastFrame = _frame;
            float dt = (float)Math.Clamp(time - st.LastTime, 0.0, 0.1);
            st.LastTime = time;

            var model = CreatureModels.For(e.Kind);
            float speed = MathF.Sqrt(e.Velocity.X * e.Velocity.X + e.Velocity.Z * e.Velocity.Z);
            if (speed > 0.05f)
            {
                float target = MathF.Atan2(-e.Velocity.X, e.Velocity.Z) / Deg;
                st.Yaw = st.HasYaw ? TurnToward(st.Yaw, target, 1f - MathF.Exp(-9f * dt)) : target;
                st.HasYaw = true;
            }
            st.Phase += dt * speed * model.StrideRate;

            count = Pose(model, e, st, speed, (float)time, count);
            creatures++;
        }
        // Forget creatures that are gone
        _stale.Clear();
        foreach (var (id, st) in _states)
            if (st.LastFrame != _frame && _frame - st.LastFrame > 120) _stale.Add(id);
        foreach (var id in _stale) _states.Remove(id);

        PartCount = _pipe.SetParts(_parts.AsSpan(0, count));
        CreatureCount = creatures;
    }

    private int Pose(CreatureModel model, WanderingEntity e, State st, float speed, float time, int count)
    {
        bool charging = e.Alert == AlertMode.Charge;
        float amp = Math.Clamp(speed / 1.6f, 0f, 1.2f);
        float id = e.Id * 1.7f;
        float swing = MathF.Sin(st.Phase) * model.Swing * amp;

        // A bigger wolf is a harder one: the day-scaled alphas are drawn larger
        float scale = model.Scale;
        if (e.Kind == EntityKind.Wolf && e.MaxHealth > 1.8f) scale *= 1f + 0.3f * (MathF.Min(e.MaxHealth / 1.8f, 1.6f) - 1f) / 0.6f;

        // A creature's position is one block over the ground (the top face of the block it stands on); a crow hangs in the air at it
        float groundY = e.Kind == EntityKind.Crow ? e.Position.Y + 0.15f * MathF.Sin(time * 2.2f + id) : e.Position.Y;
        // A rabbit hops: it leaves the ground at the top of each stride
        if (e.Kind == EntityKind.Rabbit && amp > 0.05f) groundY += MathF.Abs(MathF.Sin(st.Phase)) * 0.22f * amp;
        var root = Root(e.Position.X, groundY, e.Position.Z, st.Yaw, scale);

        var tint = Vector3.One * (0.92f + 0.16f * e.ColorJitter);
        if (e.HitFlashTimer > 0f) tint = new Vector3(1f, 0.35f, 0.35f);

        if (_scratch.Length < model.Parts.Length) _scratch = new Matrix4x4[model.Parts.Length];
        if (count + model.Parts.Length > _parts.Length) System.Array.Resize(ref _parts, Math.Max(_parts.Length * 2, count + model.Parts.Length));

        for (int i = 0; i < model.Parts.Length; i++)
        {
            var part = model.Parts[i];
            var rot = part.BaseRotation;
            switch (part.Gait)
            {
                case PartGait.FrontLegA: case PartGait.HindLegA: rot.X += swing; break;
                case PartGait.FrontLegB: case PartGait.HindLegB: rot.X -= swing; break;
                case PartGait.Head:
                    rot.X += MathF.Sin(time * 0.8f + id) * 0.06f + MathF.Sin(st.Phase * 2f) * 0.05f * amp + (charging ? 0.35f : 0f);
                    rot.Y += MathF.Sin(time * 0.35f + id * 2.3f) * 0.18f * (1f - amp);   // looks about when standing
                    break;
                case PartGait.Tail:
                    rot.Y += MathF.Sin(time * (2.5f + speed * 2f) + id) * (charging ? 0.15f : 0.4f);
                    break;
                case PartGait.EarLeft:
                    rot.Z += 0.08f + MathF.Pow(MathF.Max(MathF.Sin(time * 0.6f + id * 3.1f), 0f), 24f) * 0.5f;
                    break;
                case PartGait.EarRight:
                    rot.Z -= 0.08f + MathF.Pow(MathF.Max(MathF.Sin(time * 0.6f + id * 5.3f), 0f), 24f) * 0.5f;
                    break;
                case PartGait.WingLeft:
                    rot.Z += 0.25f + MathF.Sin(time * 17f + id) * 0.95f;
                    break;
                case PartGait.WingRight:
                    rot.Z -= 0.25f + MathF.Sin(time * 17f + id) * 0.95f;
                    break;
            }
            var pivot = part.PivotPoint;
            var local = Matrix4x4.CreateTranslation(-pivot)
                * Matrix4x4.CreateRotationX(rot.X) * Matrix4x4.CreateRotationY(rot.Y) * Matrix4x4.CreateRotationZ(rot.Z)
                * Matrix4x4.CreateTranslation(pivot);
            _scratch[i] = part.Parent >= 0 ? local * _scratch[part.Parent] : local;

            _parts[count++] = new EntityPartInstance
            {
                Model = _scratch[i] * root,
                OriginInflate = new Vector4(part.Origin, 0f),
                SizeFlags = new Vector4(part.Size, 0f),
                Uv = new Vector4(part.Uv.X, part.Uv.Y, 0f, 0f),
                Tint = new Vector4(tint, 0f),
            };
        }
        return count;
    }

    /// <summary>Draw the creatures posed by <see cref="Prepare"/>, inside the world's pass after the solid blocks and before the water.</summary>
    public void Draw(GPURenderPassEncoder pass) { if (PartCount > 0) _pipe.Draw(pass); }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _pipe.Dispose();
        _atlas?.Dispose();
    }
}
