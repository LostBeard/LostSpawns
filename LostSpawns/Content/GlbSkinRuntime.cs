using System.Numerics;
using System.Text.Json;
using SpawnDev.SpawnJS.JSObjects;

namespace LostSpawns.Content;

/// <summary>
/// CPU-side glTF skin + animation curves for one uploaded mesh.
/// Evaluates joint palettes (ibm * world) each frame for GPU skinning.
/// </summary>
public sealed class GlbSkinRuntime
{
    public const int MaxJoints = 64;

    public int JointCount { get; }
    public int[] JointNodeIndices { get; }
    public Matrix4x4[] InverseBind { get; }

    private readonly NodeState[] _nodes;
    private readonly int[] _parents;
    private readonly Dictionary<string, AnimClip> _clips;
    private readonly Matrix4x4[] _world;
    private readonly Matrix4x4[] _palette;
    private readonly NodeState[] _scratch;

    private GlbSkinRuntime(
        NodeState[] nodes,
        int[] parents,
        int[] jointNodeIndices,
        Matrix4x4[] inverseBind,
        Dictionary<string, AnimClip> clips)
    {
        _nodes = nodes;
        _parents = parents;
        JointNodeIndices = jointNodeIndices;
        InverseBind = inverseBind;
        JointCount = jointNodeIndices.Length;
        _clips = clips;
        _world = new Matrix4x4[nodes.Length];
        _palette = new Matrix4x4[MaxJoints];
        _scratch = new NodeState[nodes.Length];
        for (int i = 0; i < MaxJoints; i++)
            _palette[i] = Matrix4x4.Identity;
    }

    public bool HasClip(string name) => _clips.ContainsKey(name);

    public string? ResolveClip(bool charging, float speed)
    {
        if (charging && _clips.ContainsKey("Attack")) return "Attack";
        if (speed > 0.12f && _clips.ContainsKey("Walk")) return "Walk";
        if (_clips.ContainsKey("Idle")) return "Idle";
        if (_clips.ContainsKey("Idle_2")) return "Idle_2";
        return _clips.Keys.FirstOrDefault();
    }

    /// <summary>
    /// Sample <paramref name="clipName"/> at <paramref name="time"/> (loops by duration)
    /// and write up to MaxJoints skin matrices into <paramref name="dest"/> (row-major Numerics).
    /// Returns joint count written.
    /// </summary>
    public int EvaluatePalette(string? clipName, float time, Span<Matrix4x4> dest)
    {
        // Reset to rest pose.
        for (int i = 0; i < _nodes.Length; i++)
            _scratch[i] = _nodes[i];

        if (clipName != null && _clips.TryGetValue(clipName, out var clip) && clip.Duration > 1e-5f)
        {
            float t = time % clip.Duration;
            if (t < 0f) t += clip.Duration;
            for (int c = 0; c < clip.Channels.Length; c++)
            {
                var ch = clip.Channels[c];
                if ((uint)ch.NodeIndex >= (uint)_scratch.Length) continue;
                SampleChannel(ch, t, ref _scratch[ch.NodeIndex]);
            }
        }

        // World = local * parentWorld (Numerics / row-vector). Parents may have
        // higher indices than children (Quaternius armature root is last), so
        // resolve recursively with a visited stamp.
        Span<byte> ready = stackalloc byte[_scratch.Length];
        ready.Clear();
        for (int i = 0; i < _scratch.Length; i++)
            EnsureWorld(i, ready);

        int n = Math.Min(JointCount, Math.Min(MaxJoints, dest.Length));
        for (int j = 0; j < n; j++)
        {
            int node = JointNodeIndices[j];
            // Numerics: skin = ibm * world  (matches glTF column world*ibm after transpose load)
            dest[j] = InverseBind[j] * _world[node];
        }
        for (int j = n; j < Math.Min(MaxJoints, dest.Length); j++)
            dest[j] = Matrix4x4.Identity;
        return n;
    }

