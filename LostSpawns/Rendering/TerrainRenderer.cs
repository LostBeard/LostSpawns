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

    public void Init(GPUDevice device, GPUQueue queue, string colorFormat)
    {
        _pipe.Init(device, queue, colorFormat, WorldService.MaxColumnSlots);

        _biomes = BiomeTintMap.Create(device, queue);
        BiomeColours.Fill(_biomes);
        _pipe.SetBiomeTints(_biomes);

        _atlas = BlockTextureAtlas.FromRgba(device, queue, BlockTextures.Build(), BlockTextures.AtlasWidth, BlockTextures.AtlasHeight, BlockTextures.TileSize);
        _pipe.SetTextures(_atlas);
        _pipe.SetAnimations(BlockStyle.Animations());
        _pipe.SetMaterials(BlockStyle.Materials());
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
        _pipe.InvalidateQuadBuffers();
        _atlas?.Dispose();
        _biomes?.Dispose();
        _pipe.Dispose();
    }
}
