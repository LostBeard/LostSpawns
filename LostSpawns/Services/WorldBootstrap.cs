using SpawnDev.SpawnJS;

namespace LostSpawns.Services;

/// <summary>Starts the world the same way for every way of playing it (the desktop game and the headset): Deer Isle's elevation data, or procedural terrain if that cannot be loaded.</summary>
public static class WorldBootstrap
{
    /// <summary>Load the island's heightmap and start the world on it. Does nothing when the world is already running.</summary>
    public static async Task StartWorldAsync(SpawnJSRuntime js, WorldService world)
    {
        if (world.IsInitialized) return;
        var heightmapLoader = new HeightmapLoader();
        try
        {
            await heightmapLoader.LoadAsync(js, "maps/deer_isle.heightmap");
            heightmapLoader.SetCellSize(15.62f); // 16 km / 1024 grid
            world.InitWithHeightmap(heightmapLoader, seed: 42);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[World] Heightmap load failed: {ex.Message}, falling back to procedural");
            world.Init(seed: 42);
        }
    }

    /// <summary>Where to start: the height of the ground at the island's middle, or the first dry land found when that is under water.</summary>
    public static int SpawnHeight(WorldService world)
    {
        int spawnHeight = world.GetHeightAt(0, 0);
        if (spawnHeight <= 64) // 64 = sea level in HeightmapLoader
        {
            for (int search = 1; search < 200; search += 4)
            {
                int h = world.GetHeightAt(search * 16, search * 16);
                if (h > 68) { spawnHeight = h; break; }
            }
        }
        return spawnHeight;
    }
}
