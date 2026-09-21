using System.Numerics;
using System.Runtime.InteropServices;
using LostSpawns.Content;
using LostSpawns.Services;
using SpawnDev.SpawnJS.JSObjects;
using SpawnDev.VoxelEngine.Rendering;

namespace LostSpawns.Rendering;

/// <summary>
/// Draws GPU-resident entity meshes inside the voxel render pass.
/// Skinned GLBs use JOINTS_0/WEIGHTS_0 + a per-draw bone palette evaluated
/// from Walk/Attack/Idle clips; unskinned meshes fall back to rest pose.
/// </summary>
public sealed class EntityMeshPipeline : IDisposable
{
    private GPUDevice? _device;
    private GPUQueue? _queue;
    private GPURenderPipeline? _pipelineSkinned;
    private GPURenderPipeline? _pipelineStatic;
    private GPUBuffer? _uniformBuffer;
    private GPUBuffer? _boneBuffer;
    private GPUBindGroup? _bindGroupSkinned;
    private GPUBindGroup? _bindGroupStatic;
    private bool _disposed;

    // mvp + model + color + lightDir = 192, pad to 256
    private const int UniformBytes = 256;
    private const int BoneBytes = GlbSkinRuntime.MaxJoints * 64; // 64 * mat4
    private readonly byte[] _uniformScratch = new byte[UniformBytes];
    private readonly byte[] _boneScratch = new byte[BoneBytes];
    private readonly Matrix4x4[] _palette = new Matrix4x4[GlbSkinRuntime.MaxJoints];

