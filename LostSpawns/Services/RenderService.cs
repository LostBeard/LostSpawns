using SpawnDev.BlazorJS;
using SpawnDev.BlazorJS.JSObjects;
using SpawnDev.ILGPU.WebGPU;
using ILGPU.Runtime;
using System.Numerics;
using LostSpawns.Models;
using LostSpawns.Rendering;
using SpawnDev.VoxelEngine.Rendering;

namespace LostSpawns.Services;

/// <summary>
/// WebGPU render service using VoxelEngine's VertexPullPipeline.
/// Reads PackedQuad data directly from GPU buffers (no vertex buffer management).
/// Each chunk's quad buffer goes straight from greedy merge to the render pass.
///
/// Memory: ~8 bytes per quad vs ~216 bytes per face (27x reduction).
/// No free-list, no sub-allocation, no compaction, no buffer growth.
/// </summary>
public class RenderService : IDisposable
{
    private readonly BlazorJSRuntime _js;

    private GPUDevice? _device;
    private GPUQueue? _queue;
    private GPUCanvasContext? _context;
    private string _canvasFormat = "bgra8unorm";

    private GPUTexture? _depthTexture;
    private GPUTextureView? _depthView;
    private string? _canvasId;
    private int _canvasWidth;
    private int _canvasHeight;

    // VoxelEngine vertex-pull render pipeline (reads PackedQuad directly from storage buffer)
    private VertexPullPipeline? _voxelPipeline;

    // World reference (set during init)
    private WorldService? _world;

    private GPUBuffer? _uniformBuffer;
    private GPUBindGroup? _uniformBindGroup;

    private bool _running;
    private bool _disposed;
    private double _lastTimestamp;
    private ActionCallback<double>? _rafCallback;

    public Camera Camera { get; } = new();
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
    public int TotalChunkCount => _world?.LoadedCount ?? 0;

    public RenderService(BlazorJSRuntime js)
    {
        _js = js;
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

        _context = canvas.GetContext<GPUCanvasContext>("webgpu");

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

        // VoxelEngine vertex-pull pipeline (reads PackedQuad from storage buffers)
        _voxelPipeline = new VertexPullPipeline();
        _voxelPipeline.Init(_device, _queue, _canvasFormat);

        CreateDepthTexture();

        IsInitialized = true;
        Console.WriteLine($"[RenderService] VertexPullPipeline initialized. No vertex buffer needed.");
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
        RenderFrame();
        RequestFrame();
    }

    private void RenderFrame()
    {
        if (_device == null || _context == null || _voxelPipeline == null || _world == null)
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
                    CreateDepthTexture();
                }
            }
        }

        float aspect = (float)_canvasWidth / _canvasHeight;
        var vp = Camera.GetVpMatrix(aspect);
        var frustum = FrustumCuller.ExtractPlanes(vp);

        using var colorTexture = _context.GetCurrentTexture();
        using var colorView = colorTexture.CreateView();
        using var encoder = _device.CreateCommandEncoder();

        // Draw each visible chunk in its own render pass.
        // VertexPullPipeline.UpdateUniforms uses queue.WriteBuffer which resolves
        // before the NEXT submit - so all draws in one pass would use the last uniform.
        // Separate passes ensure each chunk gets its own section offset.
        int visible = 0;
        bool firstPass = true;
        foreach (var ((cx, cz), chunkMesh) in _world.Chunks)
        {
            if (!chunkMesh.HasMesh) continue;

            var sectionOffset = new Vector3(cx * ChunkData.SizeXZ, 0, cz * ChunkData.SizeXZ);
            var min = sectionOffset;
            var max = new Vector3(sectionOffset.X + ChunkData.SizeXZ, ChunkData.Height, sectionOffset.Z + ChunkData.SizeXZ);

            if (!FrustumCuller.IsBoxVisible(in frustum, min, max))
                continue;

            // Bake section offset into MVP: model = translate(sectionOffset)
            var model = Matrix4x4.CreateTranslation(sectionOffset);
            var mvp = model * vp;

            _voxelPipeline.UpdateUniforms(
                mvp,                                     // model-view-projection (section baked in)
                Vector3.Zero,                            // offset already in MVP
                1.0f,                                    // voxel size
                new Vector3(0.45f, 0.48f, 0.52f),       // fog color
                0.0003f,                                 // fog density
                new Vector3(0.25f, 0.26f, 0.30f),       // ambient color
                0                                        // time
            );

            // First chunk clears, subsequent chunks load (preserve previous)
            using var pass = _voxelPipeline.BeginRenderPass(encoder, colorView, _depthView!,
                clearColor: firstPass, new Vector3(0.45f, 0.48f, 0.52f));
            _voxelPipeline.DrawSection(pass, chunkMesh.QuadBuffer!, chunkMesh.QuadCount);
            pass.End();

            firstPass = false;
            visible++;
        }
        VisibleChunkCount = visible;

        // If no chunks were visible, still need to clear the canvas
        if (firstPass)
        {
            using var clearPass = _voxelPipeline.BeginRenderPass(encoder, colorView, _depthView!,
                clearColor: true, new Vector3(0.45f, 0.48f, 0.52f));
            clearPass.End();
        }

        // GameUI overlay pass (HUD, inventory, chat - renders on top of voxel scene)
        OnPostRender?.Invoke(encoder, colorView, _canvasWidth, _canvasHeight);

        using var commandBuffer = encoder.Finish();
        _queue!.Submit(new[] { commandBuffer });
    }

    private void CreateDepthTexture()
    {
        _depthView?.Dispose();
        _depthTexture?.Destroy();
        _depthTexture?.Dispose();

        _depthTexture = _device!.CreateTexture(new GPUTextureDescriptor
        {
            Size = new[] { _canvasWidth, _canvasHeight },
            Format = "depth32float",
            Usage = GPUTextureUsage.RenderAttachment,
        });
        _depthView = _depthTexture.CreateView();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _running = false;

        _rafCallback?.Dispose();
        _rafCallback = null;

        _depthView?.Dispose();
        _depthView = null;
        _depthTexture?.Destroy();
        _depthTexture?.Dispose();
        _depthTexture = null;

        _context?.Unconfigure();
        _context?.Dispose();
        _context = null;
        _canvasId = null;

        IsInitialized = false;
        _disposed = false;
    }
}
