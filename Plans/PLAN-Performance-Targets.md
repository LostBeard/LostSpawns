# Lost Spawns: Performance Targets & Budgets

## Status Legend
- **[COMMIT]** settled design decisions
- **[LIKELY]** strong preference, expect to commit
- **[UNDECIDED]** open
- **[DEFER]** post-1.0
- **[REJECT]** explicitly not doing

---

## Premise

Performance is the feature, not a feature. TJ's global rule #4 is the project north star:

> "We are not building 'good enough' libraries. Performance is not a feature - it is THE feature. Every decision should be evaluated through the lens of 'does this make the engine faster?'"

This document sets the numeric targets. Every other plan in Lost Spawns has to fit inside these budgets. A feature that cannot fit gets cut, simplified, or scoped to high-end platforms only.

The thesis we are proving: a Blazor WASM + WebGPU + ILGPU game can match or beat native for this genre. If we ship a 45fps game on Quest 3S, we have failed the thesis. We target 90fps on Quest 3S, 120fps on desktop, and a smooth 60fps floor on mobile touch play.

---

## Platform Tiers

| Tier | Reference device | Target FPS | Draw distance | Quality preset |
|---|---|---|---|---|
| **VR-High** | Quest 3 / 3S, Valve Index, Vision Pro | 90 Hz | 500m | High-VR |
| **VR-Low** | Quest 2, Pico 4 | 72 Hz | 300m | Low-VR |
| **Desktop-High** | RTX 3060 or better | 120+ Hz | 1000m | Ultra |
| **Desktop-Mid** | GTX 1060, M1 Mac, Steam Deck | 60+ Hz | 600m | Medium |
| **Desktop-Low** | Integrated graphics, old laptops | 60 Hz | 300m | Low |
| **Mobile-High** | iPhone 14+, flagship Android | 60 Hz | 400m | Mobile-High |
| **Mobile-Low** | 3-year-old phones, Chromebook | 30 Hz floor | 200m | Mobile-Low |

**[COMMIT]** We ship on all 7 tiers. VR-High and Desktop-High are showcase. VR-Low, Desktop-Mid, Desktop-Low, Mobile-High, Mobile-Low keep the game accessible to people who cannot afford top hardware.

**[COMMIT]** Auto-detect on first launch picks a sensible tier from GPU/CPU/memory. Player can override.

**[COMMIT]** Tier does not gate content. Everything in the world exists on all tiers. Only fidelity varies - draw distance, shadow detail, texture resolution, NPC density, particle counts.

---

## Frame Budget Per Tier

### VR-High (Quest 3S @ 90Hz)

**[COMMIT]** Total frame budget: 11.1ms (absolute cap to maintain 90Hz with headroom).

| Pass | Budget (ms) | Notes |
|---|---|---|
| Input + simulation | 1.5 | Physics, AI, player state |
| Chunk streaming + mesh build | 0.8 | Amortized across frames |
| Culling (frustum + occlusion) | 0.5 | Compute shader |
| Shadow map | 1.2 | 2048 cascade, low detail |
| Main pass (both eyes) | 4.5 | Stereo instanced |
| Transparency + particles | 0.8 | Limited particle budget |
| UI + HUD | 0.5 | GPU-rendered, no HTML overlay |
| Post-FX | 0.8 | AO, grading, vignette |
| Present + reprojection | 0.5 | |
| **Headroom** | **0.0** | Nothing to spare |

**[LIKELY]** If we cannot hit 11.1ms, we drop draw distance before dropping framerate. Sim-sickness tolerance is zero.

**[LIKELY]** Foveated rendering in Quest reduces shading cost by ~25-35% at peripheral vision. Enabled by default.

