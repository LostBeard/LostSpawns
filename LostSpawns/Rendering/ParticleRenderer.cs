using System.Numerics;
using LostSpawns.Services;
using SpawnDev.SpawnJS.JSObjects;
using SpawnDev.VoxelEngine.Rendering;
using static LostSpawns.Rendering.TileNoise;

namespace LostSpawns.Rendering;

/// <summary>The kinds of particle (the index an emitter names).</summary>
public enum ParticleKinds { Flame = 0, Smoke = 1, Spark = 2, Firefly = 3, Dust = 4, Blood = 5, Glow = 6 }

/// <summary>
/// Flames, smoke, sparks, fireflies, dust and blood: the engine's <see cref="ParticleRenderPipeline"/> draws every particle of every
/// emitter in one instanced draw, each one born, risen and faded by its kind from the frame's clock alone. This class is the list of
/// emitters near the camera (a fire each, a firefly cell each, a short-lived emitter for a puff of dust where a block broke) and the
/// painted sprites.
/// </summary>
public sealed class ParticleRenderer : IDisposable
{
    private readonly ParticleRenderPipeline _pipe = new();
    private EntityTextureAtlas? _atlas;
    private readonly List<ParticleEmitterInstance> _emitters = new(256);
    private readonly List<(double until, ParticleEmitterInstance emitter)> _bursts = new();
    private bool _disposed;

    private const int AtlasW = 64, AtlasH = 80;
    // Sprite places in the atlas (x, y, frame w, frame h)
    private static readonly int[] FlameRect = { 0, 0, 16, 16 };
    private static readonly int[] SmokeRect = { 0, 16, 16, 16 };
    private static readonly int[] SparkRect = { 0, 32, 8, 8 };
    private static readonly int[] BloodRect = { 0, 40, 8, 8 };
    // A soft glow needs more texels than a flame: sprites are fetched exactly, so an 8 px dot would be a few big squares
    private static readonly int[] GlowRect = { 32, 32, 32, 32 };
    private static readonly int[] DustRect = { 0, 64, 16, 16 };

    public int EmitterCount { get; private set; }

    public void Init(GPUDevice device, GPUQueue queue, string colorFormat, GPUBuffer frameBuffer)
    {
        _pipe.Init(device, queue, colorFormat, frameBuffer);
        _atlas = EntityTextureAtlas.FromRgba(device, queue, BuildAtlas(), AtlasW, AtlasH);
        _pipe.SetTextures(_atlas);
        _pipe.SetKinds(Kinds());
    }

    private static Vector4 Sprite(int[] r) => new(r[0], r[1], r[2], r[3]);

