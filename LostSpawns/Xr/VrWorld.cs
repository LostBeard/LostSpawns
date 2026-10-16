using System.Numerics;
using ILGPU.Runtime;
using LostSpawns.Rendering;
using LostSpawns.Services;
using SpawnDev.ILGPU.WebGPU;
using SpawnDev.SpawnJS;
using SpawnDev.SpawnJS.JSObjects;
using SpawnDev.VoxelEngine.Rendering;

namespace LostSpawns.Xr;

/// <summary>
/// The island seen from a headset, on the game's own page: entering VR pauses the desktop view and this takes over the same world, and the
/// player stands where the desktop camera was. WebXR hands us each frame's eyes (<see cref="XrFrameData"/>); for each eye the world is drawn by WebGPU
/// (sky, terrain, creatures, particles, water, rain: the same renderers as the desktop game) into an offscreen canvas and copied into that eye's
/// part of the headset's WebGL layer (<see cref="WebGlXrBlit"/>). The room is carried over the ground: the left stick walks along where the head
/// faces, the right stick turns smoothly about the head (a click of it switches to snap turns), and the room's floor follows the terrain.
/// </summary>
public sealed class VrWorld : IDisposable
{
    /// <summary>Columns round the player that are kept (a headset's GPU is a phone's).</summary>
    public const int DrawRadius = 7;
    /// <summary>Metres a second at full stick; a pressed-in left stick or a grip runs.</summary>
    public const float WalkSpeed = 3.5f, RunSpeed = 7f;
    private const float FillLightStrength = 0.18f;
    /// <summary>OpenGL clip space (z -1..1) to standard (z 0..1), row-vector form: what follows a WebXR projection.</summary>
    private static readonly Matrix4x4 GlToStandardZ = new(1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0.5f, 0, 0, 0, 0.5f, 1);

    private readonly SpawnJSRuntime _js;
    private readonly VoxelEngineService _engine;
    private readonly WorldService _world;
    private readonly WorldTimeService _time;
    private readonly WeatherService _weather;
    private readonly EntityService _wildlife;
    private readonly XrLocomotion _loco = new();

    private GPUDevice? _device;
    private GPUQueue? _queue;
    private string _format = "bgra8unorm";
    private TerrainRenderer? _terrain;
    private SkyRenderer? _sky;
    private CreatureRenderer? _creatures;
    private ParticleRenderer? _particles;
    private WeatherRenderer? _rain;
    private OffscreenCanvas? _bridge;
    private GPUCanvasContext? _bridgeContext;
    private GPUTexture? _depth;
    private GPUTextureView? _depthView;
    private int _eyeWidth, _eyeHeight;
    private Matrix4x4? _worldFromRoom;
    private Vector3 _spawn;
    private float _startYaw;
    private Quaternion _headOrientation = Quaternion.Identity;
    private double _lastTime = -1;
    private bool _thumbWasDown;
    private float _groundY;
    private bool _groundKnown;
    private bool _disposed;

    /// <summary>The world has columns and the renderers exist: frames draw.</summary>
    public bool Ready { get; private set; }
    public float TurnDegreesPerSecond { get => _loco.SmoothTurnDegreesPerSecond; set => _loco.SmoothTurnDegreesPerSecond = value; }
    public bool SmoothTurn => _loco.SmoothTurn;
    public int FramesDrawn { get; private set; }
    public Vector3 HeadInWorld { get; private set; }
    public int LastVisibleColumns { get; private set; }

    /// <summary>Where the head is, and which way it looks (degrees, 0 = +X, as the desktop camera counts), as of the last frame: the desktop view takes over from there.</summary>
    public (Vector3 Head, float YawDegrees, float PitchDegrees) Pose
    {
        get
        {
            if (_worldFromRoom is not { } m) return (_spawn, _startYaw * 180f / MathF.PI, 0f);
            var gaze = Vector3.TransformNormal(Vector3.Transform(-Vector3.UnitZ, _headOrientation), m);
            var (yaw, pitch) = XrMath.AnglesOf(gaze);
            return (HeadInWorld, yaw, pitch);
        }
    }

    public VrWorld(SpawnJSRuntime js, VoxelEngineService engine, WorldService world, WorldTimeService time, WeatherService weather, EntityService wildlife)
    {
        _js = js;
        _engine = engine;
        _world = world;
        _time = time;
        _weather = weather;
        _wildlife = wildlife;
    }

