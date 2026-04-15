# Compression Techniques for Quest 3S Performance

**Author:** Data (Claude CLI #2)
**Date:** 2026-04-14
**Applies to:** SpawnDev.VoxelEngine, SpawnDev.GameUI, AubsCraft, Lost Spawns

Quest 3S has 8GB shared RAM (GPU + system), Adreno 740 GPU, mobile bandwidth constraints. Every byte saved in memory and every byte not transferred over the bus is a direct performance win.

---

## Background: TurboQuant and Compression Philosophy

SpawnDev.ILGPU.ML's TurboQuant compresses transformer KV caches 7-10x using Walsh-Hadamard Transform + Lloyd-Max quantization. Its key insight isn't just compression - it's that computation happens DIRECTLY on compressed data (FWHT preserves inner products, so attention runs on quantized values without decompression). This "operate in compressed domain" principle applies beyond ML.

TurboQuant is NOT based on texture compression (BC1/BC7/ASTC). It uses signal processing (orthogonal transforms) and information theory (optimal scalar quantization for Gaussian distributions). Different math, but the philosophy transfers.

---

## 1. ASTC Texture Compression - Biggest Single Win

### What
ASTC (Adaptive Scalable Texture Compression) is hardware-decompressed by Quest 3S's Adreno 740 at zero GPU cost. The GPU reads compressed blocks from memory and decompresses in the texture unit's fixed-function hardware. No shader cycles consumed.

### Compression Ratios

| Block Size | Bits/Pixel | Ratio vs RGBA8 | Quality | Best For |
|-----------|-----------|---------------|---------|----------|
| ASTC 4x4 | 8.0 | 4x | Highest | Normal maps, UI, SDF fonts |
| ASTC 5x5 | 5.12 | 6.25x | High | Diffuse textures |
| ASTC 6x6 | 3.56 | 8.99x | Good | Large textures, terrain |
| ASTC 8x8 | 2.0 | 16x | Acceptable | Background, skybox |

### Where It Applies

**VoxelEngine / Lost Spawns / AubsCraft:**
- PBR block textures (diffuse + normal + roughness arrays)
- 100 block types * 4 variants * 3 layers * 256x256 = 300 textures
- Uncompressed: 300 * 256KB = 75MB. ASTC 6x6: ~8.3MB. 9x savings.
- Terrain blend maps, skybox, particle textures

**GameUI:**
- Font atlas: 1024x1024 RGBA = 4MB. ASTC 4x4 = ~1MB (SDF data is smooth, compresses well)
- UI image atlas (item icons, status effects): ASTC 5x5
- Nine-slice panel textures: ASTC 4x4 (need sharp corners)

### Implementation Path
- WebGPU supports compressed texture formats via `textureCompressionASTC` feature
- Check `adapter.features.has("texture-compression-astc")` at init
- Upload pre-compressed `.astc` files via `queue.writeTexture()` with `astc-*` format
- ILGPU could generate ASTC blocks on GPU at runtime from raw RGBA (the block compression algorithm - partition search + endpoint optimization - is parallelizable per block)
- Fallback: uncompressed RGBA on devices without ASTC (desktop browsers, some older GPUs)

---

## 2. Voxel Data - Palette Compression

### What
When a 16x16x16 section uses few unique block types (common for natural terrain), store a palette + indices instead of full 12-bit types per voxel.

### How It Works
```
Section with 4096 voxels, 5 unique block types:

Uncompressed: 4096 * 2 bytes (PackedBlock) = 8192 bytes

Palette compressed:
  Palette: [stone=1, dirt=2, grass=3, air=0, gravel=42] = 5 entries * 2 bytes = 10 bytes
  Indices: 4096 * 3 bits (ceil(log2(5)) = 3) = 1536 bytes
  Total: 1546 bytes = 5.3x compression
```

### Compression by Terrain Type

| Section Type | Unique Types | Bits/Index | Bytes | Ratio |
|-------------|-------------|-----------|-------|-------|
| Solid (underground) | 1 | 0 | 2 (palette only) | 4096x |
| Simple surface | 3-4 | 2 | 1034 | 7.9x |
| Mixed terrain | 5-8 | 3 | 1546 | 5.3x |
| Complex (cave biome) | 9-16 | 4 | 2058 | 4.0x |
| Highly varied | 17+ | Skip palette | 8192 | 1x |

### The TurboQuant Insight: Operate on Compressed Data
- Frustum culling: one bit per section = "has visible geometry." 16x24x16 chunk = 6144 bits = 768 bytes. Cull entire sections against frustum planes without decompressing voxels.
- LOD generation: derive super-section dominant type from palette frequency without per-voxel scan. If palette entry 0 is "stone" at >90%, the LOD block is stone.
- Face cull: empty sections (palette = [air]) skip face culling entirely. Solid single-type sections emit only exterior faces (precomputable).
- Greedy merge: sections with palette size 1 produce exactly 6 quads (one per face) without running the merge kernel at all.

### Implementation
- Add `PaletteSection` alongside current `PackedBlock[]` representation
- Choose representation at section load time based on unique type count
- GPU kernels accept both representations via a mode flag or separate dispatch paths
- Minecraft uses this exact approach in its Chunk format (palette + bit array)

---

## 3. Vertex Buffer Compression

### UIRenderer (GameUI)

Current: 32 bytes/vertex (pos float32x2 + uv float32x2 + color float32x4)

Compressed: 12 bytes/vertex
| Attribute | Current | Compressed | Format |
|-----------|---------|-----------|--------|
| Position | float32x2 (8B) | float16x2 (4B) | Screen coords fit in half precision |
| UV | float32x2 (8B) | float16x2 (4B) | Atlas coords 0-1 are perfect for fp16 |
| Color | float32x4 (16B) | unorm8x4 (4B) | 256 levels per channel is sufficient |

Result: 62.5% bandwidth reduction. For 4096 quads * 6 verts = 24K vertices:
- Current: 768KB per frame upload
- Compressed: 288KB per frame upload
- Savings: 480KB/frame at 72Hz (Quest refresh) = 33.75MB/s bandwidth saved

WebGPU vertex formats `float16x2` and `unorm8x4` are natively supported.

### VoxelEngine Mesh Vertices

Current vertex format TBD. Recommended compressed format:

| Attribute | Size | Format | Notes |
|-----------|------|--------|-------|
| Position XYZ | 3 bytes | uint8x4 (xyz + face) | Section-local 0-15 fits in 4 bits each; pack xyz into 12 bits + 3-bit face in padding |
| UV | 4 bytes | float16x2 | Texture atlas coordinates |
| AO | 1 byte | uint8 | 4 corners * 2 bits = 8 bits |
| Block type | 2 bytes | uint16 | For texture array lookup |
| Lighting | 2 bytes | unorm8x2 | Block light + sky light |
| **Total** | **12 bytes** | | vs typical 32-40 bytes uncompressed |

At 0.5m voxels (Lost Spawns), a visible section might have 500-2000 quads = 3000-12000 vertices:
- Uncompressed (36B/vert): 108-432KB per section
- Compressed (12B/vert): 36-144KB per section
- With 200 visible sections: 7.2-28.8MB vs 21.6-86.4MB

On Quest 3S with 8GB shared RAM, this is the difference between fitting and not fitting.

---

## 4. Face Mask Sparsity Compression

### What
Face masks (uint64 per XZ column, one bit per Y layer) are sparse in surface terrain. Most columns are all-air (mask = 0) or have 1-2 bits set (surface layer only).

### Dense vs Sparse Representation

For a flat terrain section at the surface:
- Dense: 256 columns * 8 bytes = 2048 bytes (most columns are 0)
- Sparse (column_index, y_position pairs): ~256 * 2 bytes = 512 bytes
- Compression: 4x

For underground sections:
- Dense: 2048 bytes (all zeros - no visible faces)
- Sparse: 0 bytes (empty list)
- Compression: infinite (skip entirely)

### Implementation
- Store face masks in sparse format for sections with <50% non-zero columns
- Dense format for complex sections (caves, structures)
- GPU kernel accepts both via mode flag
- Sparse format enables fast "any visible?" check without scanning all 256 columns

---

## 5. Hierarchical Occupancy for Early-Out Culling

### What
TurboQuant-inspired: represent section occupancy at multiple resolutions for hierarchical culling.

### Hierarchy
```
Level 0: per-voxel (16x16x16 = 4096 bits = 512 bytes)
Level 1: per-2x2x2 (8x8x8 = 512 bits = 64 bytes)
Level 2: per-4x4x4 (4x4x4 = 64 bits = 8 bytes)
Level 3: per-section (1 bit)
```

### Culling Pipeline
1. Test Level 3 (1 bit): is section empty? Skip if so.
2. Test Level 2 (8 bytes) against frustum: which octants are visible?
3. Test Level 1 (64 bytes) for visible octants: which sub-octants?
4. Only decompress/mesh Level 0 data for the visible sub-regions.

For underground sections outside the player's view, culling terminates at Level 3 (1 bit check). For partially-visible surface sections, Level 2 eliminates 50-75% of the volume before any voxel data is touched.

Total overhead: 512 + 64 + 8 + 1 = 585 bytes per section. Saves potentially megabytes of bandwidth by avoiding decompression of invisible voxels.

---

## 6. Runtime Texture Atlas Packing via ILGPU

### What
Pack all UI images (item icons, status effects, minimap tiles) into a shared atlas at load time. One texture, one bind group, one draw call for all images.

### GPU-Accelerated Packing
The bin packing problem (fitting N rectangles into a 2D atlas) has a GPU-friendly heuristic:
1. Sort rectangles by height (descending) on CPU
2. Place rows using shelf algorithm on CPU (O(N))
3. Copy source images into atlas positions using a GPU copy kernel (parallel per-pixel)

ILGPU kernel: one thread per pixel of the destination atlas. Each thread looks up which source image covers its position and copies the texel. O(1) per pixel, fully parallel.

### Dynamic Expansion
Start at 1024x1024. When full, allocate 2048x2048, copy existing atlas content (GPU-to-GPU), continue packing. Existing UV coordinates remain valid (they're in the lower-left quadrant).

---

## Summary: Memory Budget on Quest 3S

| Data | Uncompressed | Compressed | Technique | Savings |
|------|-------------|-----------|-----------|---------|
| Block textures (100 types) | 75MB | 8.3MB | ASTC 6x6 | 67MB |
| Font atlas | 4MB | 1MB | ASTC 4x4 | 3MB |
| UI image atlas | 8MB | 1.3MB | ASTC 5x5 | 6.7MB |
| Voxel sections (200 visible) | 1.6MB | 0.4MB | Palette | 1.2MB |
| Vertex buffers (200 sections) | 43MB | 14MB | Compressed format | 29MB |
| Face masks (200 sections) | 0.4MB | 0.1MB | Sparse | 0.3MB |
| UI vertices (4096 quads/frame) | 0.75MB | 0.28MB | fp16 + unorm8 | 0.47MB |
| **Total** | **~133MB** | **~25MB** | | **~108MB saved** |

On an 8GB device running a browser, OS, and our app simultaneously, 108MB is the difference between smooth 72Hz and swap thrashing.
