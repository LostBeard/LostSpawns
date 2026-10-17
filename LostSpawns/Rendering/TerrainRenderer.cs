using System.Numerics;
using LostSpawns.Services;
using SpawnDev.ILGPU.WebGPU;
using SpawnDev.SpawnJS.JSObjects;
using SpawnDev.VoxelEngine.Rendering;

namespace LostSpawns.Rendering;

/// <summary>What one frame of terrain drew.</summary>
public readonly record struct TerrainFrameStats(int VisibleColumns, int DrawCalls, long VisibleQuads);

/// <summary>
/// Draws the world's columns with <see cref="BlockRenderPipeline"/>: textured, lit and fogged blocks in three passes (opaque,
/// plants, water). The caller sets the frame (camera, sun, fog) with <see cref="BeginFrame"/>, begins the render pass, then calls
/// <see cref="PrepareVisible"/>, <see cref="DrawSolid"/>, anything that sits in the world (creatures, particles), and finally
/// <see cref="DrawWater"/> so water blends over everything behind it.
/// </summary>
public sealed class TerrainRenderer : IDisposable
{
    private readonly BlockRenderPipeline _pipe = new();
    private BlockTextureAtlas? _atlas;
    private BiomeTintMap? _biomes;
    private bool _disposed;

    // Per-frame scratch, reused
    private readonly List<WorldColumn> _visible = new(1024);
    private readonly List<(float dist, WorldColumn col)> _waterOrder = new(512);
    private static readonly BlockRenderPipeline.Pass[] SolidPasses = { BlockRenderPipeline.Pass.Opaque, BlockRenderPipeline.Pass.Cross };
    // The column whose origin each slot of this pipeline holds
    private readonly WorldColumn?[] _slotOwner = new WorldColumn?[WorldService.MaxColumnSlots];
    private int _drawCalls;
    private long _quads;

    public BlockRenderPipeline Pipeline => _pipe;
    /// <summary>The frame uniforms (camera, light, fog), which creatures and particles read too so they are lit like the ground.</summary>
    public GPUBuffer FrameBuffer => _pipe.FrameBuffer;
    public BiomeTintMap? Biomes => _biomes;

    public void Init(GPUDevice device, GPUQueue queue, string colorFormat, LightVolume? light = null)
    {
        _pipe.Init(device, queue, colorFormat, WorldService.MaxColumnSlots);

        _biomes = BiomeTintMap.Create(device, queue);
        BiomeColours.Fill(_biomes);
        _pipe.SetBiomeTints(_biomes);

        _atlas = BlockTextureAtlas.FromRgba(device, queue, BlockTextures.Build(), BlockTextures.AtlasWidth, BlockTextures.AtlasHeight, BlockTextures.TileSize);
        _pipe.SetTextures(_atlas);
        _pipe.SetAnimations(BlockStyle.Animations());
        _pipe.SetMaterials(BlockStyle.Materials());
        if (light != null) _pipe.SetLight(light);
    }

    /// <summary>Upload this frame's camera, light and fog.</summary>
    public void BeginFrame(in BlockFrameUniforms frame) => _pipe.BeginFrame(in frame);

    /// <summary>
    /// Begin the frame's pass. <paramref name="clear"/> false keeps what the targets hold (the second eye of a stereo canvas draws
    /// into its own half after the first eye's pass).
    /// </summary>
    public GPURenderPassEncoder BeginRenderPass(GPUCommandEncoder encoder, GPUTextureView color, GPUTextureView depth, Vector3 clearColor, bool clear = true)
    {
        if (clear) return _pipe.BeginRenderPass(encoder, color, depth, clearColor);
        return encoder.BeginRenderPass(new GPURenderPassDescriptor
        {
            ColorAttachments = new[]
            {
                new GPURenderPassColorAttachment { View = color, LoadOp = GPULoadOp.Load, StoreOp = GPUStoreOp.Store },
            },
            DepthStencilAttachment = new GPURenderPassDepthStencilAttachment
            {
                View = depth,
                DepthLoadOp = "load",
                DepthStoreOp = "store",
            },
        });
    }

