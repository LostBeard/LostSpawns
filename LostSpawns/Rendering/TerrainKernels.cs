using ILGPU;
using ILGPU.Runtime;

namespace LostSpawns.Rendering;

/// <summary>
/// ILGPU compute kernels for terrain generation and voxel meshing.
/// All methods are static and GPU-compatible (no allocations, no classes).
/// </summary>
public static class TerrainKernels
{
    // ──────────────────────────────────────────────────────────────
    //  Constants for the mesh kernel
    // ──────────────────────────────────────────────────────────────
    private const int SizeXZ = 16;
    private const int Height = 64;
    private const int SizeXZ2 = SizeXZ * SizeXZ; // 256

    // 6 face directions: dx, dy, dz
    // Encoded as flat array: [face*3+0]=dx, [face*3+1]=dy, [face*3+2]=dz
    // Right(+X), Left(-X), Top(+Y), Bottom(-Y), Front(+Z), Back(-Z)

    // 6 face normals: nx, ny, nz (same order as directions)

    // 6 vertices per face × 3 coords = 18 floats per face
    // All faces encoded as flat data for GPU compatibility

    // ──────────────────────────────────────────────────────────────
    //  Heightmap kernel (unchanged)
    // ──────────────────────────────────────────────────────────────

    /// <summary>
    /// GPU kernel: computes the heightmap for a 16×16 chunk.
    /// Each work item handles one column (index 0..255).
    /// </summary>
    public static void HeightmapKernel(
        Index1D index,
        ArrayView<int> perm,
        ArrayView<int> output,
        float chunkWorldX,
        float chunkWorldZ,
        float noiseScale,
        float heightScale,
        float baseHeight,
        int octaves,
        int maxHeight)
    {
        int lx = index % 16;
        int lz = index / 16;
        float wx = chunkWorldX + lx;
        float wz = chunkWorldZ + lz;

        float n = OctaveNoise(perm, wx * noiseScale, wz * noiseScale, octaves, 0.5f, 2f);
        int h = (int)(baseHeight + n * heightScale);

        if (h < 1) h = 1;
        if (h > maxHeight - 2) h = maxHeight - 2;

        output[index] = h;
    }

    // ──────────────────────────────────────────────────────────────
    //  Mesh kernel
    // ──────────────────────────────────────────────────────────────

    /// <summary>
    /// GPU kernel: generates mesh vertex data for a chunk.
    /// Each work item handles one block (index 0..16383).
    /// Uses Atomic.Add on counter to claim output space for variable-length output.
    /// </summary>
    public static void MeshKernel(
        Index1D index,
        ArrayView<int> blocks,      // 16384 block types (byte→int)
        ArrayView<float> vertices,  // output vertex buffer (pre-allocated large)
        ArrayView<int> counter,     // atomic counter (1 element) — counts floats written
        int chunkWorldX,
        int chunkWorldZ)
    {
        // Decompose flat index → (x, y, z)
        // Index layout: x + z*16 + y*256
        int y = index / SizeXZ2;
        int rem = index - y * SizeXZ2;
        int z = rem / SizeXZ;
        int x = rem - z * SizeXZ;

        int blockType = blocks[index];
        if (blockType == 0) return; // Air

        // Check 6 neighbors and emit faces (color depends on face normal)
        float cr, cg, cb;

        // Face 0: Right (+X)
        if (GetBlock(blocks, x + 1, y, z) == 0)
        {
            GetBlockColorGpu(blockType, 0f, out cr, out cg, out cb);
            EmitFace(vertices, counter, x, y, z, chunkWorldX, chunkWorldZ,
                1f, 0f, 0f, cr, cg, cb, 0);
        }

        // Face 1: Left (-X)
        if (GetBlock(blocks, x - 1, y, z) == 0)
        {
            GetBlockColorGpu(blockType, 0f, out cr, out cg, out cb);
            EmitFace(vertices, counter, x, y, z, chunkWorldX, chunkWorldZ,
                -1f, 0f, 0f, cr, cg, cb, 1);
        }

        // Face 2: Top (+Y)
        if (GetBlock(blocks, x, y + 1, z) == 0)
        {
            GetBlockColorGpu(blockType, 1f, out cr, out cg, out cb);
            EmitFace(vertices, counter, x, y, z, chunkWorldX, chunkWorldZ,
                0f, 1f, 0f, cr, cg, cb, 2);
        }

        // Face 3: Bottom (-Y)
        if (GetBlock(blocks, x, y - 1, z) == 0)
        {
            GetBlockColorGpu(blockType, -1f, out cr, out cg, out cb);
            EmitFace(vertices, counter, x, y, z, chunkWorldX, chunkWorldZ,
                0f, -1f, 0f, cr, cg, cb, 3);
        }

        // Face 4: Front (+Z)
        if (GetBlock(blocks, x, y, z + 1) == 0)
        {
            GetBlockColorGpu(blockType, 0f, out cr, out cg, out cb);
            EmitFace(vertices, counter, x, y, z, chunkWorldX, chunkWorldZ,
                0f, 0f, 1f, cr, cg, cb, 4);
        }

        // Face 5: Back (-Z)
        if (GetBlock(blocks, x, y, z - 1) == 0)
        {
            GetBlockColorGpu(blockType, 0f, out cr, out cg, out cb);
            EmitFace(vertices, counter, x, y, z, chunkWorldX, chunkWorldZ,
                0f, 0f, -1f, cr, cg, cb, 5);
        }
    }