    private static ParticleKind[] Kinds()
    {
        var kinds = new ParticleKind[7];
        // Counts: alive at once, life (s), flags, frames. Motion: velocity (x, y, z) and gravity along y. Scatter: spawn spread, sideways speed, wobble, wobble speed.
        // Fade: size at birth and death, opacity at birth and death. Origin: where in the block a particle is born, and how far up or down it may start.
        kinds[(int)ParticleKinds.Flame] = new ParticleKind
        {
            Counts = new Vector4(12, 0.85f, ParticleKind.FlagEmissive, 4), Sprite = Sprite(FlameRect),
            Motion = new Vector4(0f, 0.95f, 0f, 0.7f), Scatter = new Vector4(0.14f, 0.10f, 0.07f, 7f),
            Fade = new Vector4(0.85f, 0.20f, 1f, 0f), Tint = new Vector4(1f, 0.9f, 0.75f, 1f), Origin = new Vector4(0.5f, 0.08f, 0.5f, 0.08f),
        };
        kinds[(int)ParticleKinds.Smoke] = new ParticleKind
        {
            Counts = new Vector4(12, 3.4f, 0f, 4), Sprite = Sprite(SmokeRect),
            Motion = new Vector4(0.12f, 0.75f, 0f, 0.1f), Scatter = new Vector4(0.18f, 0.18f, 0.55f, 1.3f),
            Fade = new Vector4(0.35f, 1.4f, 0.5f, 0f), Tint = new Vector4(0.62f, 0.62f, 0.64f, 1f), Origin = new Vector4(0.5f, 0.75f, 0.5f, 0.15f),
        };
        kinds[(int)ParticleKinds.Spark] = new ParticleKind
        {
            Counts = new Vector4(6, 1.2f, ParticleKind.FlagEmissive, 1), Sprite = Sprite(SparkRect),
            Motion = new Vector4(0f, 1.9f, 0f, -0.9f), Scatter = new Vector4(0.14f, 0.55f, 0.05f, 9f),
            Fade = new Vector4(0.07f, 0.03f, 1f, 0f), Tint = new Vector4(1f, 0.72f, 0.28f, 1f), Origin = new Vector4(0.5f, 0.45f, 0.5f, 0.1f),
        };
        kinds[(int)ParticleKinds.Firefly] = new ParticleKind
        {
            Counts = new Vector4(3, 6f, ParticleKind.FlagEmissive, 1), Sprite = Sprite(GlowRect),
            Motion = new Vector4(0f, 0.03f, 0f, 0f), Scatter = new Vector4(0.7f, 0.08f, 0.9f, 1.4f),
            Fade = new Vector4(0.11f, 0.11f, 0.95f, 0.1f), Tint = new Vector4(0.85f, 1f, 0.35f, 1f), Origin = new Vector4(0.5f, 0.6f, 0.5f, 0.9f),
        };
        // A wide soft glow round a fire at night (its light on the air; the ground is not lit by it until the engine's block light is)
        kinds[(int)ParticleKinds.Glow] = new ParticleKind
        {
            Counts = new Vector4(1, 60f, ParticleKind.FlagEmissive, 1), Sprite = Sprite(GlowRect),
            Motion = new Vector4(0f, 0f, 0f, 0f), Scatter = new Vector4(0f, 0f, 0f, 0f),
            Fade = new Vector4(3.4f, 3.4f, 0.20f, 0.20f), Tint = new Vector4(1f, 0.52f, 0.16f, 1f), Origin = new Vector4(0.5f, 0.55f, 0.5f, 0f),
        };
        kinds[(int)ParticleKinds.Dust] = new ParticleKind
        {
            Counts = new Vector4(10, 0.8f, 0f, 4), Sprite = Sprite(DustRect),
            Motion = new Vector4(0f, 1.1f, 0f, -1.6f), Scatter = new Vector4(0.28f, 1.3f, 0.08f, 5f),
            Fade = new Vector4(0.22f, 0.6f, 0.8f, 0f), Tint = new Vector4(0.62f, 0.52f, 0.40f, 1f), Origin = new Vector4(0.5f, 0.5f, 0.5f, 0.25f),
        };
        kinds[(int)ParticleKinds.Blood] = new ParticleKind
        {
            Counts = new Vector4(9, 0.7f, 0f, 1), Sprite = Sprite(BloodRect),
            Motion = new Vector4(0f, 2.2f, 0f, -9f), Scatter = new Vector4(0.1f, 1.7f, 0f, 0f),
            Fade = new Vector4(0.07f, 0.05f, 1f, 0.6f), Tint = new Vector4(0.62f, 0.03f, 0.05f, 1f), Origin = new Vector4(0.5f, 0.6f, 0.5f, 0.2f),
        };
        return kinds;
    }

    /// <summary>Start a frame: the emitters of fires and fireflies are rebuilt, bursts that are over are dropped.</summary>
    public void Begin(double time)
    {
        _emitters.Clear();
        _bursts.RemoveAll(b => b.until < time);
    }

