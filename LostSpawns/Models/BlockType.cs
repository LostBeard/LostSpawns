namespace LostSpawns.Models;

/// <summary>
/// Voxel block types. Stored as byte in chunk data.
/// </summary>
public enum BlockType : byte
{
    Air = 0,
    Dirt,
    Grass,
    Stone,
    Sand,
    Water,
    Wood,
    Leaves,
}
