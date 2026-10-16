using SpawnDev.SpawnJS.JSObjects;
using SpawnDev.VoxelEngine.Rendering;

namespace LostSpawns.Rendering;

/// <summary>
/// What happens to the picture after the world is drawn: screen-space ambient occlusion (contact shadows in the grass, under the
/// trees, beside the creatures, in the corners of the terraces) from the depth buffer, a depth-aware blur of it, a touch of
/// colour grading and a vignette. The world is drawn into <see cref="SceneColorView"/> and <see cref="DepthView"/>; <see cref="Run"/>
/// writes the finished picture to the canvas, where the HUD is drawn over it.
/// </summary>
public sealed class PostProcessor : IDisposable
{
    private GPUDevice? _device;
    private GPUQueue? _queue;
    private string _format = "bgra8unorm";
    private GPUBuffer? _uniforms;
    private GPUBindGroupLayout? _aoLayout, _compLayout;
    private GPURenderPipeline? _aoPipeline, _compPipeline;
    private GPUTexture? _depth, _color, _ao;
    private GPUBindGroup? _aoGroup, _compGroup;
    private bool _disposed;

    public GPUTextureView? DepthView { get; private set; }
    public GPUTextureView? SceneColorView { get; private set; }
    public int Width { get; private set; }
    public int Height { get; private set; }

    /// <summary>Occlusion on: the world is drawn into the scene texture and run through <see cref="Run"/>; off: straight into the canvas.</summary>
    public bool OcclusionEnabled { get; set; } = true;

    /// <summary>Sphere of the occlusion test, in blocks.</summary>
    public float Radius { get; set; } = 1.3f;
    /// <summary>How dark the darkest corner gets, 0..1.</summary>
    public float Intensity { get; set; } = 1.1f;
    public float Vignette { get; set; } = 0.30f;

    public void Init(GPUDevice device, GPUQueue queue, string canvasFormat)
    {
        _device = device;
        _queue = queue;
        _format = canvasFormat;
        _uniforms = device.CreateBuffer(new GPUBufferDescriptor { Size = 64, Usage = GPUBufferUsage.Uniform | GPUBufferUsage.CopyDst });

        _aoLayout = device.CreateBindGroupLayout(new GPUBindGroupLayoutDescriptor
        {
            Entries = new GPUBindGroupLayoutEntry[]
            {
                new() { Binding = 0, Visibility = GPUShaderStageFlags.FRAGMENT, Buffer = new GPUBufferBindingLayout { Type = "uniform" } },
                new() { Binding = 1, Visibility = GPUShaderStageFlags.FRAGMENT, Texture = new GPUTextureBindingLayout { SampleType = "depth", ViewDimension = "2d" } },
            },
        });
        _compLayout = device.CreateBindGroupLayout(new GPUBindGroupLayoutDescriptor
        {
            Entries = new GPUBindGroupLayoutEntry[]
            {
                new() { Binding = 0, Visibility = GPUShaderStageFlags.FRAGMENT, Buffer = new GPUBufferBindingLayout { Type = "uniform" } },
                new() { Binding = 1, Visibility = GPUShaderStageFlags.FRAGMENT, Texture = new GPUTextureBindingLayout { SampleType = "depth", ViewDimension = "2d" } },
                new() { Binding = 2, Visibility = GPUShaderStageFlags.FRAGMENT, Texture = new GPUTextureBindingLayout { SampleType = "unfilterable-float", ViewDimension = "2d" } },
                new() { Binding = 3, Visibility = GPUShaderStageFlags.FRAGMENT, Texture = new GPUTextureBindingLayout { SampleType = "unfilterable-float", ViewDimension = "2d" } },
            },
        });

        using var shader = device.CreateShaderModule(new GPUShaderModuleDescriptor { Code = Source });
        using var aoPipelineLayout = device.CreatePipelineLayout(new GPUPipelineLayoutDescriptor { BindGroupLayouts = new[] { _aoLayout } });
        using var compPipelineLayout = device.CreatePipelineLayout(new GPUPipelineLayoutDescriptor { BindGroupLayouts = new[] { _compLayout } });
        _aoPipeline = device.CreateRenderPipeline(new GPURenderPipelineDescriptor
        {
            Layout = aoPipelineLayout,
            Vertex = new GPUVertexState { Module = shader, EntryPoint = "vs_main" },
            Fragment = new GPUFragmentState { Module = shader, EntryPoint = "fs_ao", Targets = new[] { new GPUColorTargetState { Format = "r8unorm" } } },
            Primitive = new GPUPrimitiveState { Topology = GPUPrimitiveTopology.TriangleList, CullMode = GPUCullMode.None },
        });
        _compPipeline = device.CreateRenderPipeline(new GPURenderPipelineDescriptor
        {
            Layout = compPipelineLayout,
            Vertex = new GPUVertexState { Module = shader, EntryPoint = "vs_main" },
            Fragment = new GPUFragmentState { Module = shader, EntryPoint = "fs_composite", Targets = new[] { new GPUColorTargetState { Format = canvasFormat } } },
            Primitive = new GPUPrimitiveState { Topology = GPUPrimitiveTopology.TriangleList, CullMode = GPUCullMode.None },
        });
    }

