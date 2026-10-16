using SpawnDev.SpawnJS;
using SpawnDev.SpawnJS.JSObjects;
using SpawnDev.ILGPU.WebGPU;
using ILGPU.Runtime;
using System.Numerics;
using LostSpawns.Models;
using LostSpawns.Rendering;
using SpawnDev.VoxelEngine.Rendering;

namespace LostSpawns.Services;

/// <summary>
/// WebGPU render service: the sky, the textured and lit terrain (VoxelEngine's block pipeline), the creatures, then the HUD.
/// Each column's quads go straight from the GPU mesher to the render pass, one draw a list.
/// </summary>
public class RenderService : IDisposable
{
    private readonly SpawnJSRuntime _js;
    private readonly WorldTimeService _worldTime;
    private readonly WeatherService _weather;
    private readonly SettingsService _settings;
    private readonly EntityService _entities;
    private readonly CampfireService _fires;
    private readonly GroundItemService _ground;

    private GPUDevice? _device;
    private GPUQueue? _queue;
    private GPUCanvasContext? _context;
    private string _canvasFormat = "bgra8unorm";

    private PostProcessor? _post;
    private string? _canvasId;
    private int _canvasWidth;
    private int _canvasHeight;

    // The block pipeline draws the world's columns; the sky pipeline draws behind it; the creatures (boxes with painted fur) share the pass
    private TerrainRenderer? _terrain;
    private SkyRenderer? _sky;
    private WeatherRenderer? _weatherView;
    private CreatureRenderer? _creatures;
    private ParticleRenderer? _particles;
    private float _animTime;

    // World reference (set during init)
    private WorldService? _world;

    private bool _running;
    private bool _disposed;
    private double _lastTimestamp;
    private ActionCallback<double>? _rafCallback;

    public Camera Camera { get; } = new();

    /// <summary>Forces the ambient occlusion on or off whatever the setting says (a developer view).</summary>
    public bool? OcclusionOverride { get; set; }

    /// <summary>What the player holds and how it moves, asked for each frame (the game supplies it).</summary>
    public Func<HeldView>? HeldProvider { get; set; }

    /// <summary>A short burst of particles (dust where a block broke, blood where a creature was hit) at a world position.</summary>
    public void ParticleBurst(ParticleKinds kind, Vector3 position, double seconds) => _particles?.AddBurst(kind, position, _animTime, seconds);
    public bool IsInitialized { get; private set; }
    public Action<float>? OnUpdate { get; set; }

    /// <summary>
    /// Called after the voxel render pass ends, before encoder.Finish().
    /// GameUI hooks into this to render its overlay pass on the same encoder.
    /// Parameters: (encoder, colorView, canvasWidth, canvasHeight)
    /// </summary>
    public Action<GPUCommandEncoder, GPUTextureView, int, int>? OnPostRender { get; set; }

    public GPUDevice? Device => _device;
    public GPUQueue? Queue => _queue;
    public string CanvasFormat => _canvasFormat;
    public int CanvasWidth => _canvasWidth;
    public int CanvasHeight => _canvasHeight;

    public int VisibleChunkCount { get; private set; }
    public int TotalSectionCount => _world?.LoadedSections ?? 0;
    public int TotalColumnCount => _world?.LoadedColumns ?? 0;

    public RenderService(SpawnJSRuntime js, WorldTimeService worldTime, WeatherService weather, SettingsService settings, EntityService entities, CampfireService fires, GroundItemService ground)
    {
        _js = js;
        _worldTime = worldTime;
        _weather = weather;
        _settings = settings;
        _entities = entities;
        _fires = fires;
        _ground = ground;
    }