**[LIKELY]** Space Warp (Meta's ASW equivalent) enabled as fallback. Target engine running 45Hz with ASW filling to 90 displayed. Tested but not the design target.

### VR-Low (Quest 2 @ 72Hz)

**[COMMIT]** Frame budget: 13.8ms. Aggressively reduced fidelity. Draw distance 300m. Shadow map 1024. NPCs at 25% density of VR-High. Particles simplified.

### Desktop-High (RTX 3060+)

**[COMMIT]** Frame budget: 8.3ms at 120Hz. This tier is the showcase. Everything on. Full dynamic lighting, high-detail shadows, 1000m draw distance, full NPC density.

**[LIKELY]** Ray-traced reflections and shadows: OPTIONAL, off by default, player opt-in. Performance-conscious by default.

### Desktop-Mid (GTX 1060, M1 Mac, Steam Deck)

**[COMMIT]** Frame budget: 16.6ms at 60Hz. Mid quality preset. 600m draw. 2048 shadow. Medium NPC density. Full voxel detail.

### Desktop-Low (Integrated graphics)

**[COMMIT]** Frame budget: 16.6ms at 60Hz. Low preset. 300m draw. 1024 shadow. Simplified NPC AI (coarser tick rate at distance). Fewer particles.

### Mobile-High (iPhone 14+, flagship Android)

**[COMMIT]** Frame budget: 16.6ms at 60Hz. Mobile-High preset. 400m draw. 1024 shadow. Reduced shader complexity. Battery-mode option caps at 30fps.

### Mobile-Low (3-year-old phones)

**[COMMIT]** Frame budget: 33.3ms floor at 30Hz. Visibly simpler - less foliage, simpler terrain normal maps, shorter draw distance, no post-FX except tone mapping.

---

## Memory Budget

### GPU Memory

| Tier | Target | Cap |
|---|---|---|
| VR-High | 2 GB | 3 GB |
| VR-Low | 1 GB | 1.5 GB |
| Desktop-High | 4 GB | 8 GB |
| Desktop-Mid | 2 GB | 4 GB |
| Desktop-Low | 1 GB | 2 GB |
| Mobile-High | 1 GB | 1.5 GB |
| Mobile-Low | 512 MB | 768 MB |

**[COMMIT]** Chunks are streamed in and out based on camera distance. Far chunks unload aggressively. We keep a working set that fits in the target, not the cap.

**[COMMIT]** Texture arrays with BC7 / ASTC compression (platform-appropriate). Uncompressed textures are rejected by the asset pipeline (see PLAN-Asset-Pipeline - not yet written).

**[LIKELY]** LOD system with 4 tiers. Closest: full detail. Far: greedy-meshed, reduced texture detail. Very far: silhouette-only. See AubsCraft research docs.

### System Memory (WASM heap)

| Tier | Target | Cap |
|---|---|---|
| VR-High | 1 GB | 2 GB |
| VR-Low | 512 MB | 1 GB |
| Desktop-High | 2 GB | 4 GB |
| Desktop-Mid | 1 GB | 2 GB |
| Desktop-Low | 512 MB | 1 GB |
| Mobile-High | 512 MB | 1 GB |
| Mobile-Low | 256 MB | 512 MB |

**[COMMIT]** Blazor WASM heap is a real constraint. Browser tabs have per-origin memory limits; we respect them. GC pressure is the enemy of frame pacing.

**[COMMIT]** Object pooling for hot-path allocations: projectile trails, damage numbers, particle lives, audio sources. No new-in-the-tight-loop. Zero-copy from networking layer to simulation state.

**[LIKELY]** WASM64 support on platforms that allow it (Chromium with flags today, universal tomorrow). Gets us past the 4GB 32-bit WASM limit.

---

## Bandwidth Budget

See PLAN-Networking-Multiplayer for the full table. Summary:

| Session size | Expected | Cap |
|---|---|---|
| Squad (2-8) | 30-50 KB/s | 100 KB/s |
| Town (8-32) | 80-150 KB/s | 300 KB/s |
| Region (32-100) | 150-400 KB/s | 1 MB/s |
| City event (100+) | 500 KB/s - 2 MB/s | 4 MB/s |

**[COMMIT]** Mobile-Low tier caps at 200 KB/s regardless of session size. Interest-culling is more aggressive to stay in budget.

**[COMMIT]** Chunk streaming over WebTorrent swarm does not count against the live-session cap. Streaming is background; live state is foreground.

---

## Startup Time

**[COMMIT]** Cold load (no cache, first visit) target:
- Main menu visible: under 5 seconds
- First frame of gameplay (after clicking Play): under 15 seconds for a known-seed world, under 60 seconds for a new world requiring full snapshot download

**[COMMIT]** Warm load (returning player, same world): under 8 seconds to gameplay.

**[COMMIT]** Blazor WASM AOT-compiled for production. No interpreted code in hot paths. Runtime is pre-warmed via Service Worker during idle browser time.

**[LIKELY]** Progressive loading: start the world with low-LOD placeholder chunks so the player can move in 5 seconds, then stream in full detail. Minecraft's "distance fog rolls back" pattern.

**[LIKELY]** Shader precompile on first launch. Takes a few extra seconds on the first load; avoids the hitchy "first time a new object appears, shader compiles, frame stutters" problem.

---

## Load & Hitch Tolerance

**[COMMIT]** Zero frame-time spikes during gameplay above the platform's frame budget. A hitch > 1 frame = a bug, not a "feature."

**[LIKELY]** Chunk load is amortized: each frame spends at most 0.8ms on mesh build, regardless of how many chunks are queued. Queue drains over time; player never sees a hitch.

**[LIKELY]** Texture streaming uses async decode + upload. A new texture takes a few frames to appear; it never blocks the frame.

**[LIKELY]** Network updates process off-frame: a worker thread decodes + validates, main thread consumes a stable snapshot each frame.

---

## Specific Subsystem Budgets

### Voxel Rendering

**[LIKELY]** Vertex budget:
- VR-High: 2 million vertices in view (draws)
- Desktop-High: 5 million
- Desktop-Mid: 2 million
- Desktop-Low: 1 million
- Mobile-High: 1 million
- Mobile-Low: 500 thousand

**[LIKELY]** Draw calls: 1-5 per chunk family (indirect draw, massive batching). No more than 50 draw calls total for terrain.

**[COMMIT]** Greedy meshing on every chunk. Binary greedy meshing preferred (80-95% polygon reduction). See AubsCraft research.

**[COMMIT]** Cave culling: 15-bit flood fill. 50-99% of underground geometry culled. See AubsCraft research.

**[COMMIT]** GPU frustum culling + compute-shader occlusion. CPU does zero per-chunk visibility work.

### NPC & Entity Simulation

| Tier | Active NPC cap (visible) | Ambient wildlife cap |
|---|---|---|
| VR-High | 60 | 30 |
| VR-Low | 30 | 15 |
| Desktop-High | 150 | 80 |
| Desktop-Mid | 80 | 40 |
| Desktop-Low | 40 | 20 |
| Mobile-High | 40 | 20 |
| Mobile-Low | 20 | 10 |

**[COMMIT]** NPC AI has distance-based tick rates (see PLAN-Networking-Multiplayer for network; same principle for local sim). Nearby NPCs tick at 30Hz, mid-distance at 10Hz, distant at 2Hz.

**[LIKELY]** AI behavior trees run on GPU via SpawnDev.ILGPU for bulk evaluations (pathfinding candidates, threat scoring). Proven pattern; keeps CPU free.

### Physics

**[LIKELY]** Rigid body count cap (physics-active at once):
- VR-High: 50
- Desktop-High: 200
- Lower tiers: 20-100 scaled

**[COMMIT]** Physics runs at a fixed 60Hz tick rate, decoupled from render. Render interpolates between physics steps. Prevents framerate from affecting game feel.

**[LIKELY]** Voxel collision is SDF-based (not per-voxel). A chunk's SDF is built once per edit and queried per-body per-tick.

### Audio

**[COMMIT]** Audio voice cap:
- Mobile-Low: 16 concurrent voices
- Mobile-High / VR-Low: 32
- Others: 64

**[COMMIT]** Voices beyond the cap are virtualized (simulation continues, but no audio output). Virtual voices can reclaim their slot when a real voice ends.

**[LIKELY]** 3D spatial audio for all positional sources. HRTF-based where platform supports; fallback to simpler panning on mobile.

### Networking

See PLAN-Networking-Multiplayer. Tick rates already specified there.

---

## Testing & Benchmarking

**[COMMIT]** Automated frame-time capture in every PlaywrightMultiTest run. A commit that regresses median frame time by more than 5% is flagged.

**[COMMIT]** Benchmark scenes:
1. Empty plains (baseline, chunks only)
2. Dense forest (foliage stress)
3. City ruins (polygon + shadow stress)
4. 50-NPC combat (AI + physics stress)
5. Indoor base with lighting (many dynamic lights)
6. Rain + fog over forest (weather stress)

**[COMMIT]** Each benchmark runs on every tier in CI. Target framerate must hit on each tier. Failure = block merge.

**[LIKELY]** Public benchmark page: the game ships with a "benchmark my machine" button that runs the 6 scenes and reports FPS + recommended tier. Lets players know what to expect.

**[LIKELY]** Telemetry (opt-in): players can opt into sending perf data. Helps us catch regressions on hardware we do not test directly. Fully anonymous, off by default.

---

## Profiling Infrastructure

**[COMMIT]** In-game F3 overlay: frame time graph, CPU/GPU breakdown, memory usage, network stats, chunk queue depth. Standard game-dev HUD. Shipping-enabled.

**[COMMIT]** Deep profile mode: a dedicated profile build captures per-subsystem frame timings in ring buffers. Export as JSON for Chrome DevTools Performance panel.

**[LIKELY]** WebGPU timestamp queries for GPU-side profiling. Browser support is uneven but growing. Use where available.

**[LIKELY]** ILGPU kernel profiling: TJ's existing tooling captures kernel runtimes. Surface in the F3 overlay.

---

## Platform-Specific Concerns

### WebGPU Availability

**[COMMIT]** Primary graphics API: WebGPU. Available on Chromium (stable), Safari (stable as of macOS 14 / iOS 17 Safari 17.4), Firefox (nightly + flags today, stable soon).

**[LIKELY]** Fallback: WebGL2 for older browsers. Reduced feature set - no compute shaders, no indirect draw, no storage buffers. Performance target: match Mobile-Low tier on WebGL2 fallback.

**[UNDECIDED]** Do we keep WebGL2 fallback forever, or sunset it when WebGPU is universal? Revisit late 2026 when Firefox WebGPU ships stable.

### WASM Threads

**[COMMIT]** We use Web Workers via SpawnDev.BlazorJS worker support. Networking decode, chunk mesh build, and asset streaming happen off-main-thread.

**[COMMIT]** Shared memory (SharedArrayBuffer) requires COOP/COEP headers. All our hosted builds serve these headers. Self-hosted dedicated server builds document the requirement.

**[LIKELY]** Number of worker threads: tier-scaled, typically (CPU cores - 1). Main thread handles render + input; workers handle everything else.

### iOS Quirks

**[LIKELY]** iOS Safari memory pressure is real. We target 512MB working set on iPhone 14. Background tabs get killed aggressively; we handle re-hydration via OPFS.

**[LIKELY]** PWA install improves iOS behavior. We prompt for "Add to Home Screen" after the first session for committed players.

**[LIKELY]** iOS WebXR: not yet available. iPhone + Quest ecosystem doesn't overlap, so no WebXR fallback path; iOS players use touch/flat-screen only for 1.0.

### Android Quirks

**[LIKELY]** Android WebGPU in Chrome is still rolling out. Earlier devices may force WebGL2.

**[LIKELY]** Android has wider memory variance. Allow runtime tier downgrade if memory pressure signals fire.

### macOS / iPadOS

**[LIKELY]** Apple Silicon (M1+) is absurdly fast for our use case. Treat M-series MacBooks as Desktop-High tier.

**[LIKELY]** iPadOS: works as Mobile-High or Desktop-Mid depending on model. iPad Pro can handle Desktop-Mid; older iPads are Mobile-High.

### Chromebooks

**[LIKELY]** ARM-based or Intel N-series Chromebooks: Mobile-Low or Desktop-Low depending. Auto-detect based on GPU and memory.

**[LIKELY]** Chromebook support is a point of pride. Kids in school with only a Chromebook should be able to play Lost Spawns on their own device. Accessibility is a feature.

---

## Optimization Patterns We Already Have

From the SpawnDev stack:

**[COMMIT]** SpawnDev.ILGPU: GPU compute with 6 backends. We push as much logic to GPU as makes sense.

**[COMMIT]** SpawnDev.ILGPU.ML: ONNX / transformer inference if we ever add ML features (dynamic dialogue, behavior coaching). Not in 1.0 scope but available.

**[COMMIT]** SpawnDev.ILGPU.P2P: distributed GPU compute over WebRTC. Cross-peer GPU work for swarm-wide events (mega-events, region-wide effects).

**[COMMIT]** DelegateSpecialization: one kernel handles multiple operations. Required pattern for shader-heavy work.

**[COMMIT]** CanvasRendererFactory: zero-copy GPU-to-canvas. UI and HUD use this; no pixel readback to CPU.

**[COMMIT]** SpawnDev.BlazorJS: typed JS interop. No raw JS calls; everything is AOT-compilable and optimized.

**[COMMIT]** OPFS for local storage. No IndexedDB hot paths (too slow for hot state).

**[LIKELY]** SpawnDev.BackgroundServices: async service startup pipeline. Ensures we pre-warm critical subsystems while the player is still on the main menu.

---

## What Breaks The Budget

Be loudly aware of these cost traps. Any feature proposal that needs these must come with a plan to fit.

1. **New dynamic lights beyond 4 simultaneous.** Shadow cascades are fixed cost. Each new shadow-casting light is expensive.
2. **Transparency sorting across large object counts.** Particles use additive blending or OIT; no sorted-alpha cascades.
3. **New CPU-to-GPU data transfers per frame.** Every new GPU upload slot is a few hundred microseconds. Budget them.
4. **Per-entity physics raycasts.** Bulk raycasts run on GPU via ILGPU; individual per-entity CPU raycasts are forbidden in the hot path.
5. **Large per-frame allocations.** Every GC is a hitch. Pool or preallocate.
6. **Shader recompiles.** Each new shader variant is a first-use hitch. Pre-compile all variants during startup.
7. **UI rebuild from scratch each frame.** Retained GPU-rendered UI only. See PLAN-UI-HUD.

---

## Deliverables for 1.0

1. Tier auto-detect + per-tier quality presets
2. Benchmark scenes + CI integration
3. F3 perf overlay with frame time, GPU/CPU split, memory, network
4. LOD system with adaptive vertex budget (from AubsCraft)
5. Greedy + binary greedy meshing
6. Cave culling (flood fill)
7. GPU frustum + occlusion culling
8. Chunk streaming with amortized per-frame budget
9. Object pooling for hot paths
10. Shader precompile on startup
11. Platform-appropriate texture compression
12. Memory pressure handling + graceful degrade

---

## Open Questions

**[UNDECIDED]** Dynamic resolution scaling. Many games reduce internal resolution to maintain framerate. Good for VR especially. How aggressive should we be? Player visibility of the scaling (or not)?

**[UNDECIDED]** Variable rate shading where supported. Quest 3 has VRS. Potential ~10-15% win. Worth the engine complexity?

**[UNDECIDED]** Mesh shaders (WebGPU proposal, not yet shipping). Massive wins for voxel rendering. Design-ahead for when they land?

**[UNDECIDED]** How hard do we lean on WASM SIMD? Our existing code uses it where it helps; any pervasive SIMD path audit?

**[UNDECIDED]** Async compute queues (WebGPU allows it, browsers don't all expose it). Simulation + render could parallelize better. Investigate once browser support stabilizes.

---

## Relationship to Other Plans

- **PLAN-Vision** - the thesis this plan quantifies
- **PLAN-VR-Controls** - frame budget for VR flows from here
- **PLAN-Networking-Multiplayer** - bandwidth + tick-rate budgets live in both docs; this one owns the numbers
- **PLAN-UI-HUD** - UI cost fits in the UI pass budget line
- **PLAN-Infected-AI** - NPC AI cost comes from the NPC budget line
- **PLAN-Audio-Design** - voice cap is here
- **PLAN-Terrain-Carving** - vertex + draw call budgets are here
- **PLAN-Asset-Pipeline** (not yet written, Tuvok TODO) - texture compression + LOD authoring fits budgets defined here
- **PLAN-Modding-Plugin-System** (not yet written, Tuvok TODO) - plugins must fit the same budgets; sandboxing is a separate concern
