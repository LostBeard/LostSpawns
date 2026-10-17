using System.Numerics;
using LostSpawns.Services;
using SpawnDev.SpawnJS.JSObjects;
using SpawnDev.VoxelEngine.Rendering;

namespace LostSpawns.Rendering;

/// <summary>What the player holds, for the first-person view.</summary>
/// <param name="ItemId">The held item's id (null: empty-handed).</param>
/// <param name="SwingArc">0..1..0 across a swing (0 at rest), <paramref name="SwingStrength"/> how hard it was thrown.</param>
public readonly record struct HeldView(string? ItemId, float SwingArc, float SwingStrength, float BobX, float BobY);

/// <summary>
/// The things that are made of boxes, drawn by the engine's <see cref="EntityRenderPipeline"/> in one instanced draw, lit and fogged by
/// the same frame uniforms as the ground: the wildlife (with a walk cycle from how far each has walked, the head and tail following
/// its mood), campfires and torches, loot on the ground, and the tool in the player's hand. A frame is built with
/// <see cref="Begin"/>, the <c>Add</c> calls and <see cref="Commit"/>, before the render pass.
/// </summary>
public sealed class CreatureRenderer : IDisposable
{
    private const float Deg = MathF.PI / 180f;

    private readonly EntityRenderPipeline _pipe = new();
    private EntityTextureAtlas? _atlas;
    private EntityPartInstance[] _parts = new EntityPartInstance[2048];
    private int _count;
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

    /// <summary>Parts drawn by the last <see cref="Commit"/>.</summary>
    public int PartCount { get; private set; }
    /// <summary>Creatures posed since the last <see cref="Begin"/>.</summary>
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

    /// <summary>Start a frame: nothing posed yet.</summary>
    public void Begin()
    {
        _frame++;
        _count = 0;
        CreatureCount = 0;
    }

    /// <summary>Upload the frame's boxes. Call before the render pass.</summary>
    public void Commit()
    {
        if (!_pipe.IsReady) return;
        // Forget creatures that are gone
        _stale.Clear();
        foreach (var (id, st) in _states)
            if (_frame - st.LastFrame > 120) _stale.Add(id);
        foreach (var id in _stale) _states.Remove(id);

        PartCount = _pipe.SetParts(_parts.AsSpan(0, _count));
    }

    private void Reserve(int more)
    {
        if (_count + more > _parts.Length) System.Array.Resize(ref _parts, Math.Max(_parts.Length * 2, _count + more));
        if (_scratch.Length < more) _scratch = new Matrix4x4[Math.Max(more, 16)];
    }

