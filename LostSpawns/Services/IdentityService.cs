using SpawnDev.BlazorJS;
using SpawnDev.BlazorJS.Cryptography;
using SpawnDev.BlazorJS.JSObjects;

namespace LostSpawns.Services;

/// <summary>
/// Manages the player's persistent Ed25519 identity, stored in IndexedDB.
/// On first run, generates the keypair. On subsequent runs, reloads it.
/// The public key (SPKI bytes, base64-encoded) IS the PlayerId - verifiable by peers.
/// PlayerName is a separate display string (mutable, stored in localStorage).
/// </summary>
public class IdentityService : IAsyncDisposable
{
    private const string DB_NAME = "LostIdentity";
    private const string STORE_NAME = "keys";
    private const string KEY_PUBLIC = "ed25519.spki";
    private const string KEY_PRIVATE = "ed25519.pkcs8";

    private readonly BlazorJSRuntime _js;
    private readonly IPortableCrypto _crypto;
    private PortableEd25519Key? _key;
    private byte[] _publicKeySpki = [];

    public string? PlayerId { get; private set; }
    public byte[] PublicKeySpki => _publicKeySpki;
    public string PlayerName { get; private set; } = "Survivor";
    public bool IsInitialized { get; private set; }

    public IdentityService(BlazorJSRuntime js, IPortableCrypto crypto)
    {
        _js = js;
        _crypto = crypto;
    }

    public async Task InitAsync()
    {
        if (IsInitialized) return;

        using var db = await IDBDatabase.OpenAsync(DB_NAME, 1, e =>
        {
            using var req = e.Target;
            using var dbRef = req.Result;
            if (!dbRef.ObjectStoreNames.Contains(STORE_NAME))
            {
                using var _ = dbRef.CreateObjectStore<string, byte[]>(STORE_NAME);
            }
        });

        byte[]? spki = null;
        byte[]? pkcs8 = null;
        using (var tx = db.Transaction(STORE_NAME, readWrite: false))
        {
            using var store = tx.ObjectStore<string, byte[]>(STORE_NAME);
            spki = await TryGetAsync(store, KEY_PUBLIC);
            pkcs8 = await TryGetAsync(store, KEY_PRIVATE);
        }

        if (spki != null && pkcs8 != null)
        {
            _key = await _crypto.ImportEd25519Key(spki, pkcs8, extractable: true);
        }
        else
        {
            _key = await _crypto.GenerateEd25519Key(extractable: true);
            spki = await _crypto.ExportPublicKeySpki(_key);
            pkcs8 = await _crypto.ExportPrivateKeyPkcs8(_key);

            using var tx = db.Transaction(STORE_NAME, readWrite: true);
            using var store = tx.ObjectStore<string, byte[]>(STORE_NAME);
            await store.PutAsync(spki, KEY_PUBLIC);
            await store.PutAsync(pkcs8, KEY_PRIVATE);
        }

        _publicKeySpki = spki;
        PlayerId = Convert.ToBase64String(spki);

        using var storage = _js.Get<Storage>("localStorage");
        var name = storage.GetItem("lost.playerName");
        if (!string.IsNullOrEmpty(name)) PlayerName = name;

        IsInitialized = true;
    }

    private static async Task<byte[]?> TryGetAsync(IDBObjectStore<string, byte[]> store, string key)
    {
        try { return await store.GetAsync(key); }
        catch { return null; }
    }

    public void SetPlayerName(string name)
    {
        PlayerName = name;
        using var storage = _js.Get<Storage>("localStorage");
        storage.SetItem("lost.playerName", name);
    }

    /// <summary>Sign data with the player's Ed25519 private key. Throws if InitAsync hasn't completed.</summary>
    public Task<byte[]> SignAsync(byte[] data)
    {
        if (_key == null) throw new InvalidOperationException("InitAsync not completed");
        return _crypto.Sign(_key, data);
    }

    /// <summary>Verify a peer's Ed25519 signature given their SPKI public key.</summary>
    public async Task<bool> VerifyPeerSignatureAsync(byte[] peerPublicKeySpki, byte[] data, byte[] signature)
    {
        try
        {
            using var peerKey = await _crypto.ImportEd25519Key(peerPublicKeySpki, extractable: false);
            return await _crypto.Verify(peerKey, data, signature);
        }
        catch
        {
            return false;
        }
    }

    public ValueTask DisposeAsync()
    {
        _key?.Dispose();
        return ValueTask.CompletedTask;
    }
}
