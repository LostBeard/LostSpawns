using System.Numerics;
using SpawnDev.SpawnJS.JSObjects;
using SpawnDev.VoxelEngine.Rendering;

namespace LostSpawns.Rendering;

/// <summary>
/// The sky behind the world: the gradient from the horizon's colour (the fog's) to the zenith's, a glow round the sun, the sun and
/// the moon in its phase, stars at night, and a layer of drifting clouds. Drawn first in the world's render pass, with no depth, so
/// the terrain, creatures and water draw over it.
/// </summary>
public sealed class SkyRenderer : IDisposable
{
    /// <summary>Where the clouds float, in blocks.</summary>
    public const float CloudHeight = 150f;
    /// <summary>How fast they drift, blocks a second.</summary>
    public const float CloudBlocksPerSecond = 3.2f;

    private readonly SkyPipeline _pipe = new();
    private EntityTextureAtlas? _atlas;
    private bool _disposed;
    private readonly System.Diagnostics.Stopwatch _clock = System.Diagnostics.Stopwatch.StartNew();

    /// <summary>The sky's atlas (sun, moon, clouds, rain, snow): the weather pipeline reads the same one.</summary>
    public EntityTextureAtlas? Atlas => _atlas;

    public void Init(GPUDevice device, GPUQueue queue, string colorFormat)
    {
        _pipe.Init(device, queue, colorFormat);
        _atlas = EntityTextureAtlas.FromRgba(device, queue, SkyTextures.Build(), SkyTextures.Width, SkyTextures.Height);
        _pipe.SetTextures(_atlas);
    }

    private static Vector4 Rect(int[] r) => new(r[0], r[1], r[2], r[3]);

    /// <param name="reversedViewProjection">The camera's view * projection with the reversed-Z infinite projection the pipelines use.</param>
    /// <param name="moonPhase">0 (full) .. 7, as many days as the game has run.</param>
    public void Draw(GPURenderPassEncoder pass, in Matrix4x4 reversedViewProjection, Vector3 cameraPos, in Atmosphere sky, float rain, int moonPhase, float dayFraction)
    {
        if (_disposed || !_pipe.IsReady) return;
        Matrix4x4.Invert(reversedViewProjection, out var inverse);
        float angle = Atmosphere.SkyTime(dayFraction) * MathF.Tau;
        double seconds = _clock.Elapsed.TotalSeconds;
        var uniforms = new SkyUniforms
        {
            InverseViewProjection = inverse,
            Camera = new Vector4(cameraPos, 1f),
            SunDirection = new Vector4(sky.SunDirection, sky.Daylight),
            Zenith = new Vector4(sky.Zenith, moonPhase & 7),
            Horizon = new Vector4(sky.Fog, sky.Night),
            Cloud = new Vector4(CloudHeight, (float)(seconds * CloudBlocksPerSecond % (12.0 * SkyTextures.Clouds[2])), Math.Clamp(rain, 0f, 1f), -angle),
            CloudColor = new Vector4(sky.CloudColor, 0.88f),
            SunRect = Rect(SkyTextures.Sun),
            MoonRect = Rect(SkyTextures.Moon),
            CloudRect = Rect(SkyTextures.Clouds),
        };
        _pipe.Draw(pass, in uniforms);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _pipe.Dispose();
        _atlas?.Dispose();
    }
}
