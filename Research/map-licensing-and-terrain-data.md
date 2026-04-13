# Lost Spawns - Map Licensing & Terrain Data Research

**Date:** 2026-04-13
**Bottom line:** No DayZ map data can be used directly. ALL are restricted to Arma/DayZ only. But we CAN use the same real-world elevation data sources they used - that's public domain.

---

## DayZ Map Specs

| Map | Size | Grid | Cell Size | Creator | License |
|---|---|---|---|---|---|
| Chernarus+ | ~15x15 km (225 km2) | ~3840x3840 | ~4m | Bohemia Interactive | ADPL-SA (Arma/DayZ only) |
| Livonia | 163 km2 | - | - | Bohemia (DLC) | Proprietary (paid) |
| Deer Isle | 16x16 km (256 km2) | 4096x4096 | 4m | JohnMcLane | All rights reserved |
| Namalsk | ~12.8x12.8 km (63 km2 w/ice) | - | - | Sumrak (now Bohemia) | Copyright, no reuse |
| Esseker | ~60 km2 | - | - | Ronhill | All rights reserved |
| Takistan+ | - | - | - | Community | All rights reserved |
| Taviana | - | - | - | Martin Bauer | Trademarked, litigation threats |
| Sahrani | 20x20 km (400 km2) | - | - | Bohemia (ArmA 1) | APL-SA (Arma only) |

**Verdict: ALL maps are inspiration-only. Zero usable data for Lost Spawns.**

---

## The Legal Path: Real-World Elevation Data

These maps were inspired by real places. We can download the SAME real-world elevation data and use it as seeds for procedural generation. Nobody owns geography.

### Real-World Sources Behind DayZ Maps
| DayZ Map | Real Location | Elevation Data Source |
|---|---|---|
| Chernarus | Povrly, Usti nad Labem, Czech Republic | SRTM / Copernicus DEM |
| Namalsk | Aleutian Islands, Alaska | SRTM / USGS 3DEP |
| Deer Isle | Coastal Maine, USA | USGS 3DEP (1m resolution!) |
| Livonia | Southern Poland / Lithuania | Copernicus DEM |
| Sahrani | Mediterranean / Caribbean inspired | SRTM for any Med island |

### Free Elevation Data Sources
| Source | Resolution | Coverage | License |
|---|---|---|---|
| SRTM (NASA/USGS) | 30m | Global (60N-56S) | Public domain |
| ASTER GDEM v3 | 30m | Global | Free (USGS) |
| Copernicus DEM | 30m (free) / 10m (registered) | Global | Free (ESA) |
| USGS 3DEP | 1-10m | USA | Public domain |
| GMTED2010 | 250m-1km | Global | Free |

### Conversion Tools (Free/Open Source)
- touchterrain.geol.iastate.edu - GeoTiff export from real elevation
- manticorp.github.io/unrealheightmap - 16-bit PNG export
- wgen (MIT, Rust) - terrain generation to 16-bit PNG/EXR
- HTerrain (MIT, GDScript) - terrain generator with erosion

---

## Procedural Generation Pipeline for Lost Spawns

1. **Download real-world DEM** for target region (e.g., coastal Maine for "Deer Isle feel")
2. **Import into ILGPU** as heightmap array
3. **GPU processing** (all ILGPU kernels):
   - Scale to game units
   - Add Perlin noise for voxel-scale detail
   - Hydraulic erosion simulation
   - Coastal erosion for island shapes
4. **Voxelize** - convert heightmap to 3D voxel grid
5. **Biome assignment** - altitude + moisture -> biome type
6. **Structure placement** - procedural town/road/building placement
7. **Vegetation** - tree/bush distribution per biome
8. **Resource distribution** - ores, loot zones per biome

Result: terrain that FEELS like Deer Isle/Namalsk/Chernarus but is 100% our IP.

---

## Map Size Targets for Lost Spawns

| Map Name | Inspiration | Target Size | Terrain Character |
|---|---|---|---|
| "The Island" | Deer Isle (Maine coast) | 16x16 km | Dense forest, fishing towns, military ruins, arctic north |
| "The Wasteland" | Chernarus (Czech hills) | 15x15 km | Rolling countryside, small towns, military bases |
| "The Frozen" | Namalsk (Aleutian) | 8x8 km | Arctic, ice sheets, underground bunkers, harsh |
| "The Desert" | Takistan (Central Asia) | 12x12 km | Mountains, desert valleys, compounds |
| "The Paradise" | Sahrani (Mediterranean) | 10x10 km | Tropical south, temperate north, beaches |

Each map is a world seed - same seed always generates the same map, shareable between servers.

---

## DayZ Terrain Format Reference (for understanding map architecture)

- **WRP format:** Header + heightfield + texture indices + object placements
- **Grid:** Power of 2 (512, 1024, 2048, 4096, 8192)
- **Cell size:** Typically 4m for large maps
- **Heightmap:** 16-bit or 32-bit per cell
- **Object placement:** Position + rotation + model reference

Our voxel format is simpler: each cell is a block type ID at integer coordinates. But the terrain generation pipeline is similar - heightmap -> detail -> objects.
