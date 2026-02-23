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

                // Emit 6 vertices for this face (world-space positions)
                for (int v = 0; v < 6; v++)
                {
                    var pos = FaceVertices[f][v];
                    vertices.Add(worldOffsetX + x + pos[0]); // world X
                    vertices.Add(y + pos[1]);                 // world Y
                    vertices.Add(worldOffsetZ + z + pos[2]); // world Z
                    vertices.Add(nx);
                    vertices.Add(ny);
                    vertices.Add(nz);
                    vertices.Add(r);
                    vertices.Add(g);
                    vertices.Add(b);
                }
            }
        }

        return vertices.ToArray();
    }

    /// <summary>Returns the vertex count from a packed vertex array.</summary>
    public static int VertexCount(float[] packed) => packed.Length / 9;

    private static (float r, float g, float b) GetBlockColor(BlockType type) => type switch
    {
        BlockType.Grass  => (0.30f, 0.65f, 0.20f),
        BlockType.Dirt   => (0.55f, 0.35f, 0.18f),
        BlockType.Stone  => (0.50f, 0.50f, 0.50f),
        BlockType.Sand   => (0.85f, 0.78f, 0.52f),
        BlockType.Water  => (0.20f, 0.40f, 0.80f),
        BlockType.Wood   => (0.45f, 0.30f, 0.15f),
        BlockType.Leaves => (0.18f, 0.55f, 0.15f),
        _ => (1.0f, 0.0f, 1.0f), // magenta = unknown
    };
}
