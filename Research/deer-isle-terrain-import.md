# Deer Isle Terrain Import

**Date:** 2026-04-13
**Status:** Data downloaded, pipeline in progress

## Downloaded Data

| File | Source | Resolution | Size | Status |
|---|---|---|---|---|
| `TerrainData/copernicus_n44_w069.tif` | Copernicus DEM (ESA) | 30m | 33 MB | Downloaded |
| `TerrainData/usgs_13_n45w069.tif` | USGS 3DEP | 10m | Downloading | In progress |

## Bounding Box - Deer Isle, Hancock County, Maine

| Parameter | Value |
|---|---|
| Center | 44.225°N, 68.675°W |
| South | 44.153°N |
| North | 44.297°N |
| West | 68.773°W |
| East | 68.577°W |
| Size | ~16 x 16 km |
| Elevation range | 0-90m (sea level to hilltops) |

## What the Area Contains

- Deer Isle proper - main island with forested hills
- Stonington - fishing village, working harbor, granite quarries
- Deer Isle-Sedgwick Bridge - suspension bridge to mainland
- Numerous smaller islands (Crotch Island, Green Island, Stinson Neck)
- Rocky coastline, coves, harbors
- Small towns - Deer Isle village, Sunset, Sunshine, South Deer Isle
- Route 15 running the length of the island

## Pipeline

1. Read GeoTIFF elevation data (BitMiracle.LibTiff.NET)
2. Extract 16x16km subregion from full 1-degree tile
3. Map elevation to voxel heights (0-63 range for current ChunkData.Height=64)
4. Convert to chunk grid (16x16 blocks per chunk)
5. Apply biome rules: elevation + distance-from-water -> block type
6. Place vegetation based on biome
7. Generate structures at road intersections and harbors (future)

## Elevation Mapping

Real Deer Isle: 0m (sea level) to ~90m (highest hills)
Game scale option A: 1 meter = 1 voxel (needs Height > 90)
Game scale option B: compress to 0-63 range (current Height=64)
Game scale option C: increase ChunkData.Height to 256 (like Minecraft)

Recommendation: increase Height to 256 and use 1:1 mapping where possible.
