using System.Numerics;
using System.Runtime.InteropServices;
using LostSpawns.Content;
using LostSpawns.Services;
using SpawnDev.SpawnJS.JSObjects;
using SpawnDev.VoxelEngine.Rendering;

namespace LostSpawns.Rendering;

/// <summary>
/// Draws GPU-resident entity meshes (rest pose from GLB) inside the voxel
/// render pass so they share reversed-Z depth. Walk/attack clips are not
/// skinned yet - motion is approximated with yaw + bob + charge lean until
/// a joint palette upload lands.
/// </summary>
public sealed class EntityMeshPipeline : IDisposable
{
    private GPUDevice? _device;
    private GPUQueue? _queue;
    private GPURenderPipeline? _pipeline;
    private GPUBuffer? _uniformBuffer;
    private GPUBindGroup? _bindGroup;
    private bool _disposed;

    // Per-frame uniform scratch (MVP + model + color + lightDir) = 192 bytes, pad to 256.
    private const int UniformBytes = 256;
    private readonly byte[] _uniformScratch = new byte[UniformBytes];

    public void Init(GPUDevice device, GPUQueue queue, string colorFormat)
    {
        _device = device;
        _queue = queue;

        const string wgsl = """
struct Uniforms {
  mvp: mat4x4f,
  model: mat4x4f,
  color: vec4f,
  lightDir: vec4f,
}

@group(0) @binding(0) var<uniform> u: Uniforms;

struct VSOut {
  @builtin(position) pos: vec4f,
  @location(0) worldN: vec3f,
  @location(1) color: vec4f,
}

@vertex
fn vs_main(
  @location(0) position: vec3f,
  @location(1) normal: vec3f,
) -> VSOut {
  var o: VSOut;
  o.pos = u.mvp * vec4f(position, 1.0);
  let n = (u.model * vec4f(normal, 0.0)).xyz;
  let nlen = length(n);
  o.worldN = select(vec3f(0.0, 1.0, 0.0), n / nlen, nlen > 0.001);
  o.color = u.color;
  return o;
}

@fragment
fn fs_main(i: VSOut) -> @location(0) vec4f {
  let L = normalize(u.lightDir.xyz);
  let ndl = clamp(dot(normalize(i.worldN), L), 0.15, 1.0);
  let lit = i.color.rgb * ndl;
  return vec4f(lit, i.color.a);
}
""";

        using var shader = device.CreateShaderModule(new GPUShaderModuleDescriptor { Code = wgsl });

        _pipeline = device.CreateRenderPipeline(new GPURenderPipelineDescriptor
        {
            Layout = "auto",
            Vertex = new GPUVertexState
            {
                Module = shader,
                EntryPoint = "vs_main",
                Buffers = new[]
                {
                    new GPUVertexBufferLayout
                    {
                        ArrayStride = 12,
                        Attributes = new[]
                        {
                            new GPUVertexAttribute
                            {
                                Format = GPUVertexFormat.Float32x3,
                                Offset = 0,
                                ShaderLocation = 0,
                            },
                        },
                    },
                    new GPUVertexBufferLayout
                    {
                        ArrayStride = 12,
                        Attributes = new[]
                        {
                            new GPUVertexAttribute
                            {
                                Format = GPUVertexFormat.Float32x3,
                                Offset = 0,
                                ShaderLocation = 1,
                            },
                        },
                    },
                },
            },
            Fragment = new GPUFragmentState
            {
                Module = shader,
                EntryPoint = "fs_main",
                Targets = new[]
                {
                    new GPUColorTargetState { Format = colorFormat },
                },
            },
            Primitive = new GPUPrimitiveState
            {
                Topology = GPUPrimitiveTopology.TriangleList,
                CullMode = GPUCullMode.Back,
                FrontFace = GPUFrontFace.CCW,
            },
            DepthStencil = new GPUDepthStencilState
            {
                Format = ReversedZHelper.DepthFormat,
                DepthWriteEnabled = true,
                DepthCompare = ReversedZHelper.DepthCompare,
            },
        });

        _uniformBuffer = device.CreateBuffer(new GPUBufferDescriptor
        {
            Size = UniformBytes,
            Usage = GPUBufferUsage.Uniform | GPUBufferUsage.CopyDst,
        });

        using var layout = _pipeline.GetBindGroupLayout(0);
        _bindGroup = device.CreateBindGroup(new GPUBindGroupDescriptor
        {
            Layout = layout,
            Entries = new[]
            {
                new GPUBindGroupEntry
                {
                    Binding = 0,
                    Resource = new GPUBufferBinding { Buffer = _uniformBuffer },
                },
            },
        });
    }