    /// <summary>Pose every creature within <paramref name="maxDistance"/> of the camera.</summary>
    public void AddCreatures(IReadOnlyList<WanderingEntity> entities, Vector3 camera, double time, float maxDistance)
    {
        if (!_pipe.IsReady) return;
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

            PoseCreature(model, e, st, speed, (float)time);
            CreatureCount++;
        }
    }

    private void PoseCreature(CreatureModel model, WanderingEntity e, State st, float speed, float time)
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
        bool grazing = e.Doing == EntityActivity.Graze && e.Alert == AlertMode.Idle && speed < 0.2f;
        bool resting = e.Doing == EntityActivity.Rest && e.Alert == AlertMode.Idle && speed < 0.2f && e.Kind != EntityKind.Crow;
        // Lying down: the body sinks onto the ground, the legs folded under it
        if (resting) groundY -= 0.30f * scale;
        var root = Root(e.Position.X, groundY, e.Position.Z, st.Yaw, scale);

        var tint = Vector3.One * (0.92f + 0.16f * e.ColorJitter);
        if (e.Runner) tint = new Vector3(1.25f, 0.75f, 0.7f);   // a runner is flushed and raw
        if (e.HitFlashTimer > 0f) tint = new Vector3(1f, 0.35f, 0.35f);

        Reserve(model.Parts.Length);
        for (int i = 0; i < model.Parts.Length; i++)
        {
            var part = model.Parts[i];
            var rot = part.BaseRotation;
            switch (part.Gait)
            {
                case PartGait.FrontLegA: rot.X += resting ? -1.45f : swing; break;
                case PartGait.HindLegA: rot.X += resting ? 1.45f : swing; break;
                case PartGait.FrontLegB: rot.X += resting ? -1.45f : -swing; break;
                case PartGait.HindLegB: rot.X += resting ? 1.45f : -swing; break;
                case PartGait.Head:
                    rot.X += MathF.Sin(time * 0.8f + id) * 0.06f + MathF.Sin(st.Phase * 2f) * 0.05f * amp + (charging ? 0.35f : 0f);
                    // Grazing: the head down at the grass, chewing; resting: the head sunk and still
                    if (grazing) rot.X += 1.05f + MathF.Sin(time * 5f + id) * 0.04f;
                    else if (resting) rot.X += 0.35f;
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
            Emit(part, i, rot, in root, tint);
        }
    }

    // One part: turned about its pivot, carried by its parent, placed by the root
    private void Emit(ModelPart part, int index, Vector3 rot, in Matrix4x4 root, Vector3 tint)
    {
        var pivot = part.PivotPoint;
        var local = Matrix4x4.CreateTranslation(-pivot)
            * Matrix4x4.CreateRotationX(rot.X) * Matrix4x4.CreateRotationY(rot.Y) * Matrix4x4.CreateRotationZ(rot.Z)
            * Matrix4x4.CreateTranslation(pivot);
        _scratch[index] = part.Parent >= 0 ? local * _scratch[part.Parent] : local;
        _parts[_count++] = new EntityPartInstance
        {
            Model = _scratch[index] * root,
            OriginInflate = new Vector4(part.Origin, 0f),
            SizeFlags = new Vector4(part.Size, 0f),
            Uv = new Vector4(part.Uv.X, part.Uv.Y, 0f, 0f),
            Tint = new Vector4(tint, 0f),
        };
    }

    // A model that does not move: placed, turned and scaled
    private void AddStatic(CreatureModel model, Vector3 position, float yawDegrees, float scale, Vector3 tint)
    {
        Reserve(model.Parts.Length);
        var root = Root(position.X, position.Y, position.Z, yawDegrees, scale);
        for (int i = 0; i < model.Parts.Length; i++)
            Emit(model.Parts[i], i, model.Parts[i].BaseRotation, in root, tint);
    }

    /// <summary>Campfires, bonfires and torches within range: logs and stones (the flames and the smoke are particles).</summary>
    public void AddFires(IReadOnlyList<Campfire> fires, Vector3 camera, float maxDistance)
    {
        if (!_pipe.IsReady) return;
        float maxSq = maxDistance * maxDistance;
        foreach (var f in fires)
        {
            float dx = f.Position.X - camera.X, dz = f.Position.Z - camera.Z;
            if (dx * dx + dz * dz > maxSq) continue;
            var model = f.Radius <= 3f ? PropModels.Torch : PropModels.Campfire;
            float scale = f.Radius >= 10f ? 1.7f : 1f;
            // A fire that has burned out is dark
            var tint = f.Fuel > 0.02f ? Vector3.One : new Vector3(0.45f);
            // In the middle of its block, where the flames are
            var centre = new Vector3(MathF.Floor(f.Position.X) + 0.5f, f.Position.Y, MathF.Floor(f.Position.Z) + 0.5f);
            AddStatic(model, centre, f.Id * 47f, scale, tint);
        }
    }

    /// <summary>The player's bedroll, when it is laid and within range.</summary>
    public void AddBedroll(Vector3? bedroll, Vector3 camera, float maxDistance)
    {
        if (!_pipe.IsReady || bedroll is not { } p) return;
        float dx = p.X - camera.X, dz = p.Z - camera.Z;
        if (dx * dx + dz * dz > maxDistance * maxDistance) return;
        AddStatic(PropModels.Bedroll, p, 17f, 1f, Vector3.One);
    }

    /// <summary>Loot on the ground: a model of the item, turning slowly and bobbing so it catches the eye in the grass.</summary>
    public void AddItems(IReadOnlyList<GroundItem> items, Vector3 camera, double time, float maxDistance)
    {
        if (!_pipe.IsReady) return;
        float maxSq = maxDistance * maxDistance;
        foreach (var g in items)
        {
            float dx = g.Position.X - camera.X, dz = g.Position.Z - camera.Z;
            if (dx * dx + dz * dz > maxSq) continue;
            var (model, scale) = PropModels.ForItem(g.Payload.Id);
            float t = (float)time;
            var pos = g.Position + new Vector3(0f, 0.06f + 0.05f * MathF.Sin(t * 2.1f + g.Id), 0f);
            AddStatic(model, pos, t * 35f + g.Id * 61f, scale * 1.15f, Vector3.One);
        }
    }

    /// <summary>The arm and tool the player holds, in front of the camera: it follows the swing and the walking sway.</summary>
    public void AddHeld(in HeldView held, Camera camera)
    {
        if (!_pipe.IsReady) return;
        var f = camera.Front;
        var r = camera.Right;
        var u = camera.Up;
        // Camera space as a matrix: x right, y up, z backward (toward the player), at the eye
        var basis = new Matrix4x4(
            r.X, r.Y, r.Z, 0f,
            u.X, u.Y, u.Z, 0f,
            -f.X, -f.Y, -f.Z, 0f,
            camera.Position.X, camera.Position.Y, camera.Position.Z, 1f);

        float arc = held.SwingArc * held.SwingStrength;
        // Where the grip is: low and to the right, a little ahead; a swing throws it forward and down
        var anchor = new Vector3(0.30f + held.BobX * 0.35f, -0.34f + held.BobY * 0.35f, -0.62f);
        anchor += new Vector3(-0.10f * arc, -0.10f * arc, -0.16f * arc);
        float pitch = -0.50f - 1.05f * arc;       // top tilted away from the eye, then chopping forward
        float roll = 0.30f - 0.25f * arc;
        float yaw = 0.30f;

        const float PixelScale = 0.44f;
        var toWorld = Matrix4x4.CreateScale(PixelScale / 16f) * Matrix4x4.CreateScale(-1f, -1f, 1f)
            * Matrix4x4.CreateRotationZ(roll) * Matrix4x4.CreateRotationX(pitch) * Matrix4x4.CreateRotationY(yaw)
            * Matrix4x4.CreateTranslation(anchor) * basis;

        // The arm always, the tool when there is one
        EmitHeld(HeldModels.Arm, in toWorld);
        var tool = held.ItemId is null ? null : HeldModels.For(held.ItemId);
        if (tool != null) EmitHeld(tool, in toWorld);
    }

    /// <summary>
    /// A hand in the room (the headset): the arm reaches back from the controller toward the body, and the tool it holds (if any) points the way the controller
    /// does, tilted up a little and swinging down through an arc.
    /// </summary>
    public void AddHeldAt(in HeldView held, Vector3 handPosition, Vector3 pointing)
    {
        if (!_pipe.IsReady) return;
        var forward = pointing.LengthSquared() < 1e-8f ? -Vector3.UnitZ : Vector3.Normalize(pointing);
        var side = Vector3.Cross(forward, Vector3.UnitY);
        side = side.LengthSquared() < 1e-8f ? Vector3.UnitX : Vector3.Normalize(side);
        float arc = held.SwingArc * held.SwingStrength;
        var toolDir = Vector3.Normalize(forward + Vector3.UnitY * 0.9f);
        toolDir = Vector3.Transform(toolDir, Quaternion.CreateFromAxisAngle(side, 1.7f * arc));
        var grip = handPosition + forward * 0.05f;
        EmitHeld(HeldModels.Arm, HandMatrix(toolDir, grip, 0.28f));
        var tool = held.ItemId is null ? null : HeldModels.For(held.ItemId);
        if (tool != null) EmitHeld(tool, HandMatrix(toolDir, grip, 0.5f));
    }

    /// <summary>
    /// A controller's arm in the room, rigid with it: the forearm and fist in the controller's own frame (<paramref name="controllerWorld"/>: local to world, the grip at its
    /// origin), the tool (if any) in the fist pointing forward and a little up, and on the left a device strapped to the forearm. A swing of the tool dips it about the wrist.
    /// </summary>
    public void AddArmAt(in Matrix4x4 controllerWorld, bool left, in HeldView held)
    {
        if (!_pipe.IsReady) return;
        const float Cm = 0.16f;   // PixelScale: 16 px of model = 16 cm
        var flip = Matrix4x4.CreateScale(Cm / 16f) * Matrix4x4.CreateScale(-1f, -1f, 1f);
        var toWorld = flip * controllerWorld;
        EmitHeld(HeldModels.Forearm, in toWorld);
        if (left) EmitHeld(HeldModels.WristDevice, in toWorld);
        var tool = held.ItemId is null || left ? null : HeldModels.For(held.ItemId);
        if (tool == null) return;
        // The tool's working end is +y once flipped; turn it to the controller's -z, tilted 35 degrees up, and a swing dips it up to 80 degrees more
        float arc = held.SwingArc * held.SwingStrength;
        float angle = (-55f - 80f * arc) * MathF.PI / 180f;
        var toolWorld = Matrix4x4.CreateScale(0.5f / 16f) * Matrix4x4.CreateScale(-1f, -1f, 1f) * Matrix4x4.CreateRotationX(angle) * controllerWorld;
        EmitHeld(tool, in toolWorld);
    }

    /// <summary>The other hand: just the arm, held out the way the controller points.</summary>
    public void AddHandAt(Vector3 handPosition, Vector3 pointing)
    {
        if (!_pipe.IsReady) return;
        var forward = pointing.LengthSquared() < 1e-8f ? -Vector3.UnitZ : Vector3.Normalize(pointing);
        EmitHeld(HeldModels.Arm, HandMatrix(Vector3.Normalize(forward + Vector3.UnitY * 0.6f), handPosition + forward * 0.05f, 0.28f));
    }

    // A model of the held kind (its working end at -y, the grip at the origin) put with that end along toolDir
    private static Matrix4x4 HandMatrix(Vector3 toolDir, Vector3 grip, float pixelScale)
    {
        var axis = Vector3.Cross(Vector3.UnitY, toolDir);
        var turn = axis.LengthSquared() < 1e-10f ? Quaternion.Identity
            : Quaternion.CreateFromAxisAngle(Vector3.Normalize(axis), MathF.Acos(Math.Clamp(Vector3.Dot(Vector3.UnitY, toolDir), -1f, 1f)));
        // The model is y-down; flipped, the working end is at +y, which is turned to toolDir
        return Matrix4x4.CreateScale(pixelScale / 16f) * Matrix4x4.CreateScale(-1f, -1f, 1f)
            * Matrix4x4.CreateFromQuaternion(turn) * Matrix4x4.CreateTranslation(grip);
    }

    private void EmitHeld(CreatureModel model, in Matrix4x4 toWorld)
    {
        Reserve(model.Parts.Length);
        for (int i = 0; i < model.Parts.Length; i++)
            Emit(model.Parts[i], i, model.Parts[i].BaseRotation, in toWorld, Vector3.One);
    }

    /// <summary>Draw what was posed, inside the world's pass after the solid blocks and before the water.</summary>
    public void Draw(GPURenderPassEncoder pass) { if (PartCount > 0) _pipe.Draw(pass); }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _pipe.Dispose();
        _atlas?.Dispose();
    }
}