    private void EnsureWorld(int i, Span<byte> ready)
    {
        if (ready[i] != 0) return;
        ready[i] = 1;
        int p = _parents[i];
        if (p >= 0)
        {
            EnsureWorld(p, ready);
            _world[i] = LocalMatrix(_scratch[i]) * _world[p];
        }
        else
        {
            _world[i] = LocalMatrix(_scratch[i]);
        }
    }

    private static Matrix4x4 LocalMatrix(in NodeState n) =>
        Matrix4x4.CreateScale(n.Scale)
        * Matrix4x4.CreateFromQuaternion(n.Rotation)
        * Matrix4x4.CreateTranslation(n.Translation);

    private static void SampleChannel(AnimChannel ch, float t, ref NodeState node)
    {
        int count = ch.Times.Length;
        if (count == 0) return;
        if (count == 1 || t <= ch.Times[0])
        {
            ApplyKey(ch, 0, ref node);
            return;
        }
        if (t >= ch.Times[count - 1])
        {
            ApplyKey(ch, count - 1, ref node);
            return;
        }

        int i = 1;
        while (i < count && ch.Times[i] < t) i++;
        int i0 = i - 1;
        int i1 = i;
        float t0 = ch.Times[i0];
        float t1 = ch.Times[i1];
        float u = (t1 > t0) ? (t - t0) / (t1 - t0) : 0f;

        switch (ch.Path)
        {
            case AnimPath.Translation:
            {
                var a = ReadVec3(ch.Values, i0);
                var b = ReadVec3(ch.Values, i1);
                node.Translation = Vector3.Lerp(a, b, u);
                break;
            }
            case AnimPath.Scale:
            {
                var a = ReadVec3(ch.Values, i0);
                var b = ReadVec3(ch.Values, i1);
                node.Scale = Vector3.Lerp(a, b, u);
                break;
            }
            case AnimPath.Rotation:
            {
                var a = ReadQuat(ch.Values, i0);
                var b = ReadQuat(ch.Values, i1);
                node.Rotation = Quaternion.Slerp(a, b, u);
                break;
            }
        }
    }

    private static void ApplyKey(AnimChannel ch, int index, ref NodeState node)
    {
        switch (ch.Path)
        {
            case AnimPath.Translation:
                node.Translation = ReadVec3(ch.Values, index);
                break;
            case AnimPath.Scale:
                node.Scale = ReadVec3(ch.Values, index);
                break;
            case AnimPath.Rotation:
                node.Rotation = ReadQuat(ch.Values, index);
                break;
        }
    }

    private static Vector3 ReadVec3(float[] v, int i)
    {
        int o = i * 3;
        return new Vector3(v[o], v[o + 1], v[o + 2]);
    }

    private static Quaternion ReadQuat(float[] v, int i)
    {
        // glTF quaternion is XYZW
        int o = i * 4;
        return Quaternion.Normalize(new Quaternion(v[o], v[o + 1], v[o + 2], v[o + 3]));
    }