    /// <summary>A flame, its sparks and its smoke over each fire that burns; smoke alone over one that is dying.</summary>
    public void AddFires(IReadOnlyList<Campfire> fires, Vector3 camera, float maxDistance, float daylight)
    {
        float maxSq = maxDistance * maxDistance;
        foreach (var f in fires)
        {
            float dx = f.Position.X - camera.X, dz = f.Position.Z - camera.Z;
            if (dx * dx + dz * dz > maxSq) continue;
            int x = (int)MathF.Floor(f.Position.X), y = (int)MathF.Floor(f.Position.Y), z = (int)MathF.Floor(f.Position.Z);
            bool torch = f.Radius <= 3f;
            if (f.Fuel > 0.05f)
            {
                _emitters.Add(new ParticleEmitterInstance(x, y + (torch ? 1 : 0), z, (int)ParticleKinds.Flame));
                // A bonfire is bigger: a second flame
                if (f.Radius >= 10f) _emitters.Add(new ParticleEmitterInstance(x, y, z, (int)ParticleKinds.Flame));
                _emitters.Add(new ParticleEmitterInstance(x, y + (torch ? 1 : 0), z, (int)ParticleKinds.Spark));
                // At dusk and night its glow lights the air round it
                if (daylight < 0.7f) _emitters.Add(new ParticleEmitterInstance(x, y + (torch ? 1 : 0), z, (int)ParticleKinds.Glow));
            }
            if (!torch || f.Fuel > 0.05f) _emitters.Add(new ParticleEmitterInstance(x, y + (torch ? 1 : 0), z, (int)ParticleKinds.Smoke));
        }
    }

    /// <summary>A few fireflies over the meadow at dusk and at night, in cells fixed to the world (so each hovers in its own place).</summary>
    public void AddFireflies(WorldService world, Vector3 camera, float night, float rain)
    {
        float strength = night * (1f - rain);
        if (strength < 0.25f) return;
        const int Cell = 4, Radius = 7;
        int cx0 = (int)MathF.Floor(camera.X / Cell), cz0 = (int)MathF.Floor(camera.Z / Cell);
        for (int dz = -Radius; dz <= Radius; dz++)
        for (int dx = -Radius; dx <= Radius; dx++)
        {
            int cx = cx0 + dx, cz = cz0 + dz;
            if (Rand(cx, cz, 901) > 0.16f * strength) continue;
            int x = cx * Cell + (int)(Rand(cx, cz, 902) * Cell), z = cz * Cell + (int)(Rand(cx, cz, 903) * Cell);
            int ground = world.GetHeightAt(x, z);
            if (ground <= 66) continue;     // not over water
            _emitters.Add(new ParticleEmitterInstance(x, ground + 1, z, (int)ParticleKinds.Firefly));
        }
    }

    /// <summary>A short-lived emitter at a block: dust where it broke, blood where a creature was hit.</summary>
    public void AddBurst(ParticleKinds kind, Vector3 position, double now, double seconds)
    {
        var e = new ParticleEmitterInstance((int)MathF.Floor(position.X), (int)MathF.Floor(position.Y), (int)MathF.Floor(position.Z), (int)kind);
        _bursts.Add((now + seconds, e));
    }

    /// <summary>Upload the frame's emitters.</summary>
    public void Commit()
    {
        foreach (var b in _bursts) _emitters.Add(b.emitter);
        var span = System.Runtime.InteropServices.CollectionsMarshal.AsSpan(_emitters);
        EmitterCount = _pipe.SetEmitters(span);
    }

    public void Draw(GPURenderPassEncoder pass) { if (EmitterCount > 0) _pipe.Draw(pass); }

    // --- the sprites ---

