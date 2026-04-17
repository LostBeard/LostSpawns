# Lost Spawns: Asset Pipeline

## Status Legend
- **[COMMIT]** settled design decisions
- **[LIKELY]** strong preference, expect to commit
- **[UNDECIDED]** open
- **[DEFER]** post-1.0
- **[REJECT]** explicitly not doing

---

## Premise

A browser game ships over the network. Every byte counts. Every texture decode cost matters. Every shader compile stutters frames. The asset pipeline is not a back-end concern - it is the hot path.

Lost Spawns will ship with a large asset library: voxel models, terrain textures, character clothing variants, weapon models, UI textures, sound effects, music, localized text. Modders will add more. The pipeline has to handle authoring, optimization, delivery, streaming, and runtime loading in ways that keep frame budget and memory budget intact across our 7 platform tiers (see PLAN-Performance-Targets).

This plan defines how assets move from a creator's machine to a player's GPU.

---

## Design Principles

### 1. Compression by Default

**[COMMIT]** All textures ship compressed (BC7/ASTC). Uncompressed textures are a build-time error. No exceptions.

**[COMMIT]** All audio ships in a compressed format (Opus for music, possibly WAV-encoded samples for hot-path SFX with tiny durations).

**[COMMIT]** All voxel models are encoded in a compact binary format (palette-indexed + run-length). Not JSON, not XML, not human-readable.

### 2. Streamed, Not Preloaded

**[COMMIT]** Assets load on demand. A player in the forest does not have the city textures in memory. Chunks stream in with their dependencies.

**[COMMIT]** Streaming is background, never blocking. A missing texture shows a placeholder (a default block, a question mark icon) for a few frames while it loads. Never a stall.

**[LIKELY]** Priority queues: in-view assets before out-of-view, HUD before world, audio-for-nearby-sound before audio-for-distant.

### 3. Cached Aggressively

**[COMMIT]** OPFS holds the asset cache. First download is slow; return visits pull from local cache.

**[COMMIT]** Cache is content-addressed. Each asset has a content hash. Modified assets have different hashes and download fresh. Never stale.

**[LIKELY]** Cache quota respects browser limits. Oldest/least-used assets evict when full.

### 4. CDN Delivery

**[COMMIT]** Assets are served from a CDN (content-addressed URLs, aggressive edge caching). Global delivery, low latency.

**[LIKELY]** Torrent-delivered for large assets (HD texture pack, regional asset bundles). SpawnDev.WebTorrent is already in our stack; use it.

### 5. Modder-Compatible

**[COMMIT]** The same pipeline mods use. Mods ship compressed assets through the same CDN / torrent / local install paths. No separate "mod asset pipeline" and "first-party asset pipeline."

---

## Asset Types

### Textures

**[COMMIT]** Source format: PNG (for artists). Alpha supported.

**[COMMIT]** Build pipeline compresses to:
- **Desktop/Quest**: BC7 (RGBA), BC5 (normal maps), BC4 (single-channel), BC6H (HDR)
- **Mobile/iOS**: ASTC 4x4 or 6x6 depending on importance
- **Fallback**: ETC2 for older Android

**[COMMIT]** Mipmap chain generated at build time. No runtime mipmap generation.

**[LIKELY]** Resolution tiers per texture:
- **Base**: 256x256 (for small block textures) up to 1024x1024 (for hero art)
- **HD pack** (optional): 2x resolution. Shipped as a separate downloadable.
- **Mobile-Low pack**: Half resolution. Shipped to low-end devices.

**[COMMIT]** Normal maps use BC5 (two-channel) with Z reconstruction in shader. ~33% smaller than full RGB normals.

**[LIKELY]** Texture arrays (not atlases) for chunked terrain rendering. See AubsCraft research docs for specifics.

### Voxel Models

**[COMMIT]** Source format: MagicaVoxel `.vox` files. Artists work in MagicaVoxel natively; our pipeline imports.

**[COMMIT]** Runtime format: compact binary (palette + RLE). Typical character model ~5-20 KB compressed.

**[LIKELY]** LOD variants auto-generated:
- LOD0: original voxel resolution
- LOD1: 2x downsampled
- LOD2: 4x downsampled (silhouette-only)
- LOD3: billboard (pre-rendered impostor)

**[LIKELY]** Animation frames for animated voxel entities. Keyframed skeletal rigs override individual voxel manipulation where appropriate.

### Terrain Chunks

**[COMMIT]** Generated procedurally from seed. Not pre-authored assets.

**[COMMIT]** Cached to OPFS as binary chunks (compressed with zstd). Re-generate if seed differs.