    public void Init(HTMLCanvasElement canvas, Accelerator accelerator, WorldService world)
    {
        if (IsInitialized) return;
        _world = world;

        if (accelerator is not WebGPUAccelerator webGpuAccel)
            throw new InvalidOperationException("RenderService requires a WebGPU accelerator");

        var nativeAccel = webGpuAccel.NativeAccelerator;
        _device = nativeAccel.NativeDevice
            ?? throw new InvalidOperationException("WebGPU native device is null");
        _queue = nativeAccel.Queue
            ?? throw new InvalidOperationException("WebGPU queue is null");

        _context = canvas.GetContext<GPUCanvasContext>("webgpu")
            ?? throw new InvalidOperationException("Failed to get WebGPU canvas context");

        using var navigator = _js.Get<Navigator>("navigator");
        using var gpu = navigator.Gpu;
        if (gpu is not null)
            _canvasFormat = gpu.GetPreferredCanvasFormat();

        _context.Configure(new GPUCanvasConfiguration
        {
            Device = _device,
            Format = _canvasFormat,
        });

        _canvasId = canvas.Id;
        _canvasWidth = canvas.ClientWidth;
        _canvasHeight = canvas.ClientHeight;
        canvas.Width = _canvasWidth;
        canvas.Height = _canvasHeight;

        _terrain = new TerrainRenderer();
        _terrain.Init(_device, _queue, _canvasFormat);
        _sky = new SkyRenderer();
        _sky.Init(_device, _queue, _canvasFormat);
        _weatherView = new WeatherRenderer();
        _weatherView.Init(_device, _queue, _canvasFormat, _sky.Atlas!);

        // The creatures read the terrain's frame uniforms, so they are lit and fogged like the ground
        _creatures = new CreatureRenderer();
        _creatures.Init(_device, _queue, _canvasFormat, _terrain.FrameBuffer);
        _particles = new ParticleRenderer();
        _particles.Init(_device, _queue, _canvasFormat, _terrain.FrameBuffer);

        // The world is drawn into the post-processor's colour and depth textures; it finishes the picture onto the canvas
        _post = new PostProcessor();
        _post.Init(_device, _queue, _canvasFormat);
        _post.Resize(_canvasWidth, _canvasHeight);

        IsInitialized = true;
        Console.WriteLine($"[Render] Pipeline ready ({_canvasWidth}x{_canvasHeight})");
    }

    public void StartRenderLoop()
    {
        if (_running) return;
        _running = true;
        _lastTimestamp = 0;
        _rafCallback ??= new ActionCallback<double>(OnAnimationFrame);
        RequestFrame();
    }

    public void StopRenderLoop() => _running = false;

    private void RequestFrame()
    {
        if (!_running || _disposed || _rafCallback == null) return;
        using var window = _js.Get<Window>("window");
        window.RequestAnimationFrame(_rafCallback);
    }

    private void OnAnimationFrame(double timestamp)
    {
        if (!_running || _disposed) return;
        float dt = _lastTimestamp > 0 ? (float)((timestamp - _lastTimestamp) / 1000.0) : 1f / 60f;
        _lastTimestamp = timestamp;
        dt = Math.Min(dt, 0.1f);
        OnUpdate?.Invoke(dt);
        _animTime += dt;
        RenderFrame();
        RequestFrame();
    }

