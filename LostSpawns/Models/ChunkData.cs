namespace LostSpawns.Models;

/// <summary>
/// Holds voxel data for a single chunk (16×64×16, Y-up).
/// </summary>
public class ChunkData
{
    public const int SizeXZ = 16;
    public const int Height = 64;
    public const int Volume = SizeXZ * Height * SizeXZ;

    private readonly byte[] _blocks = new byte[Volume];

    public int ChunkX { get; }
    public int ChunkZ { get; }

    /// <summary>Raw block data (flat array, Y-up: index = x + z*SizeXZ + y*SizeXZ*SizeXZ).</summary>
    public byte[] Blocks => _blocks;

    public ChunkData(int chunkX = 0, int chunkZ = 0)
    {
        ChunkX = chunkX;
        ChunkZ = chunkZ;
    }

    public BlockType GetBlock(int x, int y, int z)
    {
        if (x < 0 || x >= SizeXZ || y < 0 || y >= Height || z < 0 || z >= SizeXZ)
            return BlockType.Air;
        return (BlockType)_blocks[x + z * SizeXZ + y * SizeXZ * SizeXZ];
    }

    public void SetBlock(int x, int y, int z, BlockType type)
    {
        if (x < 0 || x >= SizeXZ || y < 0 || y >= Height || z < 0 || z >= SizeXZ)
            return;
        _blocks[x + z * SizeXZ + y * SizeXZ * SizeXZ] = (byte)type;
    }
}