    /// <summary>Build the renderers (once) and put the player at <paramref name="cameraPosition"/>, facing <paramref name="yawDegrees"/>. The game's world is already running.</summary>
    public void Start(Vector3 cameraPosition, float yawDegrees)
    {
        if (_engine.Accelerator is not WebGPUAccelerator webGpu)
            throw new InvalidOperationException("The headset view needs the WebGPU backend");
        _device = webGpu.NativeAccelerator.NativeDevice ?? throw new InvalidOperationException("WebGPU device is null");
        _queue = webGpu.NativeAccelerator.Queue ?? throw new InvalidOperationException("WebGPU queue is null");

        if (_terrain == null)
        {
            using (var navigator = _js.Get<Navigator>("navigator"))
            using (var gpu = navigator.Gpu)
                if (gpu != null) _format = gpu.GetPreferredCanvasFormat();

            _terrain = new TerrainRenderer();
            _terrain.Init(_device, _queue, _format);
            _sky = new SkyRenderer();
            _sky.Init(_device, _queue, _format);
            _rain = new WeatherRenderer();
            _rain.Init(_device, _queue, _format, _sky.Atlas!);
            _creatures = new CreatureRenderer();
            _creatures.Init(_device, _queue, _format, _terrain.FrameBuffer);
            _particles = new ParticleRenderer();
            _particles.Init(_device, _queue, _format, _terrain.FrameBuffer);
        }

        _spawn = cameraPosition;
        _startYaw = yawDegrees * MathF.PI / 180f;
        _worldFromRoom = null;
        _lastTime = -1;
        _groundKnown = false;
        _thumbWasDown = false;
        _loco.Reset();
        FramesDrawn = 0;
        Ready = true;
        Console.WriteLine($"[Vr] player at {_spawn:F1}, {_world.LoadedColumns} columns loaded");
    }

    /// <summary>One frame of the session: move the room, keep the world around it, draw each eye and copy it into the headset's layer.</summary>
    public void OnFrame(XrFrameData f, XrSession session, WebGlXrBlit blit)
    {
        if (!Ready || _disposed || f.Views.Length == 0 || session.Layer == null) return;

        _worldFromRoom ??= XrMath.WorldFromRoom(f.HeadPosition, f.HeadOrientation, _spawn, new Vector3(MathF.Cos(_startYaw), 0f, MathF.Sin(_startYaw)));
        _headOrientation = f.HeadOrientation;
        float dt = _lastTime < 0 ? 0f : (float)Math.Clamp((f.Time - _lastTime) / 1000.0, 0.0, 0.1);
        _lastTime = f.Time;

        // A click of the right stick switches between smooth and snap turning (smooth is the default)
        if (f.RightThumb && !_thumbWasDown)
        {
            _loco.SmoothTurn = !_loco.SmoothTurn;
            Console.WriteLine($"[Vr] turning {(_loco.SmoothTurn ? "smoothly" : "in steps")}");
        }
        _thumbWasDown = f.RightThumb;

        // The sticks: left walks (a click or a grip runs), right turns
        float speed = f.LeftThumb || f.LeftGrip || f.RightGrip ? RunSpeed : WalkSpeed;
        var m = _loco.Step(_worldFromRoom.Value, f.HeadPosition, f.HeadOrientation, f.LeftStick, f.RightStick, dt, speed);

        // The floor of the room follows the ground under the head
        var head = Vector3.Transform(f.HeadPosition, m);
        float ground = _world.GetHeightAt(head.X, head.Z) + 1f;
        if (!_groundKnown) { _groundY = ground; _groundKnown = true; }
        else _groundY += (ground - _groundY) * (1f - MathF.Exp(-10f * dt));
        m.M42 = _groundY;
        _worldFromRoom = m;
        head = Vector3.Transform(f.HeadPosition, m);
        HeadInWorld = head;

        // The world's own life
        _time.Tick(dt);
        _weather.Tick(dt);
        _wildlife.Tick(dt, _world, head, _time.IsNight);
        _world.ProcessReadyChunks();
        _world.UpdateDesiredChunks(head, DrawRadius);
        if (_world.LoadedColumns == 0) return;

        double seconds = f.Time / 1000.0;
        float rain = _weather.RainIntensity;
        var sky = Atmosphere.At(_time.DayFraction, rain, 0f);
        float fogEnd = MathF.Max(DrawRadius * 16f - 12f, 48f) * (1f - 0.35f * rain);

        _creatures!.Begin();
        _creatures.AddCreatures(_wildlife.Entities, head, seconds, 100f);
        _creatures.Commit();
        _particles!.Begin(seconds);
        _particles.AddFireflies(_world, head, sky.Night, rain);
        _particles.Commit();

        var first = f.Views[0].Viewport;
        EnsureTargets((int)first.Width, (int)first.Height);

        foreach (var view in f.Views)
        {
            DrawEye(view, m, in sky, rain, fogEnd, seconds);
            blit.Blit(_bridge!, session.Layer.Framebuffer!, view.Viewport);
        }
        FramesDrawn++;
        if (FramesDrawn % 240 == 1) Console.WriteLine($"[Vr] frame {FramesDrawn}: head {head:F1}, ground {_groundY:F1}, {LastVisibleColumns} columns in view, {_world.LoadedColumns} loaded");
    }

