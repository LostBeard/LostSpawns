using SpawnDev.BlazorJS;
using SpawnDev.BlazorJS.JSObjects;
using SpawnDev.ILGPU.WebGPU;
using ILGPU.Runtime;
using System.Numerics;
using LostSpawns.Models;
using LostSpawns.Rendering;

namespace LostSpawns.Services;

/// <summary>
/// WebGPU render pipeline service. Uses a single sub-allocated vertex buffer
/// with CPU-side vertex cache for proper compaction. One SetVertexBuffer per
/// frame, then Draw with firstVertex offsets per visible chunk.
/// </summary>
public class RenderService : IDisposable
{
    private readonly BlazorJSRuntime _js;

    private GPUDevice? _device;
    private GPUQueue? _queue;
    private GPUCanvasContext? _context;
    private GPURenderPipeline? _pipeline;
    private GPUShaderModule? _shaderModule;
    private string _canvasFormat = "bgra8unorm";

    private GPUTexture? _depthTexture;
    private GPUTextureView? _depthView;
    private string? _canvasId;
    private int _canvasWidth;
    private int _canvasHeight;

    // Sub-allocated vertex buffer with CPU-side cache for compaction
    private GPUBuffer? _vertexBuffer;
    private int _bufferCapacityVertices;
    private int _nextFreeVertex;
    private readonly Dictionary<(int cx, int cz), ChunkSlot> _slots = new();

    private const int BytesPerVertex = 9 * 4;  // 9 floats * 4 bytes
    private const int InitialCapacityVertices = 3_000_000; // ~108MB, 200% headroom for ~200 chunks
    private const int MaxBufferVertices = 7_000_000;       // ~252MB (under 256MB default limit)

    // Free-list: slots from removed chunks, sorted large→small for best-fit reuse
    private readonly List<(int firstVertex, int vertexCount)> _freeSlots = new();

    private GPUBuffer? _uniformBuffer;
    private GPUBindGroup? _uniformBindGroup;

    private bool _running;
    private bool _disposed;
    private double _lastTimestamp;
    private ActionCallback<double>? _rafCallback;

    private readonly float[] _mvpFloats = new float[16];
    private byte[]? _mvpBytes;

    public Camera Camera { get; } = new();
    public bool IsInitialized { get; private set; }
    public Action<float>? OnUpdate { get; set; }

    public int VisibleChunkCount { get; private set; }
    public int TotalChunkCount => _slots.Count;

    public RenderService(BlazorJSRuntime js)
    {
        _js = js;
    }

    public void Init(HTMLCanvasElement canvas, Accelerator accelerator)
    {
        if (IsInitialized) return;

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

        _shaderModule = _device.CreateShaderModule(new GPUShaderModuleDescriptor
        {
            Code = WgslShaderSource
        });

        _pipeline = _device.CreateRenderPipeline(new GPURenderPipelineDescriptor
        {
            Layout = "auto",
            Vertex = new GPUVertexState
            {
                Module = _shaderModule,
                EntryPoint = "vs_main",
                Buffers = new[]
                {
                    new GPUVertexBufferLayout
                    {
                        ArrayStride = (ulong)BytesPerVertex,
                        StepMode = GPUVertexStepMode.Vertex,
                        Attributes = new GPUVertexAttribute[]
                        {
                            new() { ShaderLocation = 0, Offset = 0,     Format = GPUVertexFormat.Float32x3 },
                            new() { ShaderLocation = 1, Offset = 3 * 4, Format = GPUVertexFormat.Float32x3 },
                            new() { ShaderLocation = 2, Offset = 6 * 4, Format = GPUVertexFormat.Float32x3 },
                        }
                    }
                }
            },
            Fragment = new GPUFragmentState
            {
                Module = _shaderModule,
                EntryPoint = "fs_main",
                Targets = new[]
                {
                    new GPUColorTargetState { Format = _canvasFormat }
                }
            },
            Primitive = new GPUPrimitiveState
            {
                Topology = GPUPrimitiveTopology.TriangleList,
                CullMode = GPUCullMode.Back,
                FrontFace = GPUFrontFace.CCW,
            },
            DepthStencil = new GPUDepthStencilState
            {
                Format = "depth24plus",
                DepthWriteEnabled = true,
                DepthCompare = "less",
            }
        });

        CreateDepthTexture();

        // Pre-allocate vertex buffer
        _bufferCapacityVertices = InitialCapacityVertices;
        _vertexBuffer = _device.CreateBuffer(new GPUBufferDescriptor
        {
            Size = (ulong)_bufferCapacityVertices * BytesPerVertex,
            Usage = GPUBufferUsage.Vertex | GPUBufferUsage.CopyDst | GPUBufferUsage.CopySrc,
        });
        _nextFreeVertex = 0;

        _uniformBuffer = _device.CreateBuffer(new GPUBufferDescriptor
        {
            Size = 64,
            Usage = GPUBufferUsage.Uniform | GPUBufferUsage.CopyDst,
        });

        _uniformBindGroup = _device.CreateBindGroup(new GPUBindGroupDescriptor
        {
            Layout = _pipeline.GetBindGroupLayout(0),
            Entries = new[]
            {
                new GPUBindGroupEntry
                {
                    Binding = 0,
                    Resource = new GPUBufferBinding { Buffer = _uniformBuffer }
                }
            }
        });

        IsInitialized = true;
        Console.WriteLine($"[RenderService] Pipeline created. Vertex buffer: {_bufferCapacityVertices} verts ({_bufferCapacityVertices * BytesPerVertex / 1024 / 1024}MB)");
    }

