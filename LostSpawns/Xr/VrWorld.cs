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
    /// <summary>Columns round the player that are kept by default (a headset's GPU is a phone's); <c>?vrdd=N</c> changes it for tuning.</summary>
    public const int DefaultDrawRadius = 7;
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
    private readonly CampfireService _fires;
    private readonly GroundItemService _ground;
    private readonly BedrollService _bedroll;
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
    // Where the time of the frames goes, reported every few seconds (what to look at when tuning for a headset)
    private readonly System.Diagnostics.Stopwatch _frameClock = new();
    private double _frameMsSum, _frameMsMax, _eyeMsSum, _gapMsSum;
    private int _framesInReport;
    private double _lastFrameStart = -1;

    /// <summary>How many columns out from the player the world is kept and drawn.</summary>
    public int DrawRadius { get; set; } = DefaultDrawRadius;

    /// <summary>
    /// A headset that cannot keep its refresh rate gets a smaller ring of columns (one fewer per report, down to <see cref="MinDrawRadius"/>), and a run of clean
    /// reports gives one back (up to the radius it started with). Off when the radius was chosen by hand (<c>?vrdd</c>).
    /// </summary>
    public bool Adaptive { get; set; } = true;

    /// <summary>How much of the headset's eye resolution the world is drawn at (the picture is stretched to the eye when copied): the GPU of a headset is the limit, and pixels are what it has most of.</summary>
    public float RenderScale { get; set; } = 0.75f;

    /// <summary>How far the forearm is tilted up from the way the controller points (degrees): the elbow end is higher than the hand.</summary>
    public float ForearmTiltDegrees { get; set; } = 35f;
    public const int MinDrawRadius = 3;
    private int _startRadius = DefaultDrawRadius;
    private double _bestGap = double.MaxValue;
    private int _cleanReports, _cleanNeeded = 4;

    /// <summary>The panels (status, menu) drawn over each eye, and the GameUI renderer that draws them; both set by the game page.</summary>
    public VrUi? Ui { get; set; }
    public Func<SpawnDev.GameUI.UIRenderer?>? UiRenderer { get; set; }
    /// <summary>What the right hand holds and how it swings, asked each frame.</summary>
    public Func<HeldView>? HeldProvider { get; set; }
    /// <summary>The right hand in the world: where it is and which way the controller points (null: not tracked this frame).</summary>
    public (Vector3 Position, Vector3 Direction)? RightHand { get; private set; }
    /// <summary>Room to world, as of the last frame (null before the first).</summary>
    public Matrix4x4? WorldFromRoomNow => _worldFromRoom;
    private double _lastSeconds;

    /// <summary>The world has columns and the renderers exist: frames draw.</summary>
    public bool Ready { get; private set; }
    public float TurnDegreesPerSecond { get => _loco.SmoothTurnDegreesPerSecond; set => _loco.SmoothTurnDegreesPerSecond = value; }
    public bool SmoothTurn { get => _loco.SmoothTurn; set => _loco.SmoothTurn = value; }
    public float SnapTurnDegrees { get => _loco.SnapTurnDegrees; set => _loco.SnapTurnDegrees = value; }
    /// <summary>The right stick was clicked: the turning switched between smooth and snap (the new state).</summary>
    public event Action<bool>? OnTurnModeToggled;
    /// <summary>How hard the sticks are pushed now (0..1): walking or turning, for the comfort vignette.</summary>
    public float StickEffort { get; private set; }
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

    public VrWorld(SpawnJSRuntime js, VoxelEngineService engine, WorldService world, WorldTimeService time, WeatherService weather, EntityService wildlife, CampfireService fires, GroundItemService ground, BedrollService bedroll)
    {
        _bedroll = bedroll;
        _fires = fires;
        _ground = ground;
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
            _terrain.Init(_device, _queue, _format, _world.Light);
            _sky = new SkyRenderer();
            _sky.Init(_device, _queue, _format);
            _rain = new WeatherRenderer();
            _rain.Init(_device, _queue, _format, _sky.Atlas!);
            _creatures = new CreatureRenderer();
            _creatures.Init(_device, _queue, _format, _terrain.FrameBuffer);
            _particles = new ParticleRenderer();
            _particles.Init(_device, _queue, _format, _terrain.FrameBuffer);
        }

        _startRadius = DrawRadius;
        _bestGap = double.MaxValue;
        _cleanReports = 0;
        _cleanNeeded = 4;
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
        _frameClock.Restart();
        // Time between the starts of two frames: 11.1 ms is a 90 Hz headset keeping up
        if (_lastFrameStart >= 0) _gapMsSum += f.Time - _lastFrameStart;
        _lastFrameStart = f.Time;

        _worldFromRoom ??= XrMath.WorldFromRoom(f.HeadPosition, f.HeadOrientation, _spawn, new Vector3(MathF.Cos(_startYaw), 0f, MathF.Sin(_startYaw)));
        _headOrientation = f.HeadOrientation;
        float dt = _lastTime < 0 ? 0f : (float)Math.Clamp((f.Time - _lastTime) / 1000.0, 0.0, 0.1);
        _lastTime = f.Time;

        // (The turning mode is changed in the menu's Settings only: a click of the stick is too easy to make by accident while turning, and it kept switching to steps.)

        // The sticks: left walks (a click or a grip runs), right turns
        float speed = f.LeftThumb || f.LeftGrip ? RunSpeed : WalkSpeed;
        StickEffort = MathF.Min(1f, MathF.Max(XrLocomotion.ApplyDeadzone(f.LeftStick).Length(), MathF.Abs(XrLocomotion.ApplyDeadzone(f.RightStick).X)));
        var m = _loco.Step(_worldFromRoom.Value, f.HeadPosition, f.HeadOrientation, f.LeftStick, f.RightStick, dt, speed);

        // Walls and trunks stop the stick-driven walk (a step of one block does not): the move is cut to what is free, sliding along a wall
        if (_groundKnown)
        {
            var before = Vector3.Transform(f.HeadPosition, _worldFromRoom.Value);
            var after = Vector3.Transform(f.HeadPosition, m);
            if (WalkBlocked(after.X, after.Z, _groundY))
            {
                if (!WalkBlocked(after.X, before.Z, _groundY)) m *= Matrix4x4.CreateTranslation(0f, 0f, before.Z - after.Z);
                else if (!WalkBlocked(before.X, after.Z, _groundY)) m *= Matrix4x4.CreateTranslation(before.X - after.X, 0f, 0f);
                else m *= Matrix4x4.CreateTranslation(before.X - after.X, 0f, before.Z - after.Z);
            }
        }

        // The floor of the room follows what is under the head: a floor, a step, a table top, the ground
        var head = Vector3.Transform(f.HeadPosition, m);
        float ground = FloorAt(head.X, head.Z, _groundKnown ? _groundY : _world.GetHeightAt(head.X, head.Z) + 1f);
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

        _lastSeconds = seconds;
        RightHand = f.RightHasRay ? (Vector3.Transform(f.RightRayOrigin, m), Vector3.Normalize(Vector3.TransformNormal(f.RightRayDirection, m))) : null;

        _creatures!.Begin();
        _creatures.AddCreatures(_wildlife.Entities, head, seconds, 100f);
        _creatures.AddFires(_fires.Fires, head, 100f);
        _creatures.AddItems(_ground.Items, head, seconds, 40f);
        _creatures.AddBedroll(_bedroll.Position, head, 100f);
        // The arms: rigid with the controllers (the grip pose), so a hand and its elbow move as one; the right holds the tool, the left carries the wrist device
        var held0 = HeldProvider?.Invoke() ?? default;
        if (f.TryGetController(false, out var rPos, out var rQ)) _creatures.AddArmAt(ControllerToWorld(rPos, rQ, m), false, in held0, ForearmTiltDegrees);
        if (f.TryGetController(true, out var lPos, out var lQ)) _creatures.AddArmAt(ControllerToWorld(lPos, lQ, m), true, in held0, ForearmTiltDegrees);
        _creatures.Commit();
        _particles!.Begin(seconds);
        _particles.AddFires(_fires.Fires, head, 100f, sky.Daylight);
        _particles.AddFireflies(_world, head, sky.Night, rain);
        _particles.Commit();

        var first = f.Views[0].Viewport;
        EnsureTargets(Math.Max(64, (int)(first.Width * RenderScale)), Math.Max(64, (int)(first.Height * RenderScale)));

        double eyeStart = _frameClock.Elapsed.TotalMilliseconds;
        foreach (var view in f.Views)
        {
            DrawEye(view, m, in sky, rain, fogEnd, seconds);
            blit.Blit(_bridge!, session.Layer.Framebuffer!, view.Viewport);
        }
        FramesDrawn++;

        // CPU time of this frame (what recording the draws and the copies cost on the page's thread; the GPU runs on its own clock)
        double ms = _frameClock.Elapsed.TotalMilliseconds;
        _frameMsSum += ms;
        _frameMsMax = Math.Max(_frameMsMax, ms);
        _eyeMsSum += ms - eyeStart;
        if (++_framesInReport >= 240)
        {
            var st = _terrain.Stats;
            Console.WriteLine($"[Vr] frame {FramesDrawn}: head {head:F1}, ground {_groundY:F1} | every {_gapMsSum / _framesInReport:F1} ms, cpu {_frameMsSum / _framesInReport:F1} ms "
                + $"(max {_frameMsMax:F1}; the two eyes {_eyeMsSum / _framesInReport:F1}) | per eye {st.VisibleColumns} columns, {st.DrawCalls} draws, {st.VisibleQuads} quads, "
                + $"{_creatures.PartCount} creature parts, {_particles.EmitterCount} emitters | {_world.LoadedColumns} columns loaded, eye {_eyeWidth} x {_eyeHeight}");
            Adapt(_gapMsSum / _framesInReport);
            _frameMsSum = _frameMsMax = _eyeMsSum = _gapMsSum = 0;
            _framesInReport = 0;
        }
    }

    /// <summary>The top of what stands under (x, z), up to a one block step above <paramref name="reference"/> and a few blocks below it; the terrain where nothing is loaded.</summary>
    private float FloorAt(float x, float z, float reference)
    {
        int bx = (int)MathF.Floor(x), bz = (int)MathF.Floor(z);
        int top = (int)MathF.Floor(reference + 1.1f);
        for (int y = top; y >= (int)MathF.Floor(reference) - 5; y--)
            if (_world.IsSolidAt(bx, y - 1, bz) && !_world.IsSolidAt(bx, y, bz)) return y;
        return _world.GetHeightAt(x, z) + 1f;
    }

    /// <summary>Something solid, or deep water, at waist or head height round the player's centre (a body 0.5 m across), with the feet on <paramref name="floor"/>.</summary>
    private bool WalkBlocked(float x, float z, float floor)
    {
        const float r = 0.25f;
        int waist = (int)MathF.Floor(floor + 1.2f), head = (int)MathF.Floor(floor + 1.75f);
        for (int i = 0; i < 4; i++)
        {
            int bx = (int)MathF.Floor(x + ((i & 1) == 0 ? -r : r)), bz = (int)MathF.Floor(z + ((i & 2) == 0 ? -r : r));
            if (_world.IsSolidAt(bx, waist, bz) || _world.IsSolidAt(bx, head, bz)) return true;
            // Water over the waist is too deep to wade (the player does not swim in the headset)
            if (_world.GetBlockAt(bx, waist, bz) == LostSpawns.Models.BlockType.Water) return true;
        }
        return false;
    }

    /// <summary>A controller's pose in the room (where it is held, how it is turned) as a local-to-world matrix, through the room's own transform.</summary>
    public static Matrix4x4 ControllerToWorld(Vector3 positionRoom, Quaternion orientationRoom, Matrix4x4 worldFromRoom)
    {
        var turn = worldFromRoom;
        turn.M41 = turn.M42 = turn.M43 = 0f;
        return Matrix4x4.CreateFromQuaternion(orientationRoom) * turn * Matrix4x4.CreateTranslation(Vector3.Transform(positionRoom, worldFromRoom));
    }

    // The gap between frames, against the best gap seen (the display's own period): well over it means frames are being dropped
    private void Adapt(double gap)
    {
        _bestGap = Math.Min(_bestGap, gap);
        if (!Adaptive) return;
        if (gap > _bestGap * 1.3 && gap > 12.0 && DrawRadius > MinDrawRadius)
        {
            DrawRadius--;
            _cleanReports = 0;
            _cleanNeeded = Math.Min(_cleanNeeded * 2, 32);   // having had to back off, wait longer before trying again
            Console.WriteLine($"[Vr] frames every {gap:F1} ms against {_bestGap:F1}: draw radius {DrawRadius}");
        }
        else if (gap <= _bestGap * 1.1)
        {
            if (++_cleanReports >= _cleanNeeded && DrawRadius < _startRadius)
            {
                DrawRadius++;
                _cleanReports = 0;
                Console.WriteLine($"[Vr] frames on time: draw radius {DrawRadius}");
            }
        }
        else _cleanReports = 0;
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

        // The panels over the world: the eye's view from the room (they are placed in the room, in metres)
        if (Ui != null && UiRenderer?.Invoke() is { } uiRenderer)
        {
            Ui.EnsureTargets(_device, _queue, _eyeWidth, _eyeHeight);
            Ui.DrawEye(_device, _queue, uiRenderer, view.RoomView * view.Projection, colorView);
        }
    }

    /// <summary>A short burst of particles (dust where a block broke, blood where a creature was hit) at a world position.</summary>
    public void Burst(ParticleKinds kind, Vector3 position, double seconds) => _particles?.AddBurst(kind, position, _lastSeconds, seconds);

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