**[LIKELY]** Cave systems, structure placements, loot spawns are part of the chunk cache.

### Audio

**[COMMIT]** Music: Opus compressed, 96 kbps mono for ambient, 128 kbps stereo for themes. Streamed.

**[COMMIT]** SFX (short): 16-bit PCM or uncompressed Opus frames for lowest decode cost. Preloaded at the scene level.

**[COMMIT]** SFX (long): Opus compressed.

**[COMMIT]** Voice dialog: Opus compressed.

**[LIKELY]** Localized audio: each locale has its own audio bundle. Only the selected locale streams.

### Fonts

**[COMMIT]** Runtime-rendered text uses SpawnScene's FontAtlas (GPU-rendered UI). Font source: TTF files, decoded at build time into signed-distance-field (SDF) atlases.

**[LIKELY]** One primary font + a monospace for code-adjacent UI + localized glyphs per supported language. See PLAN-Localization-I18n (not yet written).

### Localized Text

**[COMMIT]** Text strings in .json / .po / .toml files per locale. Structured key-value. No hard-coded strings in game code.

**[LIKELY]** Localized text bundles ship separately; only the player's selected locale downloads.

### Shaders

**[COMMIT]** WebGPU WGSL source shipped alongside compiled artifacts. Runtime compiles on first use with precomputed caches.

**[LIKELY]** Shader variants precomputed at build time - all permutations we know we need. No runtime recompile pauses for common paths.

---

## Build Pipeline

### The Toolchain

**[COMMIT]** Build pipeline is C#-based, using SpawnDev tools. Not Python, not Node, not proprietary authoring apps beyond the source editors.

**[LIKELY]** Entry point: a CLI (`LostSpawns.AssetBuild`) that takes an asset directory, runs the pipeline, outputs ready-to-ship artifacts.

**[COMMIT]** Pipeline is deterministic: same inputs = same outputs. Byte-identical across runs. Enables content-addressed caching.

### Pipeline Stages

1. **Ingest** - read source asset (PNG, VOX, TTF, etc.)
2. **Validate** - check dimensions, format, naming conventions
3. **Transform** - resize to tier resolution, generate mipmaps, compress
4. **Compress** - format-specific compression (BC7, ASTC, Opus, zstd)
5. **Hash** - compute content hash of final binary
6. **Manifest** - write entry to asset manifest
7. **Publish** - upload to CDN / create torrent info hash

**[LIKELY]** Stages can run in parallel for independent assets. Cache invalidation: if input unchanged (by source hash), skip the stages.

### Incremental Builds

**[COMMIT]** Incremental: only changed assets rebuild. Source hash stored; compared to previous build.

**[LIKELY]** Partial-dependency tracking: a texture atlas depends on its input textures; touching an input invalidates the atlas. Similar for voxel-model LODs.

### Output Manifest

**[COMMIT]** Every build produces a manifest: JSON listing every asset, its content hash, its CDN URL, its torrent info hash (if applicable), its compression format, its platform applicability.

**[COMMIT]** The game fetches the manifest on startup. Knows what assets exist and how to get them.

---

## CDN Delivery

### URL Structure

**[LIKELY]** `https://cdn.spawndev.com/lost-spawns/assets/{hash}.{ext}`

- `{hash}` is the content hash (BLAKE3 or SHA-256 truncated)
- Extension preserved for debugging clarity
- Long-cacheable: content-addressed means never needs invalidation

**[LIKELY]** Cache headers: `Cache-Control: public, max-age=31536000, immutable`. One year + immutable. CDN + browser caches indefinitely.

### CORS

**[COMMIT]** CDN serves with CORS headers permitting any origin. Allows the game to run from multiple hosts (demo at demo.spawndev.com, self-host at any-other-domain, etc.).

**[COMMIT]** COOP / COEP compatible for SharedArrayBuffer access.

### Fallback CDN

**[LIKELY]** Primary CDN + secondary fallback. Game tries primary first, falls back on failure. Both content-addressed so caching stays clean.

**[LIKELY]** Community-mirrored CDN. Volunteers can run mirrors; the manifest can point to multiple URLs per hash. Players can configure their preferred mirror.

### Bandwidth Costs

**[LIKELY]** CDN bandwidth is our primary infrastructure cost post-launch. Scales with active players. Budget carefully.

**[LIKELY]** Cost mitigation strategies:
- Aggressive browser caching (one-year immutable)
- Torrent distribution for large content (peers help each other download)
- Compression - every byte avoided is a byte saved 100x
- Regional CDN tiering (paid CDN close to user, fallback cheaper CDN far)

---

## Torrent Distribution

### When to Use Torrent

