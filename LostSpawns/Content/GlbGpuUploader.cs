using System.Numerics;
using System.Text.Json;
using SpawnDev.SpawnJS.JSObjects;

namespace LostSpawns.Content;

/// <summary>
/// Uploads the first mesh primitive from a GLB (POSITION + NORMAL + indices)
/// into WebGPU buffers. Vertex/index bytes stay in JS TypedArray views of the
/// source ArrayBuffer - only the JSON chunk crosses into managed memory.
/// </summary>
public static class GlbGpuUploader
{
    private const uint ChunkJson = 0x4E4F534A;
    private const uint ChunkBin = 0x004E4942;
    private const int ComponentFloat = 5126;
    private const int ComponentUShort = 5123;
    private const int ComponentUInt = 5125;

    public static GpuEntityMesh? TryUpload(GPUDevice device, GPUQueue queue, LoadedGltfSource source)
    {
        if (!TryReadJsonAndBin(source.Buffer, out var jsonBytes, out long binByteOffset, out long binByteLength))
            return null;

        using var doc = JsonDocument.Parse(jsonBytes);
        var root = doc.RootElement;
        if (!root.TryGetProperty("meshes", out var meshes) || meshes.GetArrayLength() == 0)
        {
            Console.WriteLine($"[Gltf] {source.Id}: no meshes");
            return null;
        }

        var prim = meshes[0].GetProperty("primitives")[0];
        var attrs = prim.GetProperty("attributes");
        if (!attrs.TryGetProperty("POSITION", out var posAccIdxEl))
        {
            Console.WriteLine($"[Gltf] {source.Id}: no POSITION");
            return null;
        }

        var accessors = root.GetProperty("accessors");
        var views = root.GetProperty("bufferViews");

        int posAccIdx = posAccIdxEl.GetInt32();
        var posAcc = accessors[posAccIdx];
        if (posAcc.GetProperty("componentType").GetInt32() != ComponentFloat
            || posAcc.GetProperty("type").GetString() != "VEC3")
        {
            Console.WriteLine($"[Gltf] {source.Id}: POSITION must be float VEC3");
            return null;
        }

        int vertexCount = posAcc.GetProperty("count").GetInt32();
        var (posViewOff, posViewLen, posStride) = ResolveView(posAcc, views, 12);
        if (posViewOff + posViewLen > binByteLength)
        {
            Console.WriteLine($"[Gltf] {source.Id}: POSITION view out of range");
            return null;
        }

        GPUBuffer? normalBuffer = null;
        int normalCount = 0;
        if (attrs.TryGetProperty("NORMAL", out var nrmAccIdxEl))
        {
            var nrmAcc = accessors[nrmAccIdxEl.GetInt32()];
            if (nrmAcc.GetProperty("componentType").GetInt32() == ComponentFloat
                && nrmAcc.GetProperty("type").GetString() == "VEC3")
            {
                normalCount = nrmAcc.GetProperty("count").GetInt32();
                var (nOff, nLen, nStride) = ResolveView(nrmAcc, views, 12);
                if (nOff + nLen <= binByteLength && normalCount == vertexCount)
                {
                    normalBuffer = UploadView(device, queue, source.Buffer, binByteOffset + nOff, nLen,
                        GPUBufferUsage.Vertex | GPUBufferUsage.CopyDst);
                }
            }
        }

        if (normalBuffer == null)
        {
            // Flat normals: upload a zero-filled buffer of the same size so the
            // shader still binds location 1. Lighting falls back to a constant.
            ulong nBytes = (ulong)(vertexCount * 12);
            normalBuffer = device.CreateBuffer(new GPUBufferDescriptor
            {
                Size = nBytes,
                Usage = GPUBufferUsage.Vertex | GPUBufferUsage.CopyDst,
                MappedAtCreation = false,
            });
            // Leave zeros - shader treats near-zero normals as up.
        }

        var posBuffer = UploadView(device, queue, source.Buffer, binByteOffset + posViewOff, posViewLen,
            GPUBufferUsage.Vertex | GPUBufferUsage.CopyDst);

        if (!prim.TryGetProperty("indices", out var idxAccEl))
        {
            posBuffer.Destroy(); posBuffer.Dispose();
            normalBuffer.Destroy(); normalBuffer.Dispose();
            Console.WriteLine($"[Gltf] {source.Id}: non-indexed meshes not supported yet");
            return null;
        }

        var idxAcc = accessors[idxAccEl.GetInt32()];
        int indexCount = idxAcc.GetProperty("count").GetInt32();
        int idxComp = idxAcc.GetProperty("componentType").GetInt32();
        GPUIndexFormat indexFormat;
        int idxElemSize;
        if (idxComp == ComponentUShort) { indexFormat = GPUIndexFormat.Uint16; idxElemSize = 2; }
        else if (idxComp == ComponentUInt) { indexFormat = GPUIndexFormat.Uint32; idxElemSize = 4; }
        else
        {
            posBuffer.Destroy(); posBuffer.Dispose();
            normalBuffer.Destroy(); normalBuffer.Dispose();
            Console.WriteLine($"[Gltf] {source.Id}: unsupported index componentType {idxComp}");
            return null;
        }

        var (iOff, iLen, _) = ResolveView(idxAcc, views, idxElemSize);
        // WebGPU requires index buffer size multiple of 4.
        ulong paddedIndexBytes = (ulong)((iLen + 3) & ~3);
        var indexBuffer = device.CreateBuffer(new GPUBufferDescriptor
        {
            Size = paddedIndexBytes,
            Usage = GPUBufferUsage.Index | GPUBufferUsage.CopyDst,
        });
        using (var idxView = new Uint8Array(source.Buffer, binByteOffset + iOff, iLen))
            queue.WriteBuffer(indexBuffer, 0, idxView);

        Vector3 bMin = Vector3.Zero, bMax = Vector3.One;
        if (posAcc.TryGetProperty("min", out var minEl) && posAcc.TryGetProperty("max", out var maxEl))
        {
            bMin = new Vector3(minEl[0].GetSingle(), minEl[1].GetSingle(), minEl[2].GetSingle());
            bMax = new Vector3(maxEl[0].GetSingle(), maxEl[1].GetSingle(), maxEl[2].GetSingle());
        }

        bool hasWalk = false, hasAttack = false, hasIdle = false;
        if (root.TryGetProperty("animations", out var anims))
        {
            foreach (var a in anims.EnumerateArray())
            {
                var name = a.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
                if (name.Equals("Walk", StringComparison.OrdinalIgnoreCase)) hasWalk = true;
                else if (name.Equals("Attack", StringComparison.OrdinalIgnoreCase)) hasAttack = true;
                else if (name.StartsWith("Idle", StringComparison.OrdinalIgnoreCase)) hasIdle = true;
            }
        }

        Console.WriteLine(
            $"[Gltf] GPU mesh {source.Id}: verts={vertexCount} idx={indexCount} " +
            $"stridePos={posStride} anims walk={hasWalk} attack={hasAttack} idle={hasIdle}");

        return new GpuEntityMesh(
            source.Id,
            posBuffer,
            normalBuffer,
            indexBuffer,
            indexCount,
            indexFormat,
            vertexCount,
            bMin,
            bMax,
            hasWalk,
            hasAttack,
            hasIdle);
    }