    /// <summary>(Re)create the textures for a canvas of this size.</summary>
    public void Resize(int width, int height)
    {
        if (_device == null) throw new InvalidOperationException("Call Init first");
        if (width == Width && height == Height && _depth != null) return;
        ReleaseTextures();
        Width = width;
        Height = height;

        _depth = _device.CreateTexture(new GPUTextureDescriptor
        {
            Size = new[] { width, height },
            Format = "depth32float",
            Usage = GPUTextureUsage.RenderAttachment | GPUTextureUsage.TextureBinding,
        });
        DepthView = _depth.CreateView();
        _color = _device.CreateTexture(new GPUTextureDescriptor
        {
            Size = new[] { width, height },
            Format = _format,
            Usage = GPUTextureUsage.RenderAttachment | GPUTextureUsage.TextureBinding,
        });
        SceneColorView = _color.CreateView();
        _ao = _device.CreateTexture(new GPUTextureDescriptor
        {
            Size = new[] { width, height },
            Format = "r8unorm",
            Usage = GPUTextureUsage.RenderAttachment | GPUTextureUsage.TextureBinding,
        });
        var aoView = _ao.CreateView();

        _aoGroup = _device.CreateBindGroup(new GPUBindGroupDescriptor
        {
            Layout = _aoLayout!,
            Entries = new GPUBindGroupEntry[]
            {
                new() { Binding = 0, Resource = new GPUBufferBinding { Buffer = _uniforms! } },
                new() { Binding = 1, Resource = DepthView },
            },
        });
        _compGroup = _device.CreateBindGroup(new GPUBindGroupDescriptor
        {
            Layout = _compLayout!,
            Entries = new GPUBindGroupEntry[]
            {
                new() { Binding = 0, Resource = new GPUBufferBinding { Buffer = _uniforms! } },
                new() { Binding = 1, Resource = DepthView },
                new() { Binding = 2, Resource = SceneColorView },
                new() { Binding = 3, Resource = aoView },
            },
        });
        // The bind group holds the view; keep ours alive with the texture
        _aoView = aoView;
    }

    private GPUTextureView? _aoView;

    private void ReleaseTextures()
    {
        _aoGroup?.Dispose(); _aoGroup = null;
        _compGroup?.Dispose(); _compGroup = null;
        _aoView?.Dispose(); _aoView = null;
        DepthView?.Dispose(); DepthView = null;
        SceneColorView?.Dispose(); SceneColorView = null;
        _depth?.Destroy(); _depth?.Dispose(); _depth = null;
        _color?.Destroy(); _color?.Dispose(); _color = null;
        _ao?.Destroy(); _ao?.Dispose(); _ao = null;
    }

