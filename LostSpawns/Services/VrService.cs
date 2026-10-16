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

    public VrService(SpawnJSRuntime js) => _js = js;

    public XrSession Session => _session ??= new XrSession(_js);

    /// <summary>A headset session is running.</summary>
    public bool IsActive => _session?.IsActive == true;

    /// <summary>How many columns out from the player the headset view draws (<c>?vrdd=N</c> on the home or game page sets it, for tuning on a headset).</summary>
    public int DrawRadius { get; set; } = VrWorld.DefaultDrawRadius;

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