    /// <summary>Block lookup with bounds checking (out-of-bounds = Air).</summary>
    private static int GetBlock(ArrayView<int> blocks, int x, int y, int z)
    {
        if (x < 0 || x >= SizeXZ || y < 0 || y >= Height || z < 0 || z >= SizeXZ)
            return 0; // Air
        return blocks[x + z * SizeXZ + y * SizeXZ2];
    }

    /// <summary>Atomically claims space and writes 6 vertices (54 floats) for one face.</summary>
    private static void EmitFace(
        ArrayView<float> vertices, ArrayView<int> counter,
        int x, int y, int z, int cwx, int cwz,
        float nx, float ny, float nz,
        float cr, float cg, float cb,
        int faceIndex)
    {
        // Claim 54 floats (6 vertices × 9 floats each)
        int offset = Atomic.Add(ref counter[0], 54);

        // World position base
        float wx = cwx + x;
        float wy = y;
        float wz = cwz + z;

        // Write 6 vertices for this face
        // Each face is a quad made of 2 triangles = 6 vertices
        // Vertex offsets relative to block corner (x,y,z) depend on face index
        WriteVertex(vertices, offset + 0,  wx, wy, wz, nx, ny, nz, cr, cg, cb, faceIndex, 0);
        WriteVertex(vertices, offset + 9,  wx, wy, wz, nx, ny, nz, cr, cg, cb, faceIndex, 1);
        WriteVertex(vertices, offset + 18, wx, wy, wz, nx, ny, nz, cr, cg, cb, faceIndex, 2);
        WriteVertex(vertices, offset + 27, wx, wy, wz, nx, ny, nz, cr, cg, cb, faceIndex, 3);
        WriteVertex(vertices, offset + 36, wx, wy, wz, nx, ny, nz, cr, cg, cb, faceIndex, 4);
        WriteVertex(vertices, offset + 45, wx, wy, wz, nx, ny, nz, cr, cg, cb, faceIndex, 5);
    }

