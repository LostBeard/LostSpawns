using LostSpawns.Models;

namespace LostSpawns.Rendering;

/// <summary>
/// CPU-side voxel mesh generator. Produces vertex data for exposed faces.
/// Each vertex: position (3f) + normal (3f) + color (3f) = 9 floats.
/// Positions are in world space (chunk offset applied).
/// </summary>
public static class VoxelMesher
{
    // Face directions: +X, -X, +Y, -Y, +Z, -Z
    private static readonly (int dx, int dy, int dz, float nx, float ny, float nz)[] Faces = new[]
    {
        ( 1,  0,  0,  1f,  0f,  0f), // Right
        (-1,  0,  0, -1f,  0f,  0f), // Left
        ( 0,  1,  0,  0f,  1f,  0f), // Top
        ( 0, -1,  0,  0f, -1f,  0f), // Bottom
        ( 0,  0,  1,  0f,  0f,  1f), // Front
        ( 0,  0, -1,  0f,  0f, -1f), // Back
    };

    // Two triangles per face (quad), using 6 vertices
    private static readonly float[][][] FaceVertices = new[]
    {
        // Right (+X)
        new[] {
            new[] { 1f, 0f, 0f }, new[] { 1f, 1f, 0f }, new[] { 1f, 1f, 1f },
            new[] { 1f, 0f, 0f }, new[] { 1f, 1f, 1f }, new[] { 1f, 0f, 1f },
        },
        // Left (-X)
        new[] {
            new[] { 0f, 0f, 1f }, new[] { 0f, 1f, 1f }, new[] { 0f, 1f, 0f },
            new[] { 0f, 0f, 1f }, new[] { 0f, 1f, 0f }, new[] { 0f, 0f, 0f },
        },
        // Top (+Y)
        new[] {
            new[] { 0f, 1f, 0f }, new[] { 0f, 1f, 1f }, new[] { 1f, 1f, 1f },
            new[] { 0f, 1f, 0f }, new[] { 1f, 1f, 1f }, new[] { 1f, 1f, 0f },
        },
        // Bottom (-Y)
        new[] {
            new[] { 0f, 0f, 1f }, new[] { 0f, 0f, 0f }, new[] { 1f, 0f, 0f },
            new[] { 0f, 0f, 1f }, new[] { 1f, 0f, 0f }, new[] { 1f, 0f, 1f },
        },
        // Front (+Z)
        new[] {
            new[] { 0f, 0f, 1f }, new[] { 1f, 0f, 1f }, new[] { 1f, 1f, 1f },
            new[] { 0f, 0f, 1f }, new[] { 1f, 1f, 1f }, new[] { 0f, 1f, 1f },
        },
        // Back (-Z)
        new[] {
            new[] { 1f, 0f, 0f }, new[] { 0f, 0f, 0f }, new[] { 0f, 1f, 0f },
            new[] { 1f, 0f, 0f }, new[] { 0f, 1f, 0f }, new[] { 1f, 1f, 0f },
        },
    };

    /// <summary>
    /// Generates mesh vertex data for a chunk in world space.
    /// Layout per vertex: px, py, pz, nx, ny, nz, r, g, b (9 floats).
    /// </summary>
    public static float[] GenerateMesh(ChunkData chunk)
    {
        int worldOffsetX = chunk.ChunkX * ChunkData.SizeXZ;
        int worldOffsetZ = chunk.ChunkZ * ChunkData.SizeXZ;

        var vertices = new List<float>(ChunkData.SizeXZ * ChunkData.SizeXZ * 6 * 9);

        for (int y = 0; y < ChunkData.Height; y++)
        for (int z = 0; z < ChunkData.SizeXZ; z++)
        for (int x = 0; x < ChunkData.SizeXZ; x++)
        {
            var block = chunk.GetBlock(x, y, z);
            if (block == BlockType.Air) continue;

            var (r, g, b) = GetBlockColor(block);

            for (int f = 0; f < 6; f++)
            {
                var (dx, dy, dz, nx, ny, nz) = Faces[f];
                var neighbor = chunk.GetBlock(x + dx, y + dy, z + dz);
                if (neighbor != BlockType.Air) continue;  // face is hidden

                // Compute per-vertex ambient occlusion for this face
                // Each vertex checks 3 neighboring blocks at its corner
                // More solid neighbors = darker vertex (0-3 occluders -> 1.0 to 0.55 brightness)
                var aoValues = ComputeFaceAO(chunk, x, y, z, f);

                // Face-dependent color adjustment (top brighter, bottom darker, sides muted)
                float fr = r, fg = g, fb = b;
                if (ny > 0.5f) { fr *= 1.05f; fg *= 1.05f; fb *= 1.02f; } // top: slight boost
                else if (ny < -0.5f) { fr *= 0.65f; fg *= 0.65f; fb *= 0.65f; } // bottom: dark
                else { fr *= 0.82f; fg *= 0.82f; fb *= 0.82f; } // sides: slight darken

                // Emit 6 vertices for this face with AO baked into color
                // Quad vertices map: v0=0, v1=1, v2=2, v3=0, v4=2, v5=3 (two triangles)
                int[] aoMap = { 0, 1, 2, 0, 2, 3 };
                for (int v = 0; v < 6; v++)
                {
                    float ao = aoValues[aoMap[v]];
                    var pos = FaceVertices[f][v];
                    vertices.Add(worldOffsetX + x + pos[0]);
                    vertices.Add(y + pos[1]);
                    vertices.Add(worldOffsetZ + z + pos[2]);
                    vertices.Add(nx);
                    vertices.Add(ny);
                    vertices.Add(nz);
                    vertices.Add(fr * ao);
                    vertices.Add(fg * ao);
                    vertices.Add(fb * ao);
                }
            }
        }

        return vertices.ToArray();
    }

