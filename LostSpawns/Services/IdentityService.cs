using SpawnDev.BlazorJS;
using SpawnDev.BlazorJS.JSObjects;

namespace LostSpawns.Services;

/// <summary>
/// Manages ECDSA and ECDH identity keys, persisted in IndexedDB.
/// On first run, generates keys and saves them. On subsequent runs, reloads them.
/// </summary>
public class IdentityService : IAsyncDisposable
{
    private readonly BlazorJSRuntime _js;

    public string? PlayerId { get; private set; }
    public string? PlayerName { get; private set; } = "Survivor";
    public bool IsInitialized { get; private set; }

    public IdentityService(BlazorJSRuntime js)
    {
        _js = js;
    }

    public Task InitAsync()
    {
        if (IsInitialized) return Task.CompletedTask;

        // TODO: Open IndexedDB "LostIdentityDB", read or generate ECDSA + ECDH key pairs.
        // For now, generate a stable random player ID stored in localStorage.
        using var storage = _js.Get<Storage>("localStorage");
        var stored = storage.GetItem("lost.playerId");
        if (string.IsNullOrEmpty(stored))
        {
            stored = Guid.NewGuid().ToString("N");
            storage.SetItem("lost.playerId", stored);
        }
        PlayerId = stored;

        var name = storage.GetItem("lost.playerName");
        if (!string.IsNullOrEmpty(name)) PlayerName = name;

        IsInitialized = true;
        return Task.CompletedTask;
    }

    public void SetPlayerName(string name)
    {
        PlayerName = name;
        using var storage = _js.Get<Storage>("localStorage");
        storage.SetItem("lost.playerName", name);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