    public static GlbSkinRuntime? TryParse(
        JsonElement root,
        ArrayBuffer binBuffer,
        long binByteOffset,
        long binByteLength)
    {
        if (!root.TryGetProperty("skins", out var skins) || skins.GetArrayLength() == 0)
            return null;
        if (!root.TryGetProperty("nodes", out var nodesEl))
            return null;

        var accessors = root.GetProperty("accessors");
        var views = root.GetProperty("bufferViews");
        int nodeCount = nodesEl.GetArrayLength();

        var nodes = new NodeState[nodeCount];
        var parents = new int[nodeCount];
        for (int i = 0; i < nodeCount; i++) parents[i] = -1;

        for (int i = 0; i < nodeCount; i++)
        {
            var n = nodesEl[i];
            nodes[i] = ReadRestNode(n);
            if (n.TryGetProperty("children", out var kids))
            {
                foreach (var c in kids.EnumerateArray())
                {
                    int ci = c.GetInt32();
                    if ((uint)ci < (uint)nodeCount)
                        parents[ci] = i;
                }
            }
        }

        var skin = skins[0];
        var jointEls = skin.GetProperty("joints");
        int jointCount = jointEls.GetArrayLength();
        if (jointCount <= 0 || jointCount > MaxJoints)
        {
            Console.WriteLine($"[Gltf] skin joint count {jointCount} unsupported (max {MaxJoints})");
            return null;
        }

        var jointNodes = new int[jointCount];
        for (int i = 0; i < jointCount; i++)
            jointNodes[i] = jointEls[i].GetInt32();

        int ibmAcc = skin.GetProperty("inverseBindMatrices").GetInt32();
        var ibmFloats = ReadFloatAccessor(accessors[ibmAcc], views, binBuffer, binByteOffset, binByteLength);
        if (ibmFloats == null || ibmFloats.Length < jointCount * 16)
        {
            Console.WriteLine("[Gltf] failed to read inverseBindMatrices");
            return null;
        }

        var ibm = new Matrix4x4[jointCount];
        for (int i = 0; i < jointCount; i++)
            ibm[i] = FromGltfMat4(ibmFloats, i * 16);

        var clips = new Dictionary<string, AnimClip>(StringComparer.OrdinalIgnoreCase);
        if (root.TryGetProperty("animations", out var anims))
        {
            foreach (var a in anims.EnumerateArray())
            {
                string name = a.TryGetProperty("name", out var nm) ? nm.GetString() ?? "" : "";
                if (string.IsNullOrEmpty(name)) continue;
                // Prefer first Idle*, keep Walk/Attack/Idle exactly.
                bool keep =
                    name.Equals("Walk", StringComparison.OrdinalIgnoreCase)
                    || name.Equals("Attack", StringComparison.OrdinalIgnoreCase)
                    || name.Equals("Idle", StringComparison.OrdinalIgnoreCase)
                    || name.Equals("Idle_2", StringComparison.OrdinalIgnoreCase);
                if (!keep) continue;
                if (clips.ContainsKey(name)) continue;

                var clip = ParseClip(a, accessors, views, binBuffer, binByteOffset, binByteLength);
                if (clip != null)
                    clips[name] = clip;
            }
        }

        Console.WriteLine($"[Gltf] skin joints={jointCount} clips=[{string.Join(',', clips.Keys)}]");
        return new GlbSkinRuntime(nodes, parents, jointNodes, ibm, clips);
    }

    private static AnimClip? ParseClip(
        JsonElement anim,
        JsonElement accessors,
        JsonElement views,
        ArrayBuffer bin,
        long binOff,
        long binLen)
    {
        if (!anim.TryGetProperty("channels", out var channelsEl)
            || !anim.TryGetProperty("samplers", out var samplersEl))
            return null;

        var list = new List<AnimChannel>();
        float duration = 0f;

        foreach (var ch in channelsEl.EnumerateArray())
        {
            int samplerIdx = ch.GetProperty("sampler").GetInt32();
            var target = ch.GetProperty("target");
            if (!target.TryGetProperty("node", out var nodeEl)) continue;
            int node = nodeEl.GetInt32();
            string path = target.GetProperty("path").GetString() ?? "";
            AnimPath ap = path switch
            {
                "translation" => AnimPath.Translation,
                "rotation" => AnimPath.Rotation,
                "scale" => AnimPath.Scale,
                _ => AnimPath.Unknown,
            };
            if (ap == AnimPath.Unknown) continue;

            var sampler = samplersEl[samplerIdx];
            int inAcc = sampler.GetProperty("input").GetInt32();
            int outAcc = sampler.GetProperty("output").GetInt32();
            var times = ReadFloatAccessor(accessors[inAcc], views, bin, binOff, binLen);
            var values = ReadFloatAccessor(accessors[outAcc], views, bin, binOff, binLen);
            if (times == null || values == null || times.Length == 0) continue;

            duration = MathF.Max(duration, times[^1]);
            list.Add(new AnimChannel(node, ap, times, values));
        }

        if (list.Count == 0) return null;
        return new AnimClip(duration, list.ToArray());
    }