    /// <summary>Writes a single vertex (9 floats) to the output buffer.</summary>
    private static void WriteVertex(
        ArrayView<float> vertices, int offset,
        float wx, float wy, float wz,
        float nx, float ny, float nz,
        float cr, float cg, float cb,
        int faceIndex, int vertIndex)
    {
        // Get vertex position offset for this face/vertex combination
        float vx, vy, vz;
        GetFaceVertex(faceIndex, vertIndex, out vx, out vy, out vz);

        vertices[offset + 0] = wx + vx; // position X
        vertices[offset + 1] = wy + vy; // position Y
        vertices[offset + 2] = wz + vz; // position Z
        vertices[offset + 3] = nx;       // normal X
        vertices[offset + 4] = ny;       // normal Y
        vertices[offset + 5] = nz;       // normal Z
        vertices[offset + 6] = cr;       // color R
        vertices[offset + 7] = cg;       // color G
        vertices[offset + 8] = cb;       // color B
    }

    /// <summary>
    /// Returns vertex position offset for a given face and vertex index.
    /// All data encoded as branching logic for GPU compatibility (no arrays).
    /// </summary>
    private static void GetFaceVertex(int face, int vert, out float x, out float y, out float z)
    {
        // Default
        x = 0f; y = 0f; z = 0f;

        if (face == 0) // Right (+X)
        {
            if (vert == 0) { x = 1f; y = 0f; z = 0f; }
            else if (vert == 1) { x = 1f; y = 1f; z = 0f; }
            else if (vert == 2) { x = 1f; y = 1f; z = 1f; }
            else if (vert == 3) { x = 1f; y = 0f; z = 0f; }
            else if (vert == 4) { x = 1f; y = 1f; z = 1f; }
            else { x = 1f; y = 0f; z = 1f; }
        }
        else if (face == 1) // Left (-X)
        {
            if (vert == 0) { x = 0f; y = 0f; z = 1f; }
            else if (vert == 1) { x = 0f; y = 1f; z = 1f; }
            else if (vert == 2) { x = 0f; y = 1f; z = 0f; }
            else if (vert == 3) { x = 0f; y = 0f; z = 1f; }
            else if (vert == 4) { x = 0f; y = 1f; z = 0f; }
            else { x = 0f; y = 0f; z = 0f; }
        }
        else if (face == 2) // Top (+Y)
        {
            if (vert == 0) { x = 0f; y = 1f; z = 0f; }
            else if (vert == 1) { x = 0f; y = 1f; z = 1f; }
            else if (vert == 2) { x = 1f; y = 1f; z = 1f; }
            else if (vert == 3) { x = 0f; y = 1f; z = 0f; }
            else if (vert == 4) { x = 1f; y = 1f; z = 1f; }
            else { x = 1f; y = 1f; z = 0f; }
        }
        else if (face == 3) // Bottom (-Y)
        {
            if (vert == 0) { x = 0f; y = 0f; z = 1f; }
            else if (vert == 1) { x = 0f; y = 0f; z = 0f; }
            else if (vert == 2) { x = 1f; y = 0f; z = 0f; }
            else if (vert == 3) { x = 0f; y = 0f; z = 1f; }
            else if (vert == 4) { x = 1f; y = 0f; z = 0f; }
            else { x = 1f; y = 0f; z = 1f; }
        }
        else if (face == 4) // Front (+Z)
        {
            if (vert == 0) { x = 0f; y = 0f; z = 1f; }
            else if (vert == 1) { x = 1f; y = 0f; z = 1f; }
            else if (vert == 2) { x = 1f; y = 1f; z = 1f; }
            else if (vert == 3) { x = 0f; y = 0f; z = 1f; }
            else if (vert == 4) { x = 1f; y = 1f; z = 1f; }
            else { x = 0f; y = 1f; z = 1f; }
        }
        else // face == 5, Back (-Z)
        {
            if (vert == 0) { x = 1f; y = 0f; z = 0f; }
            else if (vert == 1) { x = 0f; y = 0f; z = 0f; }
            else if (vert == 2) { x = 0f; y = 1f; z = 0f; }
            else if (vert == 3) { x = 1f; y = 0f; z = 0f; }
            else if (vert == 4) { x = 0f; y = 1f; z = 0f; }
            else { x = 1f; y = 1f; z = 0f; }
        }
    }

