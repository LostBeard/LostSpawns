using SpawnDev.BlazorJS;
using SpawnDev.BlazorJS.JSObjects;

namespace LostSpawns.Services;

/// <summary>
/// Manages game settings, persisted to localStorage.
/// </summary>
public class SettingsService
{
    private readonly BlazorJSRuntime _js;

    // Video
    public int DrawDistance { get; private set; } = 400;     // chunks (~6.4km view)
    public float FieldOfView { get; private set; } = 70f;    // degrees
    public bool Vsync { get; private set; } = true;

    // Player
    public string PlayerName { get; private set; } = "Survivor";

    public SettingsService(BlazorJSRuntime js)
    {
        _js = js;
        Load();
    }

    private void Load()
    {
        // Force minimum draw distance - clear stale localStorage values
        var storedDraw = GetInt("lost.settings.drawDistance", 60);
        DrawDistance = Math.Max(storedDraw, 60);
        FieldOfView = GetFloat("lost.settings.fov", 70f);
        Vsync = GetBool("lost.settings.vsync", true);
        PlayerName = GetString("lost.settings.playerName", "Survivor");
    }

    public void SaveVideo(int drawDistance, float fov, bool vsync)
    {
        DrawDistance = drawDistance; FieldOfView = fov; Vsync = vsync;
        Set("lost.settings.drawDistance", drawDistance.ToString());
        Set("lost.settings.fov", fov.ToString("F2"));
        Set("lost.settings.vsync", vsync ? "1" : "0");
    }

    public void SavePlayerName(string name)
    {
        PlayerName = name;
        Set("lost.settings.playerName", name);
    }

    private string GetString(string key, string def)
    {
        using var storage = _js.Get<Storage>("localStorage");
        var v = storage.GetItem(key);
        return string.IsNullOrEmpty(v) ? def : v;
    }

    private int GetInt(string key, int def) => int.TryParse(GetString(key, ""), out var v) ? v : def;
    private float GetFloat(string key, float def) => float.TryParse(GetString(key, ""), out var v) ? v : def;
    private bool GetBool(string key, bool def) => GetString(key, def ? "1" : "0") == "1";

    private void Set(string key, string value)
    {
        using var storage = _js.Get<Storage>("localStorage");
        storage.SetItem(key, value);
    }
}