**[LIKELY]** Torrent-delivered assets:
- HD texture pack (large, popular, cacheable for long time)
- Total-conversion mods (very large, niche audience)
- Regional asset bundles for seasonal events (many players want same large content at same time)
- World snapshots (peer-sharing world state, see PLAN-Networking-Multiplayer)

**[LIKELY]** Torrent not for small assets (< 10 MB) - CDN latency beats torrent discovery latency.

### Integration

**[COMMIT]** SpawnDev.WebTorrent handles torrent delivery. Already proven in our stack.

**[COMMIT]** Torrent info hashes stored in asset manifest. Game fetches manifest, then uses torrent to download large assets. Verification via content hash.

**[LIKELY]** Initial seeders: our servers. As players cache, they become seeders too. Peer-to-peer amplifies our bandwidth for popular content.

---

## Runtime Loading

### Streaming Strategy

**[COMMIT]** Assets stream in as needed. Pipeline:
1. Game detects need (player approaches an area, enters a UI menu, equips an item)
2. Check OPFS cache - is it there?
3. If not, start download (parallel CDN fetches for small, torrent for large)
4. Once downloaded, decompress (off-main-thread via worker)
5. Upload to GPU (for textures/models) or decode (for audio)
6. Mark ready, notify waiters

**[COMMIT]** Waiters can proceed with placeholders if ready is not imminent. Player sees a "missing texture" block briefly rather than a stall.

### Prefetching

**[LIKELY]** Prefetch heuristics:
- Player's current view: high priority
- Player's direction of motion: prefetch chunks ahead
- Nearby audio sources: prefetch sounds
- Menu the player is hovering: prefetch UI art

**[LIKELY]** Prefetch never blocks. Budget-limited.

### Decompression

**[COMMIT]** BC7 / ASTC decompress on GPU (hardware-accelerated, zero CPU cost).

**[COMMIT]** zstd decompression runs on a dedicated worker thread. Main thread never blocks on decompression.

**[LIKELY]** Opus decode runs on a dedicated audio worker.

### GPU Upload

**[COMMIT]** GPU texture uploads use WebGPU's async upload queue. No main-thread blocking.

**[LIKELY]** Texture upload budget: cap at ~8 MB per frame (tunable). Prevents upload-heavy frames from missing render deadline.

**[LIKELY]** Buffer compaction / defragmentation (see AubsCraft research) handles the case where many small uploads leave buffer fragmented.

### Eviction

**[COMMIT]** Per-tier memory caps (see PLAN-Performance-Targets). When approaching cap, evict least-recently-used assets.

**[LIKELY]** Eviction respects "pinned" assets (UI art, commonly used SFX). Pinned assets are always in memory.

**[LIKELY]** Eviction is speculative: if the evicted asset is needed again soon, it re-streams from local OPFS cache (fast) rather than CDN.

---

## Modder Workflow

### For Asset-Only Mods (Texture Packs)

**[COMMIT]** Modder:
1. Opens our asset-pack template in their preferred image editor
2. Creates replacement PNGs
3. Runs the asset-pack build tool
4. Output is a `.zip` or signed archive
5. Uploads to the Mod Hub (see PLAN-Modding-Plugin-System)

**[LIKELY]** No code written. Pure art. Signed manifest.

### For Mods With New Assets

**[COMMIT]** Mod SDK includes asset pipeline hooks. Modder:
1. Creates PNG/VOX/Opus source files
2. Registers them in mod manifest
3. Runs mod build
4. Mod package contains pre-compressed assets (same formats as base game)
5. Publishes to Hub

**[COMMIT]** Modder assets go through the same build pipeline with the same quality/format requirements. Consistency.

### Asset Format Compatibility

**[COMMIT]** All mods ship in the same formats the base game uses. No mod-only format extensions. No version-skew.

**[LIKELY]** If we update our base formats (e.g., add support for a new compression format), old mods continue to work (we keep decoders for legacy formats). New mods can opt into the new formats.

---

## Quality Control

### Texture QA

**[LIKELY]** Build pipeline catches:
- Non-power-of-2 dimensions (warn, or reject based on intended use)
- Transparency errors (straight alpha vs premultiplied)
- Oversized source files (nudge the artist)
- Missing mipmaps / incorrect aspect ratios
- Color profile issues (sRGB vs linear)

**[LIKELY]** Visual regression testing for critical art: reference renders of key scenes. CI detects unintended visual changes.

### Voxel QA

**[LIKELY]** Build pipeline catches:
- Asymmetric models that should be symmetric (flag)
- Missing LOD variants (auto-generate)
- Out-of-palette colors (normalize to chosen palette)
- Empty voxels in interior (optimize)