    private static (long offset, int length, int stride) ResolveView(
        JsonElement accessor, JsonElement views, int defaultStride)
    {
        int viewIdx = accessor.GetProperty("bufferView").GetInt32();
        var view = views[viewIdx];
        long viewOff = view.GetProperty("byteOffset").GetInt64();
        int viewLen = view.GetProperty("byteLength").GetInt32();
        long accOff = accessor.TryGetProperty("byteOffset", out var ao) ? ao.GetInt64() : 0;
        int stride = view.TryGetProperty("byteStride", out var bs) ? bs.GetInt32() : defaultStride;
        // Accessor byteOffset is within the view; upload the whole view for simplicity
        // when tightly packed (byteOffset 0). If accessor has offset, slice from there.
        return (viewOff + accOff, viewLen - (int)accOff, stride);
    }

    private static GPUBuffer UploadView(
        GPUDevice device, GPUQueue queue, ArrayBuffer glbBuffer,
        long byteOffset, int byteLength, GPUBufferUsage usage)
    {
        ulong size = (ulong)((byteLength + 3) & ~3);
        var gpu = device.CreateBuffer(new GPUBufferDescriptor
        {
            Size = size,
            Usage = usage,
        });
        using var view = new Uint8Array(glbBuffer, byteOffset, byteLength);
        queue.WriteBuffer(gpu, 0, view);
        return gpu;
    }

