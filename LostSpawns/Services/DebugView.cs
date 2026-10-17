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
    /// <summary>The player cannot be hurt (<c>?god=1</c>).</summary>
    public bool God { get; private set; }
    /// <summary>The wildlife stands still (<c>?still=1</c>).</summary>
    public bool Still { get; private set; }
    /// <summary>Show the creature name tags and health bars (<c>?tags=1</c>): the admin view; off in the game.</summary>
    public bool Tags { get; private set; }
    /// <summary>Switch the sky and block light off (<c>?light=0</c>), to compare pictures.</summary>
    public bool? Light { get; private set; }
    /// <summary>Drop a can of beans on the ground 2.5 blocks ahead of the camera (<c>?loot=1</c>), to pick up.</summary>
    public bool Loot { get; private set; }
    /// <summary>Lay the bedroll 6 blocks ahead of the camera (<c>?bedroll=1</c>).</summary>
    public bool Bedroll { get; private set; }
    /// <summary>Force the ambient occlusion on or off whatever the setting says (<c>?ao=0</c>, <c>?ao=1</c>), for comparing pictures.</summary>
    public bool? Occlusion { get; private set; }
    /// <summary>The headset's draw distance in columns (<c>?vrdd=5</c>).</summary>
    public int? VrDrawRadius { get; private set; }
    /// <summary>Creatures to place in a row a few blocks ahead of the camera, standing still (<c>?spawn=wolf,deer,rabbit</c>).</summary>
    public List<EntityKind> Spawn { get; } = new();
    /// <summary>How far ahead of the camera the <c>?spawn</c> row is placed (<c>?spawndist=3</c>), default 7.</summary>
    public float SpawnDistance { get; private set; } = 7f;
    /// <summary>Stand 14 blocks from the Nth nearest building, looking at it (<c>?poi=1</c>); <c>?poiyaw=</c> turns the view round it.</summary>
    public int Poi { get; private set; }
    /// <summary>Open the crafting screen once loaded (<c>?menu=craft</c>), to look at it.</summary>
    public bool CraftingMenu { get; private set; }
    public float PoiYaw { get; private set; }
    /// <summary>Only buildings of this kind count for <c>?poi=N</c> (<c>?poikind=well</c>).</summary>
    public string? PoiKind { get; private set; }
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
                case "god": view.God = value != "0"; break;
                case "bedroll": view.Bedroll = value != "0"; break;
                case "loot": view.Loot = value != "0"; break;
                case "light": view.Light = value != "0"; break;
                case "tags": view.Tags = value != "0"; break;
                case "still": view.Still = value != "0"; break;
                case "menu": view.CraftingMenu = value == "craft"; break;
                case "poi" when int.TryParse(value, out var pn): view.Poi = pn; break;
                case "poikind": view.PoiKind = value; break;
                case "poiyaw" when TryFloat(value, out var py): view.PoiYaw = py; break;
                case "spawndist" when TryFloat(value, out var sd): view.SpawnDistance = sd; break;
                case "ao": view.Occlusion = value != "0"; break;
                case "vrdd" when int.TryParse(value, out var vd): view.VrDrawRadius = Math.Clamp(vd, 2, 16); break;
                case "spawn":
                    foreach (var name in value.Split(',', StringSplitOptions.RemoveEmptyEntries))
                        if (Enum.TryParse<EntityKind>(name, ignoreCase: true, out var kind)) view.Spawn.Add(kind);
                    break;
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