### Audio QA

**[LIKELY]** Build pipeline catches:
- Clipping (peak > 0 dB)
- Wildly inconsistent loudness (flag for LUFS normalization)
- Silent leading/trailing (trim or warn)
- Wrong sample rate (resample)

### Performance QA

**[LIKELY]** Budget check: total asset bundle size per platform tier. If low-end pack exceeds budget, flag.

---

## Localization Handling

See PLAN-Localization-I18n (not yet written). Asset-pipeline-specific notes:

**[LIKELY]** Text strings in structured files per locale.

**[LIKELY]** Locale-specific fonts (Chinese, Japanese, Korean glyphs in separate bundles).

**[LIKELY]** Voice audio per supported locale (if we do multi-lingual voice; English-only likely for 1.0 with text-only localization).

**[LIKELY]** Locale bundles ship separately; only the active locale downloads.

---

## Accessibility Assets

See PLAN-Accessibility for the full accessibility design. Asset-pipeline-specific notes:

**[COMMIT]** Alternate textures for colorblind modes shipped as part of the base asset pack. Switched by setting, not a separate download.

**[COMMIT]** Large-text font variant pre-rendered. Selectable without performance cost.

**[LIKELY]** Haptic audio substitutes: some audio cues have haptic-pattern equivalents for VR controller. Ship as haptic pattern data, tiny.

---

## Security

### Tamper Resistance

**[COMMIT]** Content-addressed URLs mean the game verifies every downloaded asset against the expected hash. Tampered assets are rejected.

**[COMMIT]** Mod assets signed by modder. Tampered mods = invalid signature = rejected.

### No Remote Code in Assets

**[COMMIT]** Asset formats are strictly data (no embedded code). PNG, Opus, voxel binary - none are Turing-complete. A malicious asset file cannot execute code.

**[LIKELY]** Any future formats that could contain logic (say, a shader file) get special handling: signed, sandboxed.

### Privacy

**[COMMIT]** Asset fetches do not include identifying headers beyond what the browser naturally sends. We do not track which player fetched which asset.

**[COMMIT]** CDN telemetry (request counts per asset) is aggregated and anonymized.

---

## Deliverables for 1.0

1. Build pipeline CLI (`LostSpawns.AssetBuild`)
2. PNG -> BC7/ASTC/ETC2 per-tier compression
3. MagicaVoxel VOX -> compact binary runtime format
4. Font TTF -> SDF atlas generation
5. Audio -> Opus compression with correct bitrate tiers
6. Mipmap generation
7. LOD variant auto-generation for voxel models
8. Content-addressed output + manifest
9. Deterministic builds (byte-identical across runs)
10. Incremental builds (skip unchanged)
11. CDN delivery with long-cache immutable headers
12. Torrent delivery via SpawnDev.WebTorrent for large assets
13. OPFS asset cache with eviction
14. Off-main-thread decompression workers
15. Streamed loading with placeholders
16. Prefetch heuristics (view direction, nearby audio)
17. Mod SDK integration (same pipeline for mods)
18. Quality checks (dimensions, formats, clipping, budget)
19. Localization bundling

---

## Open Questions

**[UNDECIDED]** Cloud build for modders: modders upload source, our servers build compressed artifacts. Pro: modders don't need the toolchain installed. Con: infrastructure cost, CI infra. Maybe post-1.0 community service.

**[UNDECIDED]** WebGL2 fallback asset formats: WebGL2 does not support BC7. Would need ETC2 or DXT fallback. Depends on how much we target WebGL2 - leaning toward small or minimal coverage.

**[UNDECIDED]** HDR textures for high-end tiers. BC6H exists but pipeline complexity. Worth it?

**[UNDECIDED]** Procedural texture augmentation at runtime: slight variation per-instance (different moss, different rust). Could add variety without asset-count explosion. Implementation cost: compute-shader augmentation at chunk gen.

**[UNDECIDED]** Ray-tracing BVH bundles as assets. If we add RT, we need pre-built acceleration structures. Additional asset category.

---

## Relationship to Other Plans

- **PLAN-Performance-Targets** - memory/bandwidth budgets flow from here
- **PLAN-Networking-Multiplayer** - torrent-delivered world snapshots use this pipeline
- **PLAN-Modding-Plugin-System** - mods ship through this pipeline
- **PLAN-Accessibility** - alternate assets for accessibility modes
- **PLAN-Localization-I18n** (not yet written) - localized text + audio bundles
- **PLAN-UI-HUD** - UI textures and SDF fonts flow through here
- **PLAN-Audio-Design** - audio formats defined
- **PLAN-Terrain-Carving** - chunk cache format defined
