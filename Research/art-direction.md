# Lost Spawns - Art Direction

**Date:** 2026-04-13
**Core Rule:** This is DayZ, not Minecraft. No kid vibe. Ever.

---

## Visual Identity

**Tone:** Desolate. Tense. Beautiful in a broken way. Like standing in an abandoned town at dusk wondering if someone is watching you from the treeline.

**NOT:** Bright. Colorful. Cute. Cartoony. Playful. Blocky-for-the-sake-of-blocky.

---

## Color Palette

### Environment
- **Grass:** Muted olive, dark forest green - NOT Minecraft bright green
- **Dirt:** Dark brown, muddy - NOT warm chocolate
- **Stone:** Cold gray, weathered - NOT light gray
- **Sand:** Dirty beige, wet sand - NOT bright yellow
- **Water:** Dark blue-green, murky - NOT bright blue
- **Wood:** Weathered gray-brown, bark texture - NOT clean oak
- **Leaves:** Dark green, some brown/dead mixed in - NOT vibrant green
- **Snow:** Off-white, dirty - NOT pure white

### Man-Made
- **Concrete:** Cracked gray, water stains, moss creep
- **Metal:** Rusted orange-brown, oxidized
- **Asphalt:** Dark gray, cracked, weeds growing through
- **Brick:** Faded red-brown, crumbling mortar
- **Glass:** Dirty, broken, reflective fragments
- **Paint:** Peeling, faded, weather-beaten

### Atmosphere
- **Sky:** Overcast grays, muted sunsets (orange through haze, not vibrant)
- **Fog:** Thick, cold, reduces visibility to create tension
- **Night:** DARK. Real dark. Flashlight reveals 20m. NVGs reveal green-tinted world.
- **Rain:** Gray curtain, puddles reflect, everything looks wet and miserable

---

## Voxel Size Decision

**Target: 0.5m voxels** (half-meter cubes)

This gives terrain that looks significantly more natural than Minecraft's 1m blocks:
- Hills have smoother profiles (half the staircase effect)
- Buildings have more architectural detail
- Player feels appropriately sized in the world
- Still large enough for good performance (8x more than 1m, not 64x like 0.25m)

Chunk size at 0.5m voxels: 16x256x16 = 8m x 128m x 8m per chunk
(vs Minecraft 16m x 256m x 16m at 1m voxels)

Alternatively: keep 1m voxels internally but use greedy meshing + smooth shading to LOOK smoother. Cheaper, nearly as effective visually.

---

## Texture Style

- **Resolution:** 32x32 minimum per block face (vs Minecraft's 16x16)
- **Style:** Photorealistic-inspired, not pixel art. Desaturated.
- **Variation:** Multiple texture variants per block type (3-4 dirt textures randomly assigned)
- **Weathering:** All textures include subtle wear, cracks, stains
- **Normal maps:** Per-block-type for depth without extra geometry
- **NO:** Bright colors, clean surfaces, cartoon outlines, smiley faces

---

## Lighting

- **Ambient occlusion:** MANDATORY. Corners darken. Crevices are dark. This is the #1 visual upgrade over Minecraft.
- **Sun shadows:** Directional shadow mapping. Buildings cast shadows. Trees cast shadows.
- **Smooth lighting:** Light values interpolated between blocks (not flat per-face like early Minecraft)
- **Interior darkness:** Inside buildings is dark without light sources. Flashlight essential.
- **Fire/torch glow:** Warm orange point lights, flickering
- **NVG post-process:** Green/white phosphor filter, noise grain, limited range

---

## Post-Processing

- **Color grading:** Desaturated, slightly blue/cold shift for outdoors. Warm shift near fires.
- **Film grain:** Subtle, optional (adds grit)
- **Vignette:** Slight edge darkening
- **SSAO:** Screen-space ambient occlusion for depth
- **Bloom:** Very subtle - only on bright light sources (sun, fire, flashlight beam)
- **Depth of field:** Optional, for scope/binocular views
- **Motion blur:** Very subtle on fast turns, optional

---

## Sound Design Notes (Future)

- Wind through trees, constant ambient
- Crows calling (signal danger/corpses nearby)
- Distant gunshots (echo through valleys)
- Rain on different surfaces (metal roof vs ground vs water)
- Footsteps vary by surface (grass, gravel, concrete, wood, water)
- Silence is tense. Sudden sounds are terrifying.
- Voice chat with proximity falloff

---

## Reference Games (Visual Inspiration)

| Game | What to Take |
|---|---|
| DayZ | Overall tone, atmosphere, weather, color palette |
| Teardown | Small voxel realism, destruction physics, lighting |
| 7 Days to Die | Voxel building + realistic textures, zombies/survival |
| Vintage Story | Mature voxel aesthetic, no cartoon feel |
| The Long Dark | Color grading, isolation feel, weather |
| S.T.A.L.K.E.R. | Atmosphere, abandoned environments, anomalies |