    private static NodeState ReadRestNode(JsonElement n)
    {
        var t = Vector3.Zero;
        var r = Quaternion.Identity;
        var s = Vector3.One;
        if (n.TryGetProperty("translation", out var te) && te.GetArrayLength() >= 3)
            t = new Vector3(te[0].GetSingle(), te[1].GetSingle(), te[2].GetSingle());
        if (n.TryGetProperty("rotation", out var re) && re.GetArrayLength() >= 4)
            r = Quaternion.Normalize(new Quaternion(
                re[0].GetSingle(), re[1].GetSingle(), re[2].GetSingle(), re[3].GetSingle()));
        if (n.TryGetProperty("scale", out var se) && se.GetArrayLength() >= 3)
            s = new Vector3(se[0].GetSingle(), se[1].GetSingle(), se[2].GetSingle());
        return new NodeState(t, r, s);
    }

    private static Matrix4x4 FromGltfMat4(float[] m, int o) =>
        new(
            m[o], m[o + 4], m[o + 8], m[o + 12],
            m[o + 1], m[o + 5], m[o + 9], m[o + 13],
            m[o + 2], m[o + 6], m[o + 10], m[o + 14],
            m[o + 3], m[o + 7], m[o + 11], m[o + 15]);

    private static float[]? ReadFloatAccessor(
        JsonElement accessor,
        JsonElement views,
        ArrayBuffer bin,
        long binByteOffset,
        long binByteLength)
    {
        if (accessor.GetProperty("componentType").GetInt32() != 5126)
            return null;
        int count = accessor.GetProperty("count").GetInt32();
        string type = accessor.GetProperty("type").GetString() ?? "";
        int comps = type switch
        {
            "SCALAR" => 1,
            "VEC2" => 2,
            "VEC3" => 3,
            "VEC4" => 4,
            "MAT4" => 16,
            _ => 0,
        };
        if (comps == 0) return null;

        int viewIdx = accessor.GetProperty("bufferView").GetInt32();
        var view = views[viewIdx];
        long viewOff = view.TryGetProperty("byteOffset", out var vo) ? vo.GetInt64() : 0;
        long accOff = accessor.TryGetProperty("byteOffset", out var ao) ? ao.GetInt64() : 0;
        int stride = view.TryGetProperty("byteStride", out var bs)
            ? bs.GetInt32()
            : comps * 4;
        long start = binByteOffset + viewOff + accOff;
        int byteLen = count * stride;
        if (start + byteLen > binByteOffset + binByteLength && stride == comps * 4)
        {
            // tightly packed length
            byteLen = count * comps * 4;
        }
        if (start < binByteOffset || start + (count - 1) * stride + comps * 4 > binByteOffset + binByteLength)
            return null;

        // Copy floats into managed - animation/IBM only (small).
        var result = new float[count * comps];
        if (stride == comps * 4)
        {
            using var u8 = new Uint8Array(bin, start, count * comps * 4);
            var bytes = u8.ReadBytes();
            System.Buffer.BlockCopy(bytes, 0, result, 0, bytes.Length);
        }
        else
        {
            for (int i = 0; i < count; i++)
            {
                using var u8 = new Uint8Array(bin, start + i * stride, comps * 4);
                var bytes = u8.ReadBytes();
                for (int c = 0; c < comps; c++)
                    result[i * comps + c] = BitConverter.ToSingle(bytes, c * 4);
            }
        }
        return result;
    }

    private struct NodeState
    {
        public Vector3 Translation;
        public Quaternion Rotation;
        public Vector3 Scale;
        public NodeState(Vector3 t, Quaternion r, Vector3 s)
        {
            Translation = t; Rotation = r; Scale = s;
        }
    }

    private enum AnimPath { Unknown, Translation, Rotation, Scale }

    private sealed class AnimChannel
    {
        public int NodeIndex;
        public AnimPath Path;
        public float[] Times;
        public float[] Values;
        public AnimChannel(int node, AnimPath path, float[] times, float[] values)
        {
            NodeIndex = node; Path = path; Times = times; Values = values;
        }
    }

    private sealed class AnimClip
    {
        public float Duration;
        public AnimChannel[] Channels;
        public AnimClip(float duration, AnimChannel[] channels)
        {
            Duration = duration; Channels = channels;
        }
    }
}