    /// <summary>
    /// Choose the columns this frame draws: meshed, in the frustum, within <paramref name="maxDistance"/> blocks. A column's origin
    /// is written to the pipeline the first time it is seen.
    /// </summary>
    public int PrepareVisible(WorldService world, in FrustumCuller.Frustum frustum, Vector3 cameraPos, float maxDistance)
    {
        _visible.Clear();
        _waterOrder.Clear();
        _drawCalls = 0;
        _quads = 0;
        float maxSq = maxDistance * maxDistance;
        foreach (var col in world.Columns.Values)
        {
            if (!col.HasMesh) continue;
            var min = new Vector3(col.Cx * 16, col.LowestSection * 16, col.Cz * 16);
            var max = new Vector3(col.Cx * 16 + 16, (col.HighestSection + 1) * 16, col.Cz * 16 + 16);
            // Nearest point of the column's box to the camera, so a column the camera stands in is never culled by distance
            float dx = MathF.Max(MathF.Max(min.X - cameraPos.X, 0f), cameraPos.X - max.X);
            float dz = MathF.Max(MathF.Max(min.Z - cameraPos.Z, 0f), cameraPos.Z - max.Z);
            if (dx * dx + dz * dz > maxSq) continue;
            if (!FrustumCuller.IsBoxVisible(in frustum, min, max)) continue;

            // This pipeline's origin buffer holds the column's place from the first frame it is seen (a slot given to another column later is rewritten)
            if (!ReferenceEquals(_slotOwner[col.Slot], col))
            {
                _pipe.SetSectionOrigin(col.Slot, new Vector3(col.Cx * 16, 0f, col.Cz * 16));
                _slotOwner[col.Slot] = col;
            }
            _visible.Add(col);
            if (col.Mesh!.Water.Count > 0)
            {
                float cdx = col.Cx * 16 + 8 - cameraPos.X, cdz = col.Cz * 16 + 8 - cameraPos.Z;
                _waterOrder.Add((cdx * cdx + cdz * cdz, col));
            }
        }
        return _visible.Count;
    }

    // --- The headset: both eyes draw the same bundle of draws (VoxelEngine local.19 render bundles). The per-draw cost of a headset is the interop of each call, not the GPU, so the frame's
    // draws are recorded once and run in both eyes, and again only when the set of draws changes ---

    private GPURenderBundle? _solidBundle, _waterBundle;
    private long _solidSignature, _waterSignature;
    private int _solidFrameVersion = -1, _waterFrameVersion = -1;
    private const float BundleMargin = 8f;

    /// <summary>
    /// Choose the columns both eyes draw: meshed, within <paramref name="maxDistance"/> of the head, and in either eye's frustum (with a margin, so a column at the edge does not flip in and out
    /// of the set as the head moves and the bundle is not recorded again for nothing).
    /// </summary>
    public int PrepareVisibleStereo(WorldService world, in FrustumCuller.Frustum left, in FrustumCuller.Frustum right, Vector3 head, float maxDistance)
    {
        _visible.Clear();
        _waterOrder.Clear();
        _drawCalls = 0;
        _quads = 0;
        float maxSq = maxDistance * maxDistance;
        foreach (var col in world.Columns.Values)
        {
            if (!col.HasMesh) continue;
            var min = new Vector3(col.Cx * 16, col.LowestSection * 16, col.Cz * 16);
            var max = new Vector3(col.Cx * 16 + 16, (col.HighestSection + 1) * 16, col.Cz * 16 + 16);
            float dx = MathF.Max(MathF.Max(min.X - head.X, 0f), head.X - max.X);
            float dz = MathF.Max(MathF.Max(min.Z - head.Z, 0f), head.Z - max.Z);
            if (dx * dx + dz * dz > maxSq) continue;
            var m = new Vector3(BundleMargin);
            if (!FrustumCuller.IsBoxVisible(in left, min - m, max + m) && !FrustumCuller.IsBoxVisible(in right, min - m, max + m)) continue;
            if (!ReferenceEquals(_slotOwner[col.Slot], col))
            {
                _pipe.SetSectionOrigin(col.Slot, new Vector3(col.Cx * 16, 0f, col.Cz * 16));
                _slotOwner[col.Slot] = col;
            }
            _visible.Add(col);
            if (col.Mesh!.Water.Count > 0)
            {
                float cdx = col.Cx * 16 + 8 - head.X, cdz = col.Cz * 16 + 8 - head.Z;
                _waterOrder.Add((cdx * cdx + cdz * cdz, col));
            }
        }
        // Back to front by a TOTAL order (distance, then slot): ties in any other order would change the bundle's signature and record it again
        _waterOrder.Sort((a, b) => { int c = b.dist.CompareTo(a.dist); return c != 0 ? c : a.col.Slot.CompareTo(b.col.Slot); });
        foreach (var col in _visible)
        {
            var mesh = col.Mesh!;
            foreach (var list in new[] { mesh.Opaque, mesh.Plants })
                if (list.Count > 0 && list.Buffer?.GetGPUBuffer() != null) { _drawCalls++; _quads += list.Count; }
        }
        return _visible.Count;
    }

    private static long Mix(long h, long v) => unchecked((h ^ v) * 1099511628211L);

    private long SolidSignature()
    {
        long h = unchecked((long)14695981039346656037UL);
        foreach (var col in _visible)
        {
            var mesh = col.Mesh!;
            foreach (var list in new[] { mesh.Opaque, mesh.Plants })
            {
                h = Mix(h, col.Slot);
                h = Mix(h, list.FirstQuad);
                h = Mix(h, list.Count);
                h = Mix(h, list.Buffer == null ? 0 : System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(list.Buffer));
            }
        }
        return h;
    }