    private static bool TryReadJsonAndBin(
        ArrayBuffer buffer, out byte[] jsonBytes, out long binByteOffset, out long binByteLength)
    {
        jsonBytes = System.Array.Empty<byte>();
        binByteOffset = 0;
        binByteLength = 0;

        long total = buffer.ByteLength;
        if (total < 20) return false;

        using (var hdr = new Uint8Array(buffer, 0, 12))
        {
            var h = hdr.ReadBytes();
            if (BitConverter.ToUInt32(h, 0) != 0x46546C67) return false;
        }

        long pos = 12;
        binByteOffset = -1;
        while (pos + 8 <= total)
        {
            using var ch = new Uint8Array(buffer, pos, 8);
            var chBytes = ch.ReadBytes();
            uint clen = BitConverter.ToUInt32(chBytes, 0);
            uint ctype = BitConverter.ToUInt32(chBytes, 4);
            long dataStart = pos + 8;
            if (dataStart + clen > total) return false;

            if (ctype == ChunkJson)
            {
                using var js = new Uint8Array(buffer, dataStart, (int)clen);
                jsonBytes = js.ReadBytes();
                // Trim trailing spaces used as GLB JSON padding.
                int end = jsonBytes.Length;
                while (end > 0 && jsonBytes[end - 1] <= 0x20) end--;
                if (end != jsonBytes.Length)
                    System.Array.Resize(ref jsonBytes, end);
            }
            else if (ctype == ChunkBin)
            {
                binByteOffset = dataStart;
                binByteLength = clen;
            }

            pos = dataStart + clen;
        }

        return jsonBytes.Length > 0 && binByteOffset >= 0;
    }
}

/// <summary>Minimal GLB source handle used by the uploader (id + JS buffer).</summary>
public readonly record struct LoadedGltfSource(string Id, ArrayBuffer Buffer);

/// <summary>GPU-resident rest-pose mesh for one catalog entry.</summary>
public sealed class GpuEntityMesh : IDisposable
{
    public string Id { get; }
    public GPUBuffer PositionBuffer { get; }
    public GPUBuffer NormalBuffer { get; }
    public GPUBuffer IndexBuffer { get; }
    public int IndexCount { get; }
    public GPUIndexFormat IndexFormat { get; }
    public int VertexCount { get; }
    public Vector3 BoundsMin { get; }
    public Vector3 BoundsMax { get; }
    public bool HasWalkClip { get; }
    public bool HasAttackClip { get; }
    public bool HasIdleClip { get; }

    public float ModelHeight => MathF.Max(0.01f, BoundsMax.Y - BoundsMin.Y);

    private bool _disposed;

    public GpuEntityMesh(
        string id,
        GPUBuffer positionBuffer,
        GPUBuffer normalBuffer,
        GPUBuffer indexBuffer,
        int indexCount,
        GPUIndexFormat indexFormat,
        int vertexCount,
        Vector3 boundsMin,
        Vector3 boundsMax,
        bool hasWalkClip,
        bool hasAttackClip,
        bool hasIdleClip)
    {
        Id = id;
        PositionBuffer = positionBuffer;
        NormalBuffer = normalBuffer;
        IndexBuffer = indexBuffer;
        IndexCount = indexCount;
        IndexFormat = indexFormat;
        VertexCount = vertexCount;
        BoundsMin = boundsMin;
        BoundsMax = boundsMax;
        HasWalkClip = hasWalkClip;
        HasAttackClip = hasAttackClip;
        HasIdleClip = hasIdleClip;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        PositionBuffer.Destroy(); PositionBuffer.Dispose();
        NormalBuffer.Destroy(); NormalBuffer.Dispose();
        IndexBuffer.Destroy(); IndexBuffer.Dispose();
    }
}