    /// <summary>
    /// Upload mesh for a chunk. Tries to reuse a free slot first, falls back to appending.
    /// </summary>
    public void UploadChunkMesh(int cx, int cz, float[] vertices)
    {
        var key = (cx, cz);
        int vertexCount = vertices.Length / 9;
        if (vertexCount == 0) return;

        // Remove old slot if exists
        if (_slots.Remove(key, out var oldSlot))
            _freeSlots.Add((oldSlot.FirstVertex, oldSlot.VertexCount));

        // Try to reuse a free slot (first-fit: find one big enough)
        int writeOffset = -1;
        for (int i = 0; i < _freeSlots.Count; i++)
        {
            if (_freeSlots[i].vertexCount >= vertexCount)
            {
                var free = _freeSlots[i];
                writeOffset = free.firstVertex;
                // If the free slot is much larger, split it: keep the remainder
                int remainder = free.vertexCount - vertexCount;
                if (remainder > 100) // only keep if meaningful
                    _freeSlots[i] = (free.firstVertex + vertexCount, remainder);
                else
                    _freeSlots.RemoveAt(i);
                break;
            }
        }

        // If no free slot, append at end
        if (writeOffset < 0)
        {
            if (_nextFreeVertex + vertexCount > _bufferCapacityVertices)
            {
                // Grow buffer — GPU→GPU copy, no CPU re-upload stall
                int needed = _nextFreeVertex + vertexCount;
                int newCap = Math.Min(Math.Max(needed + needed / 4, _bufferCapacityVertices + 500_000), MaxBufferVertices);
                if (newCap < needed) return; // can't fit
                GrowBuffer(newCap);
            }
            writeOffset = _nextFreeVertex;
            _nextFreeVertex += vertexCount;
        }

        // Write to GPU
        var slot = new ChunkSlot { FirstVertex = writeOffset, VertexCount = vertexCount, CpuData = vertices };
        ulong byteOffset = (ulong)writeOffset * BytesPerVertex;
        using var jsArray = new Float32Array(vertices);
        _queue!.WriteBuffer(_vertexBuffer!, byteOffset, jsArray);

        _slots[key] = slot;
    }

    /// <summary>Remove a chunk's slot and add it to the free list for reuse. Coalesces adjacent free slots.</summary>
    public void RemoveChunkMesh(int cx, int cz)
    {
        if (_slots.Remove((cx, cz), out var slot))
        {
            int start = slot.FirstVertex;
            int count = slot.VertexCount;

            // Try to coalesce with adjacent free slots
            for (int i = _freeSlots.Count - 1; i >= 0; i--)
            {
                var f = _freeSlots[i];
                // Free slot immediately before this one?
                if (f.firstVertex + f.vertexCount == start)
                {
                    start = f.firstVertex;
                    count += f.vertexCount;
                    _freeSlots.RemoveAt(i);
                }
                // Free slot immediately after this one?
                else if (start + count == f.firstVertex)
                {
                    count += f.vertexCount;
                    _freeSlots.RemoveAt(i);
                }
            }
            _freeSlots.Add((start, count));
        }
    }