    // The bridge canvas and its depth buffer are one eye big
    private void EnsureTargets(int width, int height)
    {
        if (_bridge != null && width == _eyeWidth && height == _eyeHeight) return;
        _depthView?.Dispose();
        _depth?.Destroy(); _depth?.Dispose();
        _bridgeContext?.Dispose();
        _bridge?.Dispose();

        _eyeWidth = width;
        _eyeHeight = height;
        _bridge = new OffscreenCanvas(width, height);
        _bridgeContext = _bridge.GetWebGPUContext() ?? throw new InvalidOperationException("OffscreenCanvas.getContext('webgpu') returned null");
        _bridgeContext.Configure(new GPUCanvasConfiguration { Device = _device!, Format = _format, AlphaMode = "opaque" });
        _depth = _device!.CreateTexture(new GPUTextureDescriptor
        {
            Size = new[] { width, height },
            Format = "depth32float",
            Usage = GPUTextureUsage.RenderAttachment,
        });
        _depthView = _depth.CreateView();
        Console.WriteLine($"[Vr] eye target {width} x {height}");
    }

    private void DrawEye(XrViewData view, Matrix4x4 worldFromRoom, in Atmosphere sky, float rain, float fogEnd, double seconds)
    {
        var worldToEye = XrMath.EyeView(view.RoomView, worldFromRoom);
        var eyeInWorld = XrMath.EyeInWorld(view.RoomView, worldFromRoom);
        var vp = worldToEye * view.Projection;
        var vpReversed = vp * VrMatrixHelpers.ZFlipStandardToReversedZ;
        var frustum = FrustumCuller.ExtractPlanes(vp * GlToStandardZ);

        // The frame's uniforms are written before this eye's submit: the queue orders a write with the submits round it
        _terrain!.BeginFrame(new BlockFrameUniforms
        {
            ViewProjection = vpReversed,
            CameraPosition = new Vector4(eyeInWorld, 1f),
            SunDirection = new Vector4(sky.SunDirection, 0f),
            SunColor = new Vector4(sky.SunColor, sky.SunStrength),
            Ambient = new Vector4(sky.Ambient, FillLightStrength),
            FogColor = new Vector4(sky.Fog, 1f),
            FogRange = new Vector4(fogEnd * 0.42f, fogEnd, 0f, 0f),
            Time = (float)seconds,
        });
        LastVisibleColumns = _terrain.PrepareVisible(_world, in frustum, eyeInWorld, fogEnd + 24f);

        using var encoder = _device!.CreateCommandEncoder();
        using var colorTexture = _bridgeContext!.GetCurrentTexture();
        using var colorView = colorTexture.CreateView();
        using var pass = _terrain.BeginRenderPass(encoder, colorView, _depthView!, sky.Fog);
        _sky!.Draw(pass, in vpReversed, eyeInWorld, in sky, rain, _time.DayNumber % 8, _time.DayFraction);
        _terrain.DrawSolid(pass);
        _creatures!.Draw(pass);
        _particles!.Draw(pass);
        _terrain.DrawWater(pass);
        _rain!.Draw(pass, _world, in vpReversed, eyeInWorld, rain, sky.Daylight);
        pass.End();
        using var commands = encoder.Finish();
        _queue!.Submit(new[] { commands });
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Ready = false;
        _depthView?.Dispose();
        _depth?.Destroy(); _depth?.Dispose();
        _bridgeContext?.Dispose();
        _bridge?.Dispose();
        _rain?.Dispose();
        _particles?.Dispose();
        _creatures?.Dispose();
        _sky?.Dispose();
        _terrain?.Dispose();
    }
}