    public void Init(GPUDevice device, GPUQueue queue, string colorFormat)
    {
        _device = device;
        _queue = queue;

        const string commonStructs = """
struct Uniforms {
  mvp: mat4x4f,
  model: mat4x4f,
  color: vec4f,
  lightDir: vec4f,
}
struct Bones {
  mats: array<mat4x4f, 64>,
}
@group(0) @binding(0) var<uniform> u: Uniforms;
""";

        const string skinnedWgsl = commonStructs + """
@group(0) @binding(1) var<uniform> bones: Bones;

struct VSOut {
  @builtin(position) pos: vec4f,
  @location(0) worldN: vec3f,
  @location(1) color: vec4f,
}

@vertex
fn vs_main(
  @location(0) position: vec3f,
  @location(1) normal: vec3f,
  @location(2) joints: vec4<u32>,
  @location(3) weights: vec4f,
) -> VSOut {
  var o: VSOut;
  let skin =
      bones.mats[joints.x] * weights.x +
      bones.mats[joints.y] * weights.y +
      bones.mats[joints.z] * weights.z +
      bones.mats[joints.w] * weights.w;
  let skinnedPos = skin * vec4f(position, 1.0);
  let skinnedN = skin * vec4f(normal, 0.0);
  o.pos = u.mvp * skinnedPos;
  let n = (u.model * skinnedN).xyz;
  let nlen = length(n);
  o.worldN = select(vec3f(0.0, 1.0, 0.0), n / nlen, nlen > 0.001);
  o.color = u.color;
  return o;
}

@fragment
fn fs_main(i: VSOut) -> @location(0) vec4f {
  let L = normalize(u.lightDir.xyz);
  let ndl = clamp(dot(normalize(i.worldN), L), 0.15, 1.0);
  return vec4f(i.color.rgb * ndl, i.color.a);
}
""";

        const string staticWgsl = commonStructs + """
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
  return vec4f(i.color.rgb * ndl, i.color.a);
}
""";

        _uniformBuffer = device.CreateBuffer(new GPUBufferDescriptor
        {
            Size = UniformBytes,
            Usage = GPUBufferUsage.Uniform | GPUBufferUsage.CopyDst,
        });
        _boneBuffer = device.CreateBuffer(new GPUBufferDescriptor
        {
            Size = BoneBytes,
            Usage = GPUBufferUsage.Uniform | GPUBufferUsage.CopyDst,
        });

        using var skinnedShader = device.CreateShaderModule(new GPUShaderModuleDescriptor { Code = skinnedWgsl });
        using var staticShader = device.CreateShaderModule(new GPUShaderModuleDescriptor { Code = staticWgsl });

        var depth = new GPUDepthStencilState
        {
            Format = ReversedZHelper.DepthFormat,
            DepthWriteEnabled = true,
            DepthCompare = ReversedZHelper.DepthCompare,
        };
        var primitive = new GPUPrimitiveState
        {
            Topology = GPUPrimitiveTopology.TriangleList,
            CullMode = GPUCullMode.Back,
            FrontFace = GPUFrontFace.CCW,
        };
        var fragmentSkinned = new GPUFragmentState
        {
            Module = skinnedShader,
            EntryPoint = "fs_main",
            Targets = new[] { new GPUColorTargetState { Format = colorFormat } },
        };
        var fragmentStatic = new GPUFragmentState
        {
            Module = staticShader,
            EntryPoint = "fs_main",
            Targets = new[] { new GPUColorTargetState { Format = colorFormat } },
        };

        var posLayout = new GPUVertexBufferLayout
        {
            ArrayStride = 12,
            Attributes = new[]
            {
                new GPUVertexAttribute { Format = GPUVertexFormat.Float32x3, Offset = 0, ShaderLocation = 0 },
            },
        };
        var nrmLayout = new GPUVertexBufferLayout
        {
            ArrayStride = 12,
            Attributes = new[]
            {
                new GPUVertexAttribute { Format = GPUVertexFormat.Float32x3, Offset = 0, ShaderLocation = 1 },
            },
        };
        var jointsLayout = new GPUVertexBufferLayout
        {
            ArrayStride = 4,
            Attributes = new[]
            {
                new GPUVertexAttribute { Format = GPUVertexFormat.UInt8x4, Offset = 0, ShaderLocation = 2 },
            },
        };
        var weightsLayout = new GPUVertexBufferLayout
        {
            ArrayStride = 16,
            Attributes = new[]
            {
                new GPUVertexAttribute { Format = GPUVertexFormat.Float32x4, Offset = 0, ShaderLocation = 3 },
            },
        };

        _pipelineSkinned = device.CreateRenderPipeline(new GPURenderPipelineDescriptor
        {
            Layout = "auto",
            Vertex = new GPUVertexState
            {
                Module = skinnedShader,
                EntryPoint = "vs_main",
                Buffers = new[] { posLayout, nrmLayout, jointsLayout, weightsLayout },
            },
            Fragment = fragmentSkinned,
            Primitive = primitive,
            DepthStencil = depth,
        });

        _pipelineStatic = device.CreateRenderPipeline(new GPURenderPipelineDescriptor
        {
            Layout = "auto",
            Vertex = new GPUVertexState
            {
                Module = staticShader,
                EntryPoint = "vs_main",
                Buffers = new[] { posLayout, nrmLayout },
            },
            Fragment = fragmentStatic,
            Primitive = primitive,
            DepthStencil = depth,
        });

        using var layoutS = _pipelineSkinned.GetBindGroupLayout(0);
        _bindGroupSkinned = device.CreateBindGroup(new GPUBindGroupDescriptor
        {
            Layout = layoutS,
            Entries = new[]
            {
                new GPUBindGroupEntry { Binding = 0, Resource = new GPUBufferBinding { Buffer = _uniformBuffer } },
                new GPUBindGroupEntry { Binding = 1, Resource = new GPUBufferBinding { Buffer = _boneBuffer } },
            },
        });

        using var layoutT = _pipelineStatic.GetBindGroupLayout(0);
        _bindGroupStatic = device.CreateBindGroup(new GPUBindGroupDescriptor
        {
            Layout = layoutT,
            Entries = new[]
            {
                new GPUBindGroupEntry { Binding = 0, Resource = new GPUBufferBinding { Buffer = _uniformBuffer } },
            },
        });
    }