    private long WaterSignature()
    {
        long h = unchecked((long)14695981039346656037UL);
        foreach (var (_, col) in _waterOrder)
        {
            var list = col.Mesh!.Water;
            h = Mix(h, col.Slot);
            h = Mix(h, list.FirstQuad);
            h = Mix(h, list.Count);
            h = Mix(h, list.Buffer == null ? 0 : System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(list.Buffer));
        }
        return h;
    }

    /// <summary>Opaque blocks, then plants, as one bundle run in the pass (recorded again only when the set of draws or the pipeline's bindings changed).</summary>
    public void DrawSolidBundled(GPURenderPassEncoder pass)
    {
        long sig = SolidSignature();
        if (_solidBundle == null || sig != _solidSignature || _pipe.FrameBindGroupVersion != _solidFrameVersion)
        {
            _solidBundle?.Dispose();
            var recorder = _pipe.BeginBundle();
            foreach (var kind in SolidPasses)
                foreach (var col in _visible)
                {
                    var list = kind == BlockRenderPipeline.Pass.Opaque ? col.Mesh!.Opaque : col.Mesh!.Plants;
                    if (list.Count == 0 || list.Buffer?.GetGPUBuffer() is not { } gpu) continue;
                    _pipe.BindBundle(recorder, kind, gpu);
                    _pipe.DrawInBundle(recorder, list.FirstQuad, list.Count, col.Slot);
                }
            _solidBundle = recorder.Finish();
            _solidSignature = sig;
            _solidFrameVersion = _pipe.FrameBindGroupVersion;
            BundlesRecorded++;
        }
        _pipe.ExecuteBundle(pass, _solidBundle);
    }

    /// <summary>The water, back to front, as one bundle.</summary>
    public void DrawWaterBundled(GPURenderPassEncoder pass)
    {
        if (_waterOrder.Count == 0) return;
        long sig = WaterSignature();
        if (_waterBundle == null || sig != _waterSignature || _pipe.FrameBindGroupVersion != _waterFrameVersion)
        {
            _waterBundle?.Dispose();
            var recorder = _pipe.BeginBundle();
            foreach (var (_, col) in _waterOrder)
            {
                var list = col.Mesh!.Water;
                if (list.Buffer?.GetGPUBuffer() is not { } gpu) continue;
                _pipe.BindBundle(recorder, BlockRenderPipeline.Pass.Water, gpu);
                _pipe.DrawInBundle(recorder, list.FirstQuad, list.Count, col.Slot);
            }
            _waterBundle = recorder.Finish();
            _waterSignature = sig;
            _waterFrameVersion = _pipe.FrameBindGroupVersion;
            BundlesRecorded++;
        }
        _pipe.ExecuteBundle(pass, _waterBundle);
    }

    /// <summary>How many times a bundle has been recorded (it should be rare: only when the draws change).</summary>
    public long BundlesRecorded { get; private set; }

    /// <summary>Opaque blocks, then plants.</summary>
    public void DrawSolid(GPURenderPassEncoder pass)
    {
        foreach (var kind in SolidPasses)
        {
            foreach (var col in _visible)
            {
                var list = kind == BlockRenderPipeline.Pass.Opaque ? col.Mesh!.Opaque : col.Mesh!.Plants;
                if (list.Count == 0 || list.Buffer?.GetGPUBuffer() is not { } gpu) continue;
                _pipe.BindPass(pass, kind, gpu);
                // The whole column in one draw: every quad knows its section
                _pipe.Draw(pass, list.FirstQuad, list.Count, col.Slot);
                _drawCalls++;
                _quads += list.Count;
            }
        }
    }

    /// <summary>Water, farthest column first so nearer water blends over farther water.</summary>
    public void DrawWater(GPURenderPassEncoder pass)
    {
        _waterOrder.Sort((a, b) => b.dist.CompareTo(a.dist));
        foreach (var (_, col) in _waterOrder)
        {
            var list = col.Mesh!.Water;
            if (list.Buffer?.GetGPUBuffer() is not { } gpu) continue;
            _pipe.BindPass(pass, BlockRenderPipeline.Pass.Water, gpu);
            _pipe.Draw(pass, list.FirstQuad, list.Count, col.Slot);
            _drawCalls++;
            _quads += list.Count;
        }
    }

    public TerrainFrameStats Stats => new(_visible.Count, _drawCalls, _quads);

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _solidBundle?.Dispose(); _waterBundle?.Dispose();
        _pipe.InvalidateQuadBuffers();
        _atlas?.Dispose();
        _biomes?.Dispose();
        _pipe.Dispose();
    }
}
