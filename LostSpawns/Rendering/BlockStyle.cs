using System.Numerics;
using LostSpawns.Models;
using SpawnDev.VoxelEngine.Meshing;
using SpawnDev.VoxelEngine.Rendering;

namespace LostSpawns.Rendering;

/// <summary>
/// What each <see cref="BlockType"/> looks like: how the mesher treats it (<see cref="BlockClass"/>) and how the block
/// pipeline draws it (<see cref="BlockMaterial"/>). Indexed by the block's byte value, so the world's byte columns are
/// the pipeline's block ids as they are.
/// </summary>
public static class BlockStyle
{
    /// <summary>The water pass opacity.</summary>
    public const float WaterAlpha = 0.74f;

    public static int[] Classes()
    {
        var c = new int[Enum.GetValues<BlockType>().Max(b => (int)b) + 1];
        c[(int)BlockType.Air] = BlockClass.None;
        c[(int)BlockType.Dirt] = BlockClass.Opaque;
        c[(int)BlockType.Grass] = BlockClass.Opaque;
        c[(int)BlockType.Stone] = BlockClass.Opaque;
        c[(int)BlockType.Sand] = BlockClass.Opaque;
        c[(int)BlockType.Water] = BlockClass.Water;
        c[(int)BlockType.Wood] = BlockClass.Opaque;
        c[(int)BlockType.Leaves] = BlockClass.Cutout;   // holes in the leaves show what is behind them
        c[(int)BlockType.Birch] = BlockClass.Opaque;
        c[(int)BlockType.Planks] = BlockClass.Opaque;
        c[(int)BlockType.Cobblestone] = BlockClass.Opaque;
        c[(int)BlockType.Gravel] = BlockClass.Opaque;
        c[(int)BlockType.Granite] = BlockClass.Opaque;
        c[(int)BlockType.TallGrass] = BlockClass.Cross; // crossed planes; nothing is hidden by them
        c[(int)BlockType.Fern] = BlockClass.Cross;
        c[(int)BlockType.Dandelion] = BlockClass.Cross;
        c[(int)BlockType.Aster] = BlockClass.Cross;
        return c;
    }

    public static BlockMaterial[] Materials()
    {
        var m = new BlockMaterial[Enum.GetValues<BlockType>().Max(b => (int)b) + 1];
        for (int i = 0; i < m.Length; i++) m[i] = BlockMaterial.Missing;

        m[(int)BlockType.Air] = BlockMaterial.Uniform(BlockMaterial.NoTexture);
        m[(int)BlockType.Dirt] = BlockMaterial.Uniform((ushort)BlockTiles.Dirt);
        m[(int)BlockType.Stone] = BlockMaterial.Uniform((ushort)BlockTiles.Stone);
        m[(int)BlockType.Sand] = BlockMaterial.Uniform((ushort)BlockTiles.Sand);

        // Grass: the top takes the biome's grass colour (face 4 = +Y), the sides carry their own baked fringe
        m[(int)BlockType.Grass] = BlockMaterial.Create(
            (ushort)BlockTiles.GrassSide, (ushort)BlockTiles.GrassSide, (ushort)BlockTiles.GrassSide, (ushort)BlockTiles.GrassSide,
            (ushort)BlockTiles.GrassTop, (ushort)BlockTiles.Dirt,
            Vector3.One, BlockMaterial.TintFaces(1 << 4) | BlockMaterial.TintGrass);

        m[(int)BlockType.Wood] = BlockMaterial.Create(
            (ushort)BlockTiles.LogSide, (ushort)BlockTiles.LogSide, (ushort)BlockTiles.LogSide, (ushort)BlockTiles.LogSide,
            (ushort)BlockTiles.LogTop, (ushort)BlockTiles.LogTop);

        m[(int)BlockType.Leaves] = BlockMaterial.Uniform((ushort)BlockTiles.Leaves, Vector3.One,
            BlockMaterial.FlagTinted | BlockMaterial.TintFoliage);

        m[(int)BlockType.Birch] = BlockMaterial.Create(
            (ushort)BlockTiles.BirchSide, (ushort)BlockTiles.BirchSide, (ushort)BlockTiles.BirchSide, (ushort)BlockTiles.BirchSide,
            (ushort)BlockTiles.BirchTop, (ushort)BlockTiles.BirchTop);
        m[(int)BlockType.Planks] = BlockMaterial.Uniform((ushort)BlockTiles.Planks);
        m[(int)BlockType.Cobblestone] = BlockMaterial.Uniform((ushort)BlockTiles.Cobblestone);
        m[(int)BlockType.Gravel] = BlockMaterial.Uniform((ushort)BlockTiles.Gravel);
        m[(int)BlockType.Granite] = BlockMaterial.Uniform((ushort)BlockTiles.Granite);
        // Grass tufts and ferns are grayscale sprites coloured by the biome's grass colour; the flowers carry their own colours
        uint grassTint = BlockMaterial.FlagTinted | BlockMaterial.TintGrass;
        m[(int)BlockType.TallGrass] = BlockMaterial.Uniform((ushort)BlockTiles.TallGrass, Vector3.One, grassTint);
        m[(int)BlockType.Fern] = BlockMaterial.Uniform((ushort)BlockTiles.Fern, Vector3.One, grassTint);
        m[(int)BlockType.Dandelion] = BlockMaterial.Uniform((ushort)BlockTiles.Dandelion);
        m[(int)BlockType.Aster] = BlockMaterial.Uniform((ushort)BlockTiles.Aster);

        // Water: the surface sits a tenth low, lit flat; tinted by the biome's water colour
        m[(int)BlockType.Water] = BlockMaterial.Uniform((ushort)BlockTiles.Water0, Vector3.One,
            BlockMaterial.FlagTinted | BlockMaterial.TintWater | BlockMaterial.FlagLowerTop | BlockMaterial.FlagFlatShading, WaterAlpha);
        return m;
    }

    /// <summary>The animations of the atlas: the water loops through its eight frames.</summary>
    public static List<TextureAnimation> Animations() => new() { new TextureAnimation(BlockTiles.Water0, BlockTextures.WaterTimeline()) };
}