    /// <summary>
    /// Grows the buffer using GPU→GPU copy. No CPU re-upload, no stall.
    /// Existing slot offsets remain valid since data is copied at the same positions.
    /// </summary>
    private void GrowBuffer(int newCapacity)
    {
        Console.WriteLine($"[RenderService] Growing buffer: {_bufferCapacityVertices} -> {newCapacity} vertices ({newCapacity * BytesPerVertex / 1024 / 1024}MB)");

        var newBuffer = _device!.CreateBuffer(new GPUBufferDescriptor
        {
            Size = (ulong)newCapacity * BytesPerVertex,
            Usage = GPUBufferUsage.Vertex | GPUBufferUsage.CopyDst | GPUBufferUsage.CopySrc,
        });

        // GPU→GPU copy existing data (fast, no CPU involvement)
        if (_nextFreeVertex > 0 && _vertexBuffer != null)
        {
            using var encoder = _device.CreateCommandEncoder();
            encoder.CopyBufferToBuffer(
                _vertexBuffer, 0,
                newBuffer, 0,
                (ulong)_nextFreeVertex * BytesPerVertex);
            using var commandBuffer = encoder.Finish();
            _queue!.Submit(new[] { commandBuffer });
        }

        _vertexBuffer?.Destroy();
        _vertexBuffer?.Dispose();
        _vertexBuffer = newBuffer;
        _bufferCapacityVertices = newCapacity;
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
        if (_device == null || _context == null || _pipeline == null ||
            _vertexBuffer == null || _slots.Count == 0)
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

        Camera.WriteMvp(_mvpFloats, aspect);
        _mvpBytes ??= new byte[64];
        Buffer.BlockCopy(_mvpFloats, 0, _mvpBytes, 0, 64);
        _queue!.WriteBuffer(_uniformBuffer!, 0, _mvpBytes);

        var frustum = FrustumCuller.ExtractPlanes(vp);

        using var colorTexture = _context.GetCurrentTexture();
        using var colorView = colorTexture.CreateView();
        using var encoder = _device.CreateCommandEncoder();

        using var pass = encoder.BeginRenderPass(new GPURenderPassDescriptor
        {
            ColorAttachments = new[]
            {
                new GPURenderPassColorAttachment
                {
                    View = colorView,
                    LoadOp = GPULoadOp.Clear,
                    StoreOp = GPUStoreOp.Store,
                    ClearValue = new GPUColorDict { R = 0.45, G = 0.48, B = 0.52, A = 1.0 }, // overcast sky
                }
            },
            DepthStencilAttachment = new GPURenderPassDepthStencilAttachment
            {
                View = _depthView!,
                DepthLoadOp = "clear",
                DepthStoreOp = "store",
                DepthClearValue = 1.0f,
            }
        });

        pass.SetPipeline(_pipeline);
        pass.SetBindGroup(0, _uniformBindGroup!);

        // Bind the single vertex buffer ONCE
        pass.SetVertexBuffer(0, _vertexBuffer);

        // Draw each visible chunk using firstVertex offset
        int visible = 0;
        foreach (var ((cx, cz), slot) in _slots)
        {
            if (slot.VertexCount == 0) continue;

            var min = new Vector3(cx * ChunkData.SizeXZ, 0, cz * ChunkData.SizeXZ);
            var max = new Vector3(cx * ChunkData.SizeXZ + ChunkData.SizeXZ, ChunkData.Height, cz * ChunkData.SizeXZ + ChunkData.SizeXZ);

            if (!FrustumCuller.IsBoxVisible(in frustum, min, max))
                continue;

            pass.Draw((uint)slot.VertexCount, 1, (uint)slot.FirstVertex, 0);
            visible++;
        }
        VisibleChunkCount = visible;

        pass.End();

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
            Format = "depth24plus",
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

        _vertexBuffer?.Destroy();
        _vertexBuffer?.Dispose();
        _vertexBuffer = null;
        _slots.Clear();
        _freeSlots.Clear();
        _nextFreeVertex = 0;

        _uniformBindGroup?.Dispose();
        _uniformBindGroup = null;
        _uniformBuffer?.Destroy();
        _uniformBuffer?.Dispose();
        _uniformBuffer = null;
        _depthView?.Dispose();
        _depthView = null;
        _depthTexture?.Destroy();
        _depthTexture?.Dispose();
        _depthTexture = null;
        _shaderModule?.Dispose();
        _shaderModule = null;
        _pipeline = null;
        _context?.Unconfigure();
        _context?.Dispose();
        _context = null;
        _canvasId = null;

        // Allow re-init on next navigation to the game page
        IsInitialized = false;
        _disposed = false;
    }