    /// <summary>Occlusion pass, then the composite onto <paramref name="canvasView"/>. The world must already be in <see cref="SceneColorView"/>.</summary>
    /// <param name="fovYRadians">The vertical field of view of the camera that drew it; <paramref name="near"/> its near plane (reversed-Z infinite projection).</param>
    public void Run(GPUCommandEncoder encoder, GPUTextureView canvasView, float fovYRadians, float near)
    {
        if (_queue == null || _aoGroup == null || _compGroup == null || _aoPipeline == null || _compPipeline == null) return;
        float tanHalf = MathF.Tan(fovYRadians * 0.5f);
        var f = new float[]
        {
            tanHalf, (float)Width / Height, near, Radius,
            0.03f, Intensity, Width, Height,
            Vignette, 1.06f /* saturation */, 0f, 0f,
            0f, 0f, 0f, 0f,
        };
        var bytes = new byte[f.Length * 4];
        Buffer.BlockCopy(f, 0, bytes, 0, bytes.Length);
        _queue.WriteBuffer(_uniforms!, 0, bytes);

        using (var pass = encoder.BeginRenderPass(new GPURenderPassDescriptor
        {
            ColorAttachments = new[] { new GPURenderPassColorAttachment { View = _aoView!, LoadOp = GPULoadOp.Clear, StoreOp = GPUStoreOp.Store, ClearValue = new double[] { 1, 1, 1, 1 } } },
        }))
        {
            pass.SetPipeline(_aoPipeline);
            pass.SetBindGroup(0, _aoGroup);
            pass.Draw(3, 1, 0, 0);
            pass.End();
        }
        using (var pass = encoder.BeginRenderPass(new GPURenderPassDescriptor
        {
            ColorAttachments = new[] { new GPURenderPassColorAttachment { View = canvasView, LoadOp = GPULoadOp.Clear, StoreOp = GPUStoreOp.Store, ClearValue = new double[] { 0, 0, 0, 1 } } },
        }))
        {
            pass.SetPipeline(_compPipeline);
            pass.SetBindGroup(0, _compGroup);
            pass.Draw(3, 1, 0, 0);
            pass.End();
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        ReleaseTextures();
        _uniforms?.Destroy(); _uniforms?.Dispose();
        _aoPipeline?.Dispose();
        _compPipeline?.Dispose();
        _aoLayout?.Dispose();
        _compLayout?.Dispose();
    }

    private const string Source = @"
struct Params {
    tanHalf: f32, aspect: f32, near: f32, radius: f32,
    bias: f32, intensity: f32, width: f32, height: f32,
    vignette: f32, saturation: f32, pad0: f32, pad1: f32,
    pad2: f32, pad3: f32, pad4: f32, pad5: f32,
};
@group(0) @binding(0) var<uniform> p: Params;
@group(0) @binding(1) var depthTex: texture_depth_2d;
@group(0) @binding(2) var colorTex: texture_2d<f32>;
@group(0) @binding(3) var aoTex: texture_2d<f32>;

@vertex
fn vs_main(@builtin(vertex_index) i: u32) -> @builtin(position) vec4<f32> {
    let uv = vec2<f32>(f32((i << 1u) & 2u), f32(i & 2u));
    return vec4<f32>(uv * 2.0 - 1.0, 0.0, 1.0);
}

// Distance along the view axis of the surface at a pixel (reversed-Z infinite projection: depth = near / distance); the sky is very far
fn linearAt(px: vec2<i32>) -> f32 {
    let size = vec2<i32>(i32(p.width), i32(p.height));
    let d = textureLoad(depthTex, clamp(px, vec2<i32>(0, 0), size - vec2<i32>(1, 1)), 0);
    if (d <= 0.0) { return 1e9; }
    return p.near / d;
}

fn viewPos(px: vec2<i32>, dist: f32) -> vec3<f32> {
    let uv = (vec2<f32>(px) + vec2<f32>(0.5, 0.5)) / vec2<f32>(p.width, p.height);
    let ndc = vec2<f32>(uv.x * 2.0 - 1.0, 1.0 - uv.y * 2.0);
    return vec3<f32>(ndc.x * p.aspect * p.tanHalf * dist, ndc.y * p.tanHalf * dist, -dist);
}

fn noiseAt(xy: vec2<f32>) -> f32 {
    return fract(52.9829189 * fract(dot(xy, vec2<f32>(0.06711056, 0.00583715))));
}

@fragment
fn fs_ao(@builtin(position) frag: vec4<f32>) -> @location(0) vec4<f32> {
    let px = vec2<i32>(frag.xy);
    let dist = linearAt(px);
    if (dist > 5e8) { return vec4<f32>(1.0, 1.0, 1.0, 1.0); }   // sky
    let P = viewPos(px, dist);

    // The surface normal from the depth round the pixel (the neighbour that is nearer in depth on each axis)
    let lx1 = linearAt(px + vec2<i32>(1, 0));
    let lx0 = linearAt(px - vec2<i32>(1, 0));
    let ly1 = linearAt(px + vec2<i32>(0, 1));
    let ly0 = linearAt(px - vec2<i32>(0, 1));
    var dx = viewPos(px + vec2<i32>(1, 0), lx1) - P;
    if (abs(lx0 - dist) < abs(lx1 - dist)) { dx = P - viewPos(px - vec2<i32>(1, 0), lx0); }
    var dy = viewPos(px + vec2<i32>(0, 1), ly1) - P;
    if (abs(ly0 - dist) < abs(ly1 - dist)) { dy = P - viewPos(px - vec2<i32>(0, 1), ly0); }
    var n = normalize(cross(dy, dx));
    if (dot(n, -P) < 0.0) { n = -n; }

    // A hemisphere of sample points round the surface, turned by a different angle at each pixel
    let angle = noiseAt(frag.xy) * 6.2831853;
    let rv = vec3<f32>(cos(angle), sin(angle), 0.0);
    let t = normalize(rv - n * dot(rv, n));
    let b = cross(n, t);
    var occ = 0.0;
    let count = 16;
    for (var i = 0; i < count; i = i + 1) {
        let fi = f32(i);
        let h = (fi + 0.5) / f32(count);
        let a = fi * 2.3999632;
        let radial = sqrt(1.0 - h);
        let k = vec3<f32>(cos(a) * radial, sin(a) * radial, sqrt(h));
        let sc = mix(0.12, 1.0, h * h);
        let s = P + (t * k.x + b * k.y + n * k.z) * (p.radius * sc);
        let w = -s.z;
        if (w <= 0.01) { continue; }
        let ndc = vec2<f32>(s.x / (p.aspect * p.tanHalf * w), s.y / (p.tanHalf * w));
        if (abs(ndc.x) > 1.0 || abs(ndc.y) > 1.0) { continue; }
        let spx = vec2<i32>(i32((ndc.x * 0.5 + 0.5) * p.width), i32((0.5 - ndc.y * 0.5) * p.height));
        let ls = linearAt(spx);
        // Occluded when the surface at the sample's pixel is nearer than the sample is; ignore what is far in front or behind (another object)
        let range = smoothstep(0.0, 1.0, p.radius / max(abs(dist - ls), 0.0001));
        if (ls < w - p.bias) { occ = occ + range; }
    }
    var ao = clamp(1.0 - p.intensity * (occ / f32(count)), 0.25, 1.0);
    // Far away the occlusion is lost in the fog anyway
    ao = mix(ao, 1.0, smoothstep(40.0, 110.0, dist));
    return vec4<f32>(ao, ao, ao, 1.0);
}

@fragment
fn fs_composite(@builtin(position) frag: vec4<f32>) -> @location(0) vec4<f32> {
    let px = vec2<i32>(frag.xy);
    var col = textureLoad(colorTex, px, 0).rgb;
    let dist = linearAt(px);
    if (dist < 5e8) {
        // A 5 x 5 blur of the occlusion that does not cross edges of depth (weights fall with the depth difference)
        var sum = 0.0;
        var wsum = 0.0;
        for (var dy = -2; dy <= 2; dy = dy + 1) {
            for (var dx = -2; dx <= 2; dx = dx + 1) {
                let q = px + vec2<i32>(dx, dy);
                let lq = linearAt(q);
                let w = 1.0 / (1.0 + abs(lq - dist) / (0.04 * dist + 0.05) * 6.0);
                sum = sum + textureLoad(aoTex, q, 0).r * w;
                wsum = wsum + w;
            }
        }
        col = col * (sum / max(wsum, 0.0001));
    }
    // Grade: a little more colour, the corners of the picture a little darker
    let luma = dot(col, vec3<f32>(0.2126, 0.7152, 0.0722));
    col = mix(vec3<f32>(luma, luma, luma), col, p.saturation);
    let uv = frag.xy / vec2<f32>(p.width, p.height) - vec2<f32>(0.5, 0.5);
    col = col * (1.0 - p.vignette * dot(uv, uv) * 2.2);
    return vec4<f32>(col, 1.0);
}
";
}
