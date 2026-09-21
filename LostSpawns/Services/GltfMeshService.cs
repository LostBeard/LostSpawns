using LostSpawns.Content;
using SpawnDev.SpawnJS;
using SpawnDev.SpawnJS.JSObjects;

namespace LostSpawns.Services;

/// <summary>
/// Fetches CC0 GLB meshes into JS ArrayBuffers (off the .NET heap), then
/// uploads POSITION/NORMAL/index views to WebGPU for entity drawing.
/// </summary>
public sealed class GltfMeshService : IDisposable
{
    private readonly SpawnJSRuntime _js;
    private readonly Dictionary<string, LoadedGltf> _cache = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, GpuEntityMesh> _gpuMeshes = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _failed = new(StringComparer.OrdinalIgnoreCase);
    private bool _disposed;

    public GltfMeshService(SpawnJSRuntime js) => _js = js;

    /// <summary>Number of GLBs currently cached in JS heap.</summary>
    public int CachedCount => _cache.Count;

    /// <summary>Number of meshes uploaded to GPU.</summary>
    public int GpuMeshCount => _gpuMeshes.Count;

    public bool IsLoaded(string id) => _cache.ContainsKey(id);

    public bool TryGet(string id, out LoadedGltf mesh) => _cache.TryGetValue(id, out mesh!);

    public bool TryGetGpuMesh(string id, out GpuEntityMesh mesh) => _gpuMeshes.TryGetValue(id, out mesh!);

    /// <summary>True when a GPU mesh is ready for this wildlife kind.</summary>
    public bool HasGpuMeshForKind(EntityKind kind)
    {
        if (!MeshCatalog.TryGetWildlife(kind, out var entry) || string.IsNullOrEmpty(entry.Path))
            return false;
        return _gpuMeshes.ContainsKey(entry.Id);
    }

    public GpuEntityMesh? ResolveWildlifeMesh(EntityKind kind)
    {
        if (!MeshCatalog.TryGetWildlife(kind, out var entry) || string.IsNullOrEmpty(entry.Path))
            return null;
        return _gpuMeshes.TryGetValue(entry.Id, out var m) ? m : null;
    }

    /// <summary>
    /// Preload every MeshCatalog entry that has a Path. Missing files log and
    /// continue (tools are optional drop-ins). Safe to call once at world init.
    /// </summary>
    public async Task PreloadCatalogAsync()
    {
        foreach (var entry in MeshCatalog.PreloadCandidates())
            await LoadAsync(entry);
    }

    /// <summary>
    /// Upload every cached GLB into GPU buffers. Call after RenderService.Init
    /// so device/queue exist. Idempotent - skips ids already on GPU.
    /// </summary>
    public void UploadCachedToGpu(GPUDevice device, GPUQueue queue)
    {
        foreach (var loaded in _cache.Values)
        {
            if (_gpuMeshes.ContainsKey(loaded.Id)) continue;
            try
            {
                var mesh = GlbGpuUploader.TryUpload(
                    device, queue, new LoadedGltfSource(loaded.Id, loaded.Buffer));
                if (mesh != null)
                    _gpuMeshes[loaded.Id] = mesh;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Gltf] GPU upload {loaded.Id} failed: {ex.Message}");
            }
        }
    }

    public async Task<LoadedGltf?> LoadAsync(MeshCatalogEntry entry)
    {
        if (string.IsNullOrEmpty(entry.Path)) return null;
        if (_cache.TryGetValue(entry.Id, out var hit)) return hit;
        if (_failed.Contains(entry.Id)) return null;

        try
        {
            using var response = await _js.CallAsync<string, Response>("fetch", entry.Path);
            if (!response.Ok)
            {
                Console.WriteLine($"[Gltf] {entry.Id}: HTTP {(int)response.Status} for {entry.Path}");
                _failed.Add(entry.Id);
                return null;
            }

            var buffer = await response.ArrayBuffer();
            long byteLen = buffer.ByteLength;
            if (byteLen < 12)
            {
                buffer.Dispose();
                Console.WriteLine($"[Gltf] {entry.Id}: too small ({byteLen} bytes)");
                _failed.Add(entry.Id);
                return null;
            }

            using var headerSlice = buffer.Slice(0, 12);
            var header = headerSlice.ReadBytes();
            uint magic = BitConverter.ToUInt32(header, 0);
            uint version = BitConverter.ToUInt32(header, 4);
            uint declared = BitConverter.ToUInt32(header, 8);
            if (magic != 0x46546C67 || version != 2)
            {
                buffer.Dispose();
                Console.WriteLine($"[Gltf] {entry.Id}: bad GLB header magic=0x{magic:X8} ver={version}");
                _failed.Add(entry.Id);
                return null;
            }

            var loaded = new LoadedGltf(entry.Id, entry.Path!, buffer, byteLen, declared);
            _cache[entry.Id] = loaded;
            Console.WriteLine($"[Gltf] loaded {entry.Id} ({byteLen:N0} bytes, declared {declared:N0}) from {entry.Path}");
            return loaded;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Gltf] {entry.Id} failed: {ex.Message}");
            _failed.Add(entry.Id);
            return null;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        foreach (var m in _gpuMeshes.Values)
            m.Dispose();
        _gpuMeshes.Clear();
        foreach (var m in _cache.Values)
            m.Dispose();
        _cache.Clear();
    }
}

/// <summary>
/// A GLB whose bytes live in a JS ArrayBuffer. Call Dispose to free the JS slot.
/// Do not ReadBytes() the full buffer into .NET - GPU upload must take the JS view.
/// </summary>
public sealed class LoadedGltf : IDisposable
{
    public string Id { get; }
    public string Path { get; }
    public ArrayBuffer Buffer { get; }
    public long ByteLength { get; }
    public uint DeclaredLength { get; }

    private bool _disposed;

    public LoadedGltf(string id, string path, ArrayBuffer buffer, long byteLength, uint declaredLength)
    {
        Id = id;
        Path = path;
        Buffer = buffer;
        ByteLength = byteLength;
        DeclaredLength = declaredLength;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Buffer.Dispose();
    }
}
