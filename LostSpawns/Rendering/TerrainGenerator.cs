using LostSpawns.Models;

namespace LostSpawns.Rendering;

/// <summary>
/// Generates voxel terrain for chunks using Perlin noise.
/// Produces rolling hills with stone/dirt/grass layers, sand near water, and simple trees.
/// </summary>
public class TerrainGenerator
{
    private readonly PerlinNoise _noise;
    private readonly int _seed;

    public const int SeaLevel = 18;
    public const int BaseHeight = 24;
    public const float HeightScale = 14f;
    public const float NoiseScale = 0.025f;

    public TerrainGenerator(int seed)
    {
        _seed = seed;
        _noise = new PerlinNoise(seed);
    }

    public ChunkData GenerateChunk(int chunkX, int chunkZ)
    {
        var chunk = new ChunkData(chunkX, chunkZ);

        // Pass 1: terrain heightmap (CPU Perlin noise)
        for (int x = 0; x < ChunkData.SizeXZ; x++)
        for (int z = 0; z < ChunkData.SizeXZ; z++)
        {
            float worldX = chunkX * ChunkData.SizeXZ + x;
            float worldZ = chunkZ * ChunkData.SizeXZ + z;
            int h = GetHeight(worldX, worldZ);
            FillColumn(chunk, x, z, h);
        }

        PlaceTrees(chunk, chunkX, chunkZ);
        return chunk;
    }

    /// <summary>
    /// Generates a chunk using a pre-computed heightmap (e.g., from GPU kernel).
    /// heightmap must be 256 entries (16×16 grid, index = z*16 + x).
    /// </summary>
    public ChunkData GenerateChunkFromHeightmap(int chunkX, int chunkZ, int[] heightmap)
    {
        var chunk = new ChunkData(chunkX, chunkZ);

        for (int x = 0; x < ChunkData.SizeXZ; x++)
        for (int z = 0; z < ChunkData.SizeXZ; z++)
        {
            int h = heightmap[z * ChunkData.SizeXZ + x];
            FillColumn(chunk, x, z, h);
        }

        PlaceTrees(chunk, chunkX, chunkZ);
        return chunk;
    }

    private static void FillColumn(ChunkData chunk, int x, int z, int h)
    {
        for (int y = 0; y < ChunkData.Height; y++)
        {
            BlockType block;
            if (y > h)
                block = y <= SeaLevel ? BlockType.Water : BlockType.Air;
            else if (y == h)
                block = h <= SeaLevel + 1 ? BlockType.Sand : BlockType.Grass;
            else if (y > h - 4)
                block = BlockType.Dirt;
            else
                block = BlockType.Stone;

            chunk.SetBlock(x, y, z, block);
        }
    }

    public int GetHeight(float worldX, float worldZ)
    {
        float n = _noise.OctaveNoise(worldX * NoiseScale, worldZ * NoiseScale, 4, 0.5f, 2f);
        return Math.Clamp((int)(BaseHeight + n * HeightScale), 1, ChunkData.Height - 2);
    }

    private void PlaceTrees(ChunkData chunk, int chunkX, int chunkZ)
    {
        Vegetation.Place(chunk, chunkX, chunkZ, SurfaceAt, SeaLevel, _seed);
    }

    private (int y, BlockType type) SurfaceAt(int worldX, int worldZ)
    {
        int h = GetHeight(worldX, worldZ);
        return (h, h <= SeaLevel + 1 ? BlockType.Sand : BlockType.Grass);
    }
}