    /// <summary>Block type + face normal → RGB color. Face-dependent for natural look.</summary>
    private static void GetBlockColorGpu(int blockType, float ny, out float r, out float g, out float b)
    {
        // BlockType enum: Air=0, Dirt=1, Grass=2, Stone=3, Sand=4, Water=5, Wood=6, Leaves=7
        if (blockType == 1) // Dirt
        {
            if (ny > 0.5f)
            { r = 0.60f; g = 0.40f; b = 0.22f; } // top: lighter dirt
            else
            { r = 0.55f; g = 0.35f; b = 0.18f; } // sides/bottom
        }
        else if (blockType == 2) // Grass
        {
            if (ny > 0.5f)
            { r = 0.30f; g = 0.65f; b = 0.20f; } // top: green
            else if (ny < -0.5f)
            { r = 0.55f; g = 0.35f; b = 0.18f; } // bottom: dirt
            else
            { r = 0.42f; g = 0.48f; b = 0.22f; } // sides: dirt-green mix
        }
        else if (blockType == 3) { r = 0.50f; g = 0.50f; b = 0.50f; }  // Stone
        else if (blockType == 4) { r = 0.85f; g = 0.78f; b = 0.52f; }  // Sand
        else if (blockType == 5) { r = 0.20f; g = 0.40f; b = 0.80f; }  // Water
        else if (blockType == 6) { r = 0.45f; g = 0.30f; b = 0.15f; }  // Wood
        else if (blockType == 7) { r = 0.18f; g = 0.55f; b = 0.15f; }  // Leaves
        else { r = 1.0f; g = 0.0f; b = 1.0f; }                         // Unknown
    }

    // ──────────────────────────────────────────────────────────────
    //  Perlin noise (for heightmap kernel)
    // ──────────────────────────────────────────────────────────────

    private static float OctaveNoise(ArrayView<int> perm, float x, float y, int octaves, float persistence, float lacunarity)
    {
        float total = 0f, amplitude = 1f, frequency = 1f, maxValue = 0f;
        for (int i = 0; i < octaves; i++)
        {
            total += Noise2D(perm, x * frequency, y * frequency) * amplitude;
            maxValue += amplitude;
            amplitude *= persistence;
            frequency *= lacunarity;
        }
        return total / maxValue;
    }

    private static float Noise2D(ArrayView<int> perm, float x, float y)
    {
        int xi = FloorToInt(x) & 255;
        int yi = FloorToInt(y) & 255;
        float xf = x - FloorF(x);
        float yf = y - FloorF(y);

        float u = Fade(xf);
        float v = Fade(yf);

        int aa = perm[perm[xi] + yi];
        int ab = perm[perm[xi] + yi + 1];
        int ba = perm[perm[xi + 1] + yi];
        int bb = perm[perm[xi + 1] + yi + 1];

        return Lerp(
            Lerp(Grad(aa, xf, yf), Grad(ba, xf - 1f, yf), u),
            Lerp(Grad(ab, xf, yf - 1f), Grad(bb, xf - 1f, yf - 1f), u),
            v);
    }

    private static float Fade(float t) => t * t * t * (t * (t * 6f - 15f) + 10f);
    private static float Lerp(float a, float b, float t) => a + t * (b - a);

    private static float Grad(int hash, float x, float y)
    {
        int h = hash & 3;
        float u = h < 2 ? x : y;
        float v = h < 2 ? y : x;
        return ((h & 1) == 0 ? u : -u) + ((h & 2) == 0 ? v : -v);
    }

    private static int FloorToInt(float x)
    {
        int xi = (int)x;
        // Explicit cast: WGSL cannot implicitly compare f32 with i32
        return x < (float)xi ? xi - 1 : xi;
    }

    private static float FloorF(float x)
    {
        int xi = (int)x;
        float xf = (float)xi;
        return x < xf ? xf - 1f : xf;
    }
}
