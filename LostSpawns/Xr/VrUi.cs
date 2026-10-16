using System.Numerics;
using SpawnDev.GameUI;
using SpawnDev.GameUI.Elements;
using SpawnDev.SpawnJS.JSObjects;

namespace LostSpawns.Xr;

/// <summary>
/// The panels of the headset view and how they are drawn: after the world, each panel in its own submit (GameUI writes one set of buffers per
/// batch, so two batches in one submit would both draw the last), tested against a depth buffer of its own (GameUI's pipeline is built for
/// <c>depth24plus</c>, the world's is a reversed-Z <c>depth32float</c>) that is cleared to the far plane once, so the panels are always seen.
/// </summary>
public sealed class VrUi : IDisposable
{
    private readonly UIControllerRay _ray = new() { MaxLength = 4f };
    private GPUTexture? _depth;
    private GPUTextureView? _depthView;
    private int _width, _height;

    /// <summary>The panels, drawn in this order (later ones on top).</summary>
    public List<VrPanel> Panels { get; } = new();

    /// <summary>Show the pointing ray (a menu is open).</summary>
    public bool ShowRay { get; set; }
    public Vector3 RayOrigin { get; set; }
    public Vector3 RayDirection { get; set; } = -Vector3.UnitZ;
    /// <summary>Length of the ray's visible part when it meets a panel (metres).</summary>
    public float RayHit { get; set; }
    public bool RayHovering { get; set; }

    /// <summary>The size of the eye's targets: the depth buffer is made to match.</summary>
    public void EnsureTargets(GPUDevice device, GPUQueue queue, int width, int height)
    {
        if (_depth != null && width == _width && height == _height) return;
        _depthView?.Dispose();
        _depth?.Destroy(); _depth?.Dispose();
        _width = width;
        _height = height;
        _depth = device.CreateTexture(new GPUTextureDescriptor
        {
            Size = new[] { width, height },
            Format = "depth24plus",
            Usage = GPUTextureUsage.RenderAttachment,
        });
        _depthView = _depth.CreateView();
        // Never written by the panels, only tested against: the far plane everywhere
        using var encoder = device.CreateCommandEncoder();
        using (var pass = encoder.BeginRenderPass(new GPURenderPassDescriptor
        {
            ColorAttachments = System.Array.Empty<GPURenderPassColorAttachment>(),
            DepthStencilAttachment = new GPURenderPassDepthStencilAttachment
            {
                View = _depthView,
                DepthLoadOp = "clear",
                DepthStoreOp = "store",
                DepthClearValue = 1f,
            },
        }))
        {
            pass.End();
        }
        using var commands = encoder.Finish();
        queue.Submit(new[] { commands });
    }

    /// <summary>Draw every open panel and the ray for one eye, onto <paramref name="colorView"/> (what the world already drew there is kept).</summary>
    public void DrawEye(GPUDevice device, GPUQueue queue, UIRenderer renderer, Matrix4x4 roomViewProjection, GPUTextureView colorView)
    {
        if (_depthView == null) return;
        foreach (var panel in Panels)
        {
            if (!panel.IsOpen) continue;
            using var encoder = device.CreateCommandEncoder();
            panel.Draw(renderer, roomViewProjection, encoder, colorView, _depthView);
            using var commands = encoder.Finish();
            queue.Submit(new[] { commands });
        }
        if (ShowRay)
        {
            using var encoder = device.CreateCommandEncoder();
            _ray.SetRay(RayOrigin, RayDirection);
            _ray.IsHovering = RayHovering;
            _ray.HitDistance = RayHit;
            _ray.DrawWorld(renderer, roomViewProjection, encoder, colorView, _depthView);
            using var commands = encoder.Finish();
            queue.Submit(new[] { commands });
        }
    }

    public void Dispose()
    {
        _depthView?.Dispose();
        _depth?.Destroy(); _depth?.Dispose();
        _depth = null;
        _depthView = null;
    }
}
