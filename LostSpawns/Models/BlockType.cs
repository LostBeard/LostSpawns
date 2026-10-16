namespace LostSpawns.Models;

/// <summary>
/// Voxel block types. Stored as byte in chunk data and in saved edits, so values are permanent: append, never reorder.
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
    /// <summary>A tuft of grass standing on the ground (a plant: you walk through it).</summary>
    TallGrass,
    Fern,
    Dandelion,
    Aster,
    /// <summary>Shore and stream-bed pebbles; digs like sand.</summary>
    Gravel,
    /// <summary>Exposed rock: cliffs and shores. Mines like stone.</summary>
    Granite,
}

/// <summary>What a block type means for play, apart from how it looks.</summary>
public static class BlockTypes
{
    /// <summary>A plant stands in its cell without filling it: nothing collides with it and rays pass through.</summary>
    public static bool IsPlant(this BlockType t) => t is BlockType.TallGrass or BlockType.Fern or BlockType.Dandelion or BlockType.Aster;

    /// <summary>The block that decides how this one is mined, dug and heard: granite is stone, gravel is sand.</summary>
    public static BlockType Material(this BlockType t) => t switch
    {
        BlockType.Granite => BlockType.Stone,
        BlockType.Gravel => BlockType.Sand,
        _ => t,
    };
}
