using System.Numerics;
using LostSpawns.Services;
using SpawnDev.SpawnJS.JSObjects;
using SpawnDev.VoxelEngine.Rendering;

namespace LostSpawns.Rendering;

/// <summary>
/// Rain over the world, drawn last in the pass: a streak for each column of a window round the camera, a vertical quad that turns to
/// face the camera and falls from the sky to the first thing that roofs the column (a tree crown, a roof, the ground), so it does not
/// rain under a tree. The window is rebuilt from the world when the camera moves into another column.
/// </summary>
public sealed class WeatherRenderer : IDisposable
{
    /// <summary>The window's side in columns: the streaks reach 12 blocks from the camera.</summary>
    public const int Side = 24;
    /// <summary>The ground the shader counts from sits this far below world y 0 (heights are stored unsigned).</summary>
    private const int HeightBias = 64;

    private readonly WeatherPipeline _pipe = new();
    private readonly uint[] _cells = new uint[Side * Side];
    private int _x0 = int.MinValue, _z0 = int.MinValue;
    private double _lastBuild = double.NegativeInfinity;
    private bool _ready;
    private bool _disposed;
    private readonly System.Diagnostics.Stopwatch _clock = System.Diagnostics.Stopwatch.StartNew();

    public void Init(GPUDevice device, GPUQueue queue, string colorFormat, EntityTextureAtlas atlas)
    {
        _pipe.Init(device, queue, colorFormat);
        _pipe.SetTextures(atlas);
        _ready = true;
    }

    private static Vector4 Rect(int[] r) => new(r[0], r[1], r[2], r[3]);

    /// <param name="rain">How hard it rains, 0..1.</param>
    /// <param name="daylight">How bright the day is, 0..1 (rain is dark at night).</param>
    public void Draw(GPURenderPassEncoder pass, WorldService world, in Matrix4x4 reversedViewProjection, Vector3 cameraPos, float rain, float daylight)
    {
        if (!_ready || _disposed || rain < 0.02f) return;

        int x0 = (int)MathF.Floor(cameraPos.X) - Side / 2;
        int z0 = (int)MathF.Floor(cameraPos.Z) - Side / 2;
        double now = _clock.Elapsed.TotalSeconds;
        // Rebuilt when the camera enters another column, and twice a second (a tree was felled, the chunks arrived)
        if (x0 != _x0 || z0 != _z0 || now - _lastBuild > 0.5)
        {
            for (int dz = 0; dz < Side; dz++)
            for (int dx = 0; dx < Side; dx++)
            {
                int top = world.RainHeightAt(x0 + dx, z0 + dz);
                _cells[dz * Side + dx] = top < 0 ? 0u : ((WeatherPipeline.KindRain << 16) | (uint)(top + HeightBias));
            }
            _pipe.SetCells(_cells, Side);
            _x0 = x0;
            _z0 = z0;
            _lastBuild = now;
        }

        // The viewer's own clock, so the rain keeps falling while the game's time is frozen
        double ticks = now * 20.0 % 100000.0;
        var uniforms = new WeatherUniforms
        {
            ViewProjection = reversedViewProjection,
            Camera = new Vector4(cameraPos, (float)ticks),
            Params = new Vector4(Math.Clamp(rain, 0f, 1f), 0.35f + 0.65f * Math.Clamp(daylight, 0f, 1f), Side, 12f),
            Window = new Vector4(x0, z0, 0f, 0f),
            RainRect = Rect(SkyTextures.Rain),
            SnowRect = Rect(SkyTextures.Snow),
        };
        _pipe.Draw(pass, in uniforms);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _pipe.Dispose();
    }
}