    /// <summary>Metadata for a chunk's sub-region within the shared vertex buffer.</summary>
    private struct ChunkSlot
    {
        public int FirstVertex;
        public int VertexCount;
        public float[] CpuData;   // Cached for compaction
    }

    #region WGSL Shader

    private const string WgslShaderSource = @"
struct Uniforms {
    mvp : mat4x4<f32>,
};

@group(0) @binding(0) var<uniform> uniforms : Uniforms;

struct VertexInput {
    @location(0) position : vec3<f32>,
    @location(1) normal   : vec3<f32>,
    @location(2) color    : vec3<f32>,
};

struct VertexOutput {
    @builtin(position) clip_position : vec4<f32>,
    @location(0) world_normal : vec3<f32>,
    @location(1) base_color   : vec3<f32>,
    @location(2) world_pos    : vec3<f32>,
};

@vertex
fn vs_main(input : VertexInput) -> VertexOutput {
    var output : VertexOutput;
    output.clip_position = uniforms.mvp * vec4<f32>(input.position, 1.0);
    output.world_normal = input.normal;
    output.base_color = input.color;
    output.world_pos = input.position;
    return output;
}

// Hash function for subtle per-block color variation
fn hash2(p : vec2<f32>) -> f32 {
    let h = dot(p, vec2<f32>(127.1, 311.7));
    return fract(sin(h) * 43758.5453123);
}

@fragment
fn fs_main(input : VertexOutput) -> @location(0) vec4<f32> {
    // === Dual-light system (warm sun + cool fill) ===
    let sun_dir = normalize(vec3<f32>(0.35, 0.85, 0.40));
    let fill_dir = normalize(vec3<f32>(-0.3, 0.2, -0.5));
    let n = normalize(input.world_normal);

    let sun_intensity = max(dot(n, sun_dir), 0.0);
    let fill_intensity = max(dot(n, fill_dir), 0.0);

    // Overcast daylight - desaturated, cold, post-apocalyptic
    let sun_color = vec3<f32>(0.85, 0.82, 0.75);   // muted warm, filtered through clouds
    let fill_color = vec3<f32>(0.45, 0.48, 0.55);   // cold blue-gray fill
    let ambient = vec3<f32>(0.25, 0.26, 0.30);       // dark ambient base

    let light = ambient + sun_color * sun_intensity * 0.50 + fill_color * fill_intensity * 0.20;

    // === Per-block color variation (breaks visual monotony) ===
    let block_pos = floor(input.world_pos);
    let variation = hash2(vec2<f32>(block_pos.x, block_pos.z)) * 0.08 - 0.04;

    // === Face-dependent tinting ===
    var color = input.base_color;

    // Top faces get slight brightness boost (skylight)
    if (n.y > 0.5) {
        color = color * 1.05 + vec3<f32>(0.01, 0.02, 0.0);
    }
    // Bottom faces get darkened
    if (n.y < -0.5) {
        color = color * 0.70;
    }
    // Side faces get slight dirtiness (lower saturation)
    if (abs(n.y) < 0.1) {
        color = mix(color, vec3<f32>(dot(color, vec3<f32>(0.3, 0.59, 0.11))), 0.12);
    }

    // Apply variation
    color = color + vec3<f32>(variation);

    // === Apply lighting ===
    color = color * light;

    // === Distance fog ===
    let dist = length(input.world_pos);
    let fog_start = 80.0;
    let fog_end = 220.0;
    let fog_color = vec3<f32>(0.45, 0.48, 0.52);  // overcast gray fog
    let fog_factor = clamp((dist - fog_start) / (fog_end - fog_start), 0.0, 1.0);
    let fog_factor_smooth = fog_factor * fog_factor; // quadratic falloff
    color = mix(color, fog_color, fog_factor_smooth);

    return vec4<f32>(color, 1.0);
}
";

    #endregion
}
