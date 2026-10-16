using System.Numerics;

namespace LostSpawns.Services;

/// <summary>
/// Developer URL parameters for looking at the game from a known place, time and weather, which is how a change to the picture is
/// checked: <c>?cam=x,z,yaw,pitch[,up]</c> (up = blocks above the ground there, default 6), <c>?time=0.3</c> (fraction of the day,
/// the clock then stands still), <c>?rain=0.8</c> (the weather is held there). Nothing here changes a normal visit.
/// </summary>
public sealed class DebugView
{
    public float? Time { get; private set; }
    public float? Rain { get; private set; }
    /// <summary>Break this many blocks along the camera ray (nearest first) a moment after loading, to check that an edit re-meshes (<c>?dig=N</c>).</summary>
    public int Dig { get; private set; }
    public (float x, float z, float yaw, float pitch, float up)? Camera { get; private set; }

    public static DebugView Parse(string uri)
    {
        var view = new DebugView();
        int q = uri.IndexOf('?');
        if (q < 0) return view;
        foreach (var part in uri[(q + 1)..].Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            int eq = part.IndexOf('=');
            if (eq <= 0) continue;
            string key = part[..eq], value = Uri.UnescapeDataString(part[(eq + 1)..]);
            switch (key)
            {
                case "time" when TryFloat(value, out var t): view.Time = t; break;
                case "rain" when TryFloat(value, out var r): view.Rain = r; break;
                case "dig" when int.TryParse(value, out var d): view.Dig = d; break;
                case "cam":
                    var f = value.Split(',');
                    if (f.Length >= 4 && TryFloat(f[0], out var x) && TryFloat(f[1], out var z) && TryFloat(f[2], out var yaw) && TryFloat(f[3], out var pitch))
                    {
                        float up = f.Length >= 5 && TryFloat(f[4], out var u) ? u : 6f;
                        view.Camera = (x, z, yaw, pitch, up);
                    }
                    break;
            }
        }
        return view;
    }

    private static bool TryFloat(string s, out float v) => float.TryParse(s, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out v);
}