    /// <summary>
    /// Draw all entities that have a GPU mesh. Call inside the voxel render
    /// pass (after terrain draws) so depth testing works against voxels.
    /// </summary>
    public void DrawEntities(
        GPURenderPassEncoder pass,
        Matrix4x4 viewProjection,
        IReadOnlyList<WanderingEntity> entities,
        Func<EntityKind, GpuEntityMesh?> resolveMesh,
        Vector3 lightDir,
        float timeSeconds)
    {
        if (_pipeline == null || _queue == null || _bindGroup == null || entities.Count == 0)
            return;

        pass.SetPipeline(_pipeline);
        pass.SetBindGroup(0, _bindGroup);

        for (int i = 0; i < entities.Count; i++)
        {
            var e = entities[i];
            var mesh = resolveMesh(e.Kind);
            if (mesh == null) continue;

            var model = BuildModelMatrix(e, mesh, timeSeconds);
            var mvp = model * viewProjection; // row-vector: v * model * vp

            var color = ColorForKind(e);
            if (e.HitFlashTimer > 0f)
                color = new Vector4(1f, 1f, 1f, 1f);

            WriteUniforms(mvp, model, color, lightDir);
            _queue.WriteBuffer(_uniformBuffer!, 0, _uniformScratch);

            pass.SetVertexBuffer(0, mesh.PositionBuffer);
            pass.SetVertexBuffer(1, mesh.NormalBuffer);
            pass.SetIndexBuffer(mesh.IndexBuffer, mesh.IndexFormat);
            pass.DrawIndexed((uint)mesh.IndexCount);
        }
    }

    private static Matrix4x4 BuildModelMatrix(WanderingEntity e, GpuEntityMesh mesh, float timeSeconds)
    {
        float scale = ScaleForKind(e.Kind);
        // Quaternius animals face +Z; yaw from horizontal velocity.
        float yaw = 0f;
        float speed = MathF.Sqrt(e.Velocity.X * e.Velocity.X + e.Velocity.Z * e.Velocity.Z);
        if (speed > 0.05f)
            yaw = MathF.Atan2(e.Velocity.X, e.Velocity.Z);

        // Walk bob when moving; faster bob when charging. Uses Walk clip presence
        // only as a hint that the asset expects locomotion (skinning later).
        float bob = 0f;
        float bobFreq = e.Alert == AlertMode.Charge ? 10f : 7f;
        if (speed > 0.08f)
            bob = MathF.Abs(MathF.Sin(timeSeconds * bobFreq + e.Id)) * 0.06f * scale;

        // Charge lean: pitch toward travel direction.
        float pitch = 0f;
        if (e.Alert == AlertMode.Charge && mesh.HasAttackClip)
            pitch = -0.25f;
        else if (e.Alert == AlertMode.Charge)
            pitch = -0.18f;

        // Feet at mesh BoundsMin.Y -> world ground. Entity Position.Y is ~ground+1.
        float groundY = e.Kind == EntityKind.Crow
            ? e.Position.Y
            : e.Position.Y - 1f;
        float y = groundY - mesh.BoundsMin.Y * scale + bob;

        var t = Matrix4x4.CreateTranslation(e.Position.X, y, e.Position.Z);
        var rYaw = Matrix4x4.CreateRotationY(yaw);
        var rPitch = Matrix4x4.CreateRotationX(pitch);
        var s = Matrix4x4.CreateScale(scale);
        return s * rPitch * rYaw * t;
    }

    private static float ScaleForKind(EntityKind kind) => kind switch
    {
        EntityKind.Wolf => 1.05f,
        EntityKind.Deer => 1.15f,
        EntityKind.Bear => 1.85f,
        EntityKind.Boar => 0.75f,
        EntityKind.Rabbit => 0.32f,
        EntityKind.Crow => 0.4f,
        _ => 1f,
    };

    private static Vector4 ColorForKind(WanderingEntity e)
    {
        float j = e.ColorJitter;
        return e.Kind switch
        {
            EntityKind.Rabbit => new Vector4(0.85f + j * 0.1f, 0.75f, 0.65f, 1f),
            EntityKind.Boar => new Vector4(0.45f + j * 0.1f, 0.32f, 0.22f, 1f),
            EntityKind.Crow => new Vector4(0.12f, 0.12f, 0.14f, 1f),
            EntityKind.Wolf => new Vector4(0.55f + j * 0.15f, 0.55f, 0.6f, 1f),
            EntityKind.Deer => new Vector4(0.65f + j * 0.1f, 0.45f, 0.28f, 1f),
            EntityKind.Bear => new Vector4(0.35f + j * 0.08f, 0.22f, 0.14f, 1f),
            _ => new Vector4(0.7f, 0.7f, 0.7f, 1f),
        };
    }

    private void WriteUniforms(Matrix4x4 mvp, Matrix4x4 model, Vector4 color, Vector3 lightDir)
    {
        // Layout matches WGSL: mat4 + mat4 + vec4 + vec4 = 192, buffer is 256.
        var span = _uniformScratch.AsSpan();
        span.Clear();
        MemoryMarshal.Write(span.Slice(0, 64), in mvp);
        MemoryMarshal.Write(span.Slice(64, 64), in model);
        MemoryMarshal.Write(span.Slice(128, 16), in color);
        var ld = new Vector4(lightDir.X, lightDir.Y, lightDir.Z, 0f);
        MemoryMarshal.Write(span.Slice(144, 16), in ld);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _bindGroup?.Dispose();
        _bindGroup = null;
        _uniformBuffer?.Destroy();
        _uniformBuffer?.Dispose();
        _uniformBuffer = null;
        _pipeline?.Dispose();
        _pipeline = null;
    }
}