    public void DrawEntities(
        GPURenderPassEncoder pass,
        Matrix4x4 viewProjection,
        IReadOnlyList<WanderingEntity> entities,
        Func<EntityKind, GpuEntityMesh?> resolveMesh,
        Vector3 lightDir,
        float timeSeconds)
    {
        if (_queue == null || entities.Count == 0) return;

        for (int i = 0; i < entities.Count; i++)
        {
            var e = entities[i];
            var mesh = resolveMesh(e.Kind);
            if (mesh == null) continue;

            float speed = MathF.Sqrt(e.Velocity.X * e.Velocity.X + e.Velocity.Z * e.Velocity.Z);
            bool charging = e.Alert == AlertMode.Charge;
            string? clip = mesh.Skin?.ResolveClip(charging, speed);
            // Per-entity phase so a herd isn't synchronized.
            float animT = timeSeconds + e.Id * 0.37f;

            if (mesh.IsSkinned && mesh.Skin != null)
            {
                mesh.Skin.EvaluatePalette(clip, animT, _palette);
                MemoryMarshal.AsBytes(_palette.AsSpan()).CopyTo(_boneScratch);
                _queue.WriteBuffer(_boneBuffer!, 0, _boneScratch);
            }

            var model = BuildModelMatrix(e, mesh, speed, charging, skinned: mesh.IsSkinned);
            var mvp = model * viewProjection;
            var color = ColorForKind(e);
            if (e.HitFlashTimer > 0f)
                color = new Vector4(1f, 1f, 1f, 1f);

            WriteUniforms(mvp, model, color, lightDir);
            _queue.WriteBuffer(_uniformBuffer!, 0, _uniformScratch);

            if (mesh.IsSkinned)
            {
                pass.SetPipeline(_pipelineSkinned!);
                pass.SetBindGroup(0, _bindGroupSkinned!);
                pass.SetVertexBuffer(0, mesh.PositionBuffer);
                pass.SetVertexBuffer(1, mesh.NormalBuffer);
                pass.SetVertexBuffer(2, mesh.JointsBuffer!);
                pass.SetVertexBuffer(3, mesh.WeightsBuffer!);
            }
            else
            {
                pass.SetPipeline(_pipelineStatic!);
                pass.SetBindGroup(0, _bindGroupStatic!);
                pass.SetVertexBuffer(0, mesh.PositionBuffer);
                pass.SetVertexBuffer(1, mesh.NormalBuffer);
            }

            pass.SetIndexBuffer(mesh.IndexBuffer, mesh.IndexFormat);
            pass.DrawIndexed((uint)mesh.IndexCount);
        }
    }

    private static Matrix4x4 BuildModelMatrix(
        WanderingEntity e, GpuEntityMesh mesh, float speed, bool charging, bool skinned)
    {
        // Armature scale (~0.3) is already in the skin hierarchy for skinned meshes.
        float scale = skinned ? ScaleForKindSkinned(e.Kind) : ScaleForKind(e.Kind);

        float yaw = 0f;
        if (speed > 0.05f)
            yaw = MathF.Atan2(e.Velocity.X, e.Velocity.Z);

        // Procedural bob only for unskinned fallback.
        float bob = 0f;
        if (!skinned && speed > 0.08f)
            bob = MathF.Abs(MathF.Sin(Environment.TickCount * 0.01f + e.Id)) * 0.06f * scale;

        float pitch = 0f;
        if (!skinned && charging)
            pitch = -0.2f;

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

    // Skinned Quaternius meshes already include ~0.3 armature scale in the
    // hierarchy; these multipliers tune relative size across kinds.
    private static float ScaleForKindSkinned(EntityKind kind) => kind switch
    {
        EntityKind.Wolf => 1.15f,
        EntityKind.Deer => 1.25f,
        EntityKind.Bear => 1.9f,
        EntityKind.Boar => 0.85f,
        EntityKind.Rabbit => 0.4f,
        EntityKind.Crow => 0.45f,
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
        _bindGroupSkinned?.Dispose();
        _bindGroupStatic?.Dispose();
        _bindGroupSkinned = null;
        _bindGroupStatic = null;
        _uniformBuffer?.Destroy(); _uniformBuffer?.Dispose(); _uniformBuffer = null;
        _boneBuffer?.Destroy(); _boneBuffer?.Dispose(); _boneBuffer = null;
        _pipelineSkinned?.Dispose(); _pipelineSkinned = null;
        _pipelineStatic?.Dispose(); _pipelineStatic = null;
    }
}
