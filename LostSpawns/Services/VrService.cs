using LostSpawns.Xr;
using SpawnDev.SpawnJS;

namespace LostSpawns.Services;

/// <summary>
/// The headset session, kept across pages: a click on the home page starts the session (a session must be requested from a click, and the world takes
/// longer to load than a click's permission lasts), the game page picks it up once the world is ready, and the pause menu can start one in the middle
/// of a game. The session ends when the headset says so.
/// </summary>
public sealed class VrService : IDisposable
{
    private readonly SpawnJSRuntime _js;
    private XrSession? _session;

    public VrService(SpawnJSRuntime js)
    {
        _js = js;
        LoadOptions();
    }

    /// <summary>What the player chose for the headset (the VR menu's Settings page); kept in localStorage.</summary>
    public sealed class VrOptions
    {
        public bool SmoothTurn { get; set; } = true;
        public float TurnDegreesPerSecond { get; set; } = 110f;
        public float SnapDegrees { get; set; } = 30f;
        /// <summary>Darken the edges of the view while moving or turning.</summary>
        public bool ComfortVignette { get; set; }
        public int DrawRadius { get; set; } = VrWorld.DefaultDrawRadius;
        /// <summary>The share of the eye resolution the world is drawn at.</summary>
        public float RenderScale { get; set; } = 0.75f;
        /// <summary>The forearm's tilt up from the controller (degrees).</summary>
        public float ForearmTilt { get; set; } = 35f;
    }

    public VrOptions Options { get; private set; } = new();
    private const string OptionsKey = "lost.vr3";   // (lost.vr could hold the step turning an accidental stick click had switched on)

    private void LoadOptions()
    {
        try
        {
            using var storage = _js.Get<SpawnDev.SpawnJS.JSObjects.Storage>("localStorage");
            var raw = storage.GetItem(OptionsKey);
            if (!string.IsNullOrEmpty(raw)) Options = System.Text.Json.JsonSerializer.Deserialize<VrOptions>(raw) ?? new VrOptions();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Vr] options not read: {ex.Message}");
        }
        DrawRadius = Math.Clamp(Options.DrawRadius, VrWorld.MinDrawRadius, 10);
    }

    public void SaveOptions()
    {
        try
        {
            using var storage = _js.Get<SpawnDev.SpawnJS.JSObjects.Storage>("localStorage");
            storage.SetItem(OptionsKey, System.Text.Json.JsonSerializer.Serialize(Options));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Vr] options not saved: {ex.Message}");
        }
    }

    public XrSession Session => _session ??= new XrSession(_js);

    /// <summary>A headset session is running.</summary>
    public bool IsActive => _session?.IsActive == true;

    /// <summary>How many columns out from the player the headset view draws (<c>?vrdd=N</c> on the home or game page sets it, for tuning on a headset).</summary>
    public int DrawRadius { get; set; } = VrWorld.DefaultDrawRadius;
    /// <summary>The radius follows the headset's frame rate (off when <c>?vrdd</c> chose it).</summary>
    public bool AdaptiveDrawRadius { get; set; } = true;

    /// <summary>Why the last <see cref="BeginAsync"/> failed.</summary>
    public string? LastError { get; private set; }

    public Task<bool> IsSupportedAsync() => Session.IsVrSupportedAsync();

    /// <summary>Start a session. Call it straight from the click that asks for it, before any other await.</summary>
    public async Task<bool> BeginAsync()
    {
        if (IsActive) return true;
        LastError = null;
        try
        {
            await Session.EnterAsync("immersive-vr");
            return true;
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            Console.WriteLine($"[Vr] could not enter: {ex.Message}");
            return false;
        }
    }

    public void Dispose() => _session?.Dispose();
}