    /// <summary>Returns the vertex count from a packed vertex array.</summary>
    public static int VertexCount(float[] packed) => packed.Length / 9;

    /// <summary>
    /// Computes ambient occlusion for the 4 corners of a face.
    /// Each corner checks 3 neighboring blocks (side1, side2, corner).
    /// Returns brightness values: 1.0 (no occlusion) to 0.55 (fully occluded).
    /// </summary>
    private static float[] ComputeFaceAO(ChunkData chunk, int x, int y, int z, int faceIndex)
    {
        // AO corner offsets per face - each corner checks side1, side2, and diagonal
        // For each face, 4 corners x 3 neighbor offsets
        int[][] cornerOffsets = faceIndex switch
        {
            0 => new[] { // +X face: corners at (1,0,0), (1,1,0), (1,1,1), (1,0,1)
                new[] { 1,0,-1, 1,-1,0, 1,-1,-1 }, new[] { 1,0,-1, 1,1,0, 1,1,-1 },
                new[] { 1,0,1, 1,1,0, 1,1,1 },     new[] { 1,0,1, 1,-1,0, 1,-1,1 },
            },
            1 => new[] { // -X face
                new[] { -1,0,1, -1,-1,0, -1,-1,1 }, new[] { -1,0,1, -1,1,0, -1,1,1 },
                new[] { -1,0,-1, -1,1,0, -1,1,-1 }, new[] { -1,0,-1, -1,-1,0, -1,-1,-1 },
            },
            2 => new[] { // +Y face (top)
                new[] { -1,1,0, 0,1,-1, -1,1,-1 }, new[] { -1,1,0, 0,1,1, -1,1,1 },
                new[] { 1,1,0, 0,1,1, 1,1,1 },     new[] { 1,1,0, 0,1,-1, 1,1,-1 },
            },
            3 => new[] { // -Y face (bottom)
                new[] { -1,-1,0, 0,-1,1, -1,-1,1 }, new[] { -1,-1,0, 0,-1,-1, -1,-1,-1 },
                new[] { 1,-1,0, 0,-1,-1, 1,-1,-1 },  new[] { 1,-1,0, 0,-1,1, 1,-1,1 },
            },
            4 => new[] { // +Z face
                new[] { -1,0,1, 0,-1,1, -1,-1,1 }, new[] { 1,0,1, 0,-1,1, 1,-1,1 },
                new[] { 1,0,1, 0,1,1, 1,1,1 },     new[] { -1,0,1, 0,1,1, -1,1,1 },
            },
            _ => new[] { // -Z face
                new[] { 1,0,-1, 0,-1,-1, 1,-1,-1 }, new[] { -1,0,-1, 0,-1,-1, -1,-1,-1 },
                new[] { -1,0,-1, 0,1,-1, -1,1,-1 }, new[] { 1,0,-1, 0,1,-1, 1,1,-1 },
            },
        };

        var ao = new float[4];
        for (int corner = 0; corner < 4; corner++)
        {
            var offsets = cornerOffsets[corner];
            bool s1 = chunk.GetBlock(x + offsets[0], y + offsets[1], z + offsets[2]) != BlockType.Air;
            bool s2 = chunk.GetBlock(x + offsets[3], y + offsets[4], z + offsets[5]) != BlockType.Air;
            bool cn = chunk.GetBlock(x + offsets[6], y + offsets[7], z + offsets[8]) != BlockType.Air;

            // Classic voxel AO formula: 0-3 occluders
            int occluders = (s1 ? 1 : 0) + (s2 ? 1 : 0) + ((s1 && s2) ? 1 : (cn ? 1 : 0));
            ao[corner] = occluders switch
            {
                0 => 1.00f,  // fully lit
                1 => 0.85f,  // slightly shadowed
                2 => 0.70f,  // moderately shadowed
                _ => 0.55f,  // heavily shadowed (corner)
            };
        }
        return ao;
    }

    private static (float r, float g, float b) GetBlockColor(BlockType type) => type switch
    {
        // DayZ palette - muted, desaturated, post-apocalyptic
        BlockType.Grass  => (0.28f, 0.38f, 0.18f),  // muted olive, dark forest green
        BlockType.Dirt   => (0.32f, 0.24f, 0.16f),  // dark brown, muddy
        BlockType.Stone  => (0.38f, 0.38f, 0.40f),  // cold gray, weathered
        BlockType.Sand   => (0.55f, 0.50f, 0.38f),  // dirty beige, wet sand
        BlockType.Water  => (0.12f, 0.22f, 0.32f),  // dark blue-green, murky
        BlockType.Wood   => (0.30f, 0.25f, 0.18f),  // weathered gray-brown bark
        BlockType.Leaves => (0.20f, 0.32f, 0.14f),  // dark green, some brown mixed
        _ => (1.0f, 0.0f, 1.0f), // magenta = unknown
    };
}