    private void RenderFrame()
    {
        if (_device == null || _context == null || _terrain == null || _sky == null || _world == null)
            return;

        // Dynamic resize: match canvas pixel resolution to its CSS display size
        if (_canvasId != null)
        {
            using var doc = _js.Get<Document>("document");
            using var canvasEl = doc.GetElementById<HTMLCanvasElement>(_canvasId);
            if (canvasEl != null)
            {
                int cw = canvasEl.ClientWidth;
                int ch = canvasEl.ClientHeight;
                if (cw > 0 && ch > 0 && (cw != _canvasWidth || ch != _canvasHeight))
                {
                    _canvasWidth = cw;
                    _canvasHeight = ch;
                    canvasEl.Width = cw;
                    canvasEl.Height = ch;
                    _post!.Resize(cw, ch);
                }
            }
        }

        // The settings' field of view and occlusion switch
        Camera.FieldOfView = _settings.FieldOfView;
        _post!.OcclusionEnabled = OcclusionOverride ?? _settings.AmbientOcclusion;

        float aspect = (float)_canvasWidth / _canvasHeight;
        var vp = Camera.GetVpMatrix(aspect);
        // Raw Matrix4x4 - WGSL column-major reinterpretation handles the transpose
        var frustum = FrustumCuller.ExtractPlanes(vp);
        var cameraPos = Camera.Position;

        // The light of this moment: the sun, ambient, fog and sky, from the clock and the weather
        var sky = Atmosphere.At(_worldTime.DayFraction, _weather.RainIntensity, 0f);
        float rain = _weather.RainIntensity;
        // Nothing past the fog is drawn: terrain there is the fog colour, and costs only draws
        float fogEnd = MathF.Max(_settings.DrawDistance * 16f - 12f, 48f) * (1f - 0.35f * rain);
        float fogStart = fogEnd * 0.42f;

        _terrain.BeginFrame(new BlockFrameUniforms
        {
            ViewProjection = vp,
            CameraPosition = new Vector4(cameraPos, 1f),
            SunDirection = new Vector4(sky.SunDirection, 0f),
            SunColor = new Vector4(sky.SunColor, sky.SunStrength),
            Ambient = new Vector4(sky.Ambient, FillLightStrength),
            FogColor = new Vector4(sky.Fog, 1f),
            FogRange = new Vector4(fogStart, fogEnd, 0f, 0f),
            Time = _animTime,
        });

        using var colorTexture = _context.GetCurrentTexture();
        using var colorView = colorTexture.CreateView();

        VisibleChunkCount = _terrain.PrepareVisible(_world, in frustum, cameraPos, fogEnd + 24f);

        // Everything made of boxes for this moment: the wildlife, the fires, the loot, the tool in the hand (uploaded before the pass)
        float modelRange = MathF.Min(fogEnd, 120f);
        _creatures!.Begin();
        _creatures.AddCreatures(_entities.Entities, cameraPos, _animTime, modelRange);
        _creatures.AddFires(_fires.Fires, cameraPos, modelRange);
        _creatures.AddItems(_ground.Items, cameraPos, _animTime, 60f);
        _creatures.AddHeld(HeldProvider?.Invoke() ?? default, Camera);
        _creatures.Commit();

        // Flames, smoke, sparks, fireflies, dust, blood
        _particles!.Begin(_animTime);
        _particles.AddFires(_fires.Fires, cameraPos, modelRange, sky.Daylight);
        _particles.AddFireflies(_world, cameraPos, sky.Night, rain);
        _particles.Commit();

        // Single encoder, single render pass, single submit
        using var encoder = _device.CreateCommandEncoder();
        // Cleared to the fog colour: what lies past the fog (not drawn) looks exactly like fogged terrain
        // With occlusion on the world goes to an offscreen picture that the post-processor finishes onto the canvas; off, straight to the canvas
        var worldTarget = _post.OcclusionEnabled ? _post.SceneColorView! : colorView;
        using var pass = _terrain.BeginRenderPass(encoder, worldTarget, _post.DepthView!, sky.Fog);

        // The sky first, under everything
        _sky.Draw(pass, in vp, cameraPos, in sky, rain, MoonPhase(), _worldTime.DayFraction);

        _terrain.DrawSolid(pass);

        // The creatures, fires, loot and held tool, then the particles: depth tested against the ground, before the water so water in front of one blends over it
        _creatures.Draw(pass);
        _particles.Draw(pass);

        // Water last so it blends over the ground and creatures behind it
        _terrain.DrawWater(pass);

        // The rain over everything
        _weatherView!.Draw(pass, _world, in vp, cameraPos, rain, sky.Daylight);

        pass.End();
        if (_post.OcclusionEnabled)
            _post.Run(encoder, colorView, Camera.FieldOfView * MathF.PI / 180f, 0.1f);
        using var cmdBuf = encoder.Finish();
        _queue!.Submit(new[] { cmdBuf });

        // GameUI overlay pass (separate encoder, LoadOp.Load preserves terrain)
        using var uiEncoder = _device.CreateCommandEncoder();
        OnPostRender?.Invoke(uiEncoder, colorView, _canvasWidth, _canvasHeight);
        using var uiCmd = uiEncoder.Finish();
        _queue!.Submit(new[] { uiCmd });
    }

    /// <summary>The cool fill light the block shader adds from the shaded side, so faces turned from the sun are not black.</summary>
    private const float FillLightStrength = 0.18f;

    // One phase of the moon (0 full .. 7) a few nights at a time
    private int MoonPhase() => _worldTime.DayNumber % 8;

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _running = false;

        _rafCallback?.Dispose();
        _rafCallback = null;

        _particles?.Dispose();
        _particles = null;
        _creatures?.Dispose();
        _creatures = null;
        _weatherView?.Dispose();
        _weatherView = null;
        _sky?.Dispose();
        _sky = null;
        _terrain?.Dispose();
        _terrain = null;

        _post?.Dispose();
        _post = null;

        _context?.Unconfigure();
        _context?.Dispose();
        _context = null;
        _canvasId = null;

        IsInitialized = false;
        _disposed = false;
    }
}