    private static byte[] BuildAtlas()
    {
        var rgba = new byte[AtlasW * AtlasH * 4];
        void Paint(int[] rect, int frames, Func<int, float, float, Vector4> f)
        {
            for (int k = 0; k < frames; k++)
            for (int y = 0; y < rect[3]; y++)
            for (int x = 0; x < rect[2]; x++)
            {
                var c = f(k, (x + 0.5f) / rect[2], (y + 0.5f) / rect[3]);
                int i = ((rect[1] + y) * AtlasW + rect[0] + k * rect[2] + x) * 4;
                rgba[i] = (byte)Math.Clamp((int)(c.X * 255f + 0.5f), 0, 255);
                rgba[i + 1] = (byte)Math.Clamp((int)(c.Y * 255f + 0.5f), 0, 255);
                rgba[i + 2] = (byte)Math.Clamp((int)(c.Z * 255f + 0.5f), 0, 255);
                rgba[i + 3] = (byte)Math.Clamp((int)(c.W * 255f + 0.5f), 0, 255);
            }
        }

        // Flame: a teardrop, bright core, orange body, the tip flickering a little differently in each frame
        Paint(FlameRect, 4, (k, u, v) =>
        {
            float cx = u - 0.5f + (Value(v * 3f, k * 0.37f, 7, 7, 951) - 0.5f) * 0.20f * (1f - v);
            float width = 0.40f * MathF.Pow(MathF.Sin(MathF.PI * Math.Clamp(v * 0.95f + 0.04f, 0f, 1f)), 0.7f) * (0.35f + 0.65f * v);
            float d = MathF.Abs(cx) / MathF.Max(width, 0.001f);
            if (d > 1f || v < 0.02f) return new Vector4(0, 0, 0, 0);
            float core = 1f - d;
            var body = Vector3.Lerp(new Vector3(1f, 0.28f, 0.04f), new Vector3(1f, 0.72f, 0.18f), core);
            body = Vector3.Lerp(body, new Vector3(1f, 0.97f, 0.75f), MathF.Pow(core, 3f) * v);
            return new Vector4(body, SmoothStep(0f, 0.25f, 1f - d));
        });

        // Smoke: a soft grey puff made of overlapping lobes
        Paint(SmokeRect, 4, (k, u, v) =>
        {
            float dx = u - 0.5f, dy = v - 0.5f;
            float r = MathF.Sqrt(dx * dx + dy * dy) / 0.5f;
            float n = Fbm(u + k * 0.13f, v, 4, 4, 3, 961);
            float a = SmoothStep(1f, 0.15f, r) * (0.55f + 0.7f * n);
            float l = 0.62f + 0.35f * n;
            return new Vector4(l, l, l * 1.02f, Math.Clamp(a, 0f, 1f));
        });

        Paint(SparkRect, 1, (k, u, v) =>
        {
            float r = MathF.Sqrt((u - 0.5f) * (u - 0.5f) + (v - 0.5f) * (v - 0.5f)) / 0.5f;
            return new Vector4(1f, 0.9f, 0.6f, SmoothStep(1f, 0.2f, r));
        });

        // Dust: a clumpy brown puff
        Paint(DustRect, 4, (k, u, v) =>
        {
            float dx = u - 0.5f, dy = v - 0.5f;
            float r = MathF.Sqrt(dx * dx + dy * dy) / 0.5f;
            float n = Fbm(u + k * 0.21f, v, 5, 5, 3, 971);
            float a = SmoothStep(1f, 0.3f, r) * (0.5f + 0.8f * n);
            return new Vector4(0.85f, 0.85f, 0.85f, Math.Clamp(a, 0f, 1f));
        });

        Paint(BloodRect, 1, (k, u, v) =>
        {
            float r = MathF.Sqrt((u - 0.5f) * (u - 0.5f) + (v - 0.5f) * (v - 0.5f)) / 0.5f;
            return new Vector4(1f, 1f, 1f, r < 0.85f ? 1f : 0f);
        });

        // Firefly: a soft glowing dot
        Paint(GlowRect, 1, (k, u, v) =>
        {
            float r = MathF.Sqrt((u - 0.5f) * (u - 0.5f) + (v - 0.5f) * (v - 0.5f)) / 0.5f;
            return new Vector4(1f, 1f, 1f, MathF.Pow(Math.Clamp(1f - r, 0f, 1f), 1.6f));
        });
        return rgba;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _pipe.Dispose();
        _atlas?.Dispose();
    }
}
