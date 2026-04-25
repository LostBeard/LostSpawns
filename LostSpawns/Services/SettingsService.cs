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
    public int DrawDistance { get; private set; } = 12;      // chunks (192m view radius)
    public float FieldOfView { get; private set; } = 70f;    // degrees
    public bool Vsync { get; private set; } = true;

    // Audio
    public float MasterVolume { get; private set; } = 1f;    // [0, 1]

    // Player
    public string PlayerName { get; private set; } = "Survivor";

    public SettingsService(BlazorJSRuntime js)
    {
        _js = js;
        Load();
    }

    private void Load()
    {
        var storedDraw = GetInt("lost.settings.drawDistance", 12);
        DrawDistance = Math.Clamp(storedDraw, 4, 32);
        FieldOfView = GetFloat("lost.settings.fov", 70f);
        Vsync = GetBool("lost.settings.vsync", true);
        MasterVolume = Math.Clamp(GetFloat("lost.settings.volume", 1f), 0f, 1f);
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

    public void SaveVolume(float volume)
    {
        MasterVolume = Math.Clamp(volume, 0f, 1f);
        Set("lost.settings.volume", MasterVolume.ToString("F2"));
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
