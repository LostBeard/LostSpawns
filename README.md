# Lost Spawns

A voxel-based 3D survival game built entirely in **Blazor WebAssembly** — showcasing the power of [SpawnDev.BlazorJS](https://github.com/LostBeard/SpawnDev.BlazorJS) and [SpawnDev.ILGPU](https://github.com/LostBeard/SpawnDev.ILGPU) for GPU-accelerated rendering and compute in the browser.

## 🎮 Overview

A **WIP**, Lost Spawns is a post-apocalyptic survival game inspired by DayZ. It runs entirely client-side in your web browser using WebGPU for hardware-accelerated 3D rendering and GPU compute - no plugins, no native code, just C# and Blazor.

The scope is intentionally ambitious: a full DayZ-scale persistent open world with moldable terrain, dynamic weather, hybrid smooth + blocky environments, flood-fill lighting, and Quest 3S VR native support. All from a single codebase running on WebGPU, WebGL, or Wasm depending on the browser. This is a showcase of what Blazor WASM + SpawnDev.ILGPU can do when pushed hard.

## ✨ Features (Shipped)

### GPU Pipeline (SpawnDev.ILGPU + WebGPU)
- **GPU Terrain Generation** — Perlin noise heightmap computed on the GPU via ILGPU compute kernels
- **GPU Mesh Generation** — Voxel face culling and vertex emission done entirely on the GPU with atomic counters via SpawnDev.VoxelEngine binary greedy meshing
- **GPU→GPU Buffer Copies** — Sub-view peer-to-peer copies for efficient vertex data transfer
- **WebGPU Render Pipeline** — Custom WGSL shaders with dual-light system, distance fog, and per-block color variation

### Rendering
- **Infinite Voxel Terrain** — Procedurally generated, streamed in real-time as you explore
- **Face-Dependent Block Colors** — Grass is green on top, earth-toned on sides
- **Distance Fog** — Smooth quadratic falloff blending terrain into the sky
- **Frustum Culling** — Only visible chunks are drawn
- **Free-List Vertex Buffer** — Sub-allocated GPU buffer with slot reuse and coalescing (zero-stall grow via GPU copy)
- **Day/Night Cycle** — Sun, moon, stars, dawn/dusk transitions, golden-hour tint, moonlight tint, mist, fireflies, falling leaves
- **Weather** — Rain, lightning + delayed thunder, storm gloom, wind ambient, rainwater collection
- **HUD** — GPU-rendered via SpawnDev.GameUI (no HTML overlays on canvas) - status bars, hotbar, minimap, debug line, toasts, achievements panel, pause/settings, loading screen with tips

### Survival Gameplay
- **Stats** — HP, hunger, thirst, stamina, temperature; hunger/thirst gate stamina regen, sprint drains thirst, hypothermia + heatstroke
- **Combat** — Melee (axe/pick), bow with feather arrows, sneak attack 1.75x bonus, bleed status, knockback
- **Wildlife** — Rabbit, boar, crow, deer, wolf, bear; pack behavior, herd panic, charging predators, wounded flee
- **Day-night escalation** — Wolves spawn after dark, density + HP scale per day, dawn break-off
- **Crafting** — C key, level-gated recipes, tier 2 stone tools, fur coat, bandage, torches, campfire, bonfire
- **Campfires** — Fuel decay, auto-cook raw meat, warmth aura, sleep-to-dawn, mercy fire on respawn
- **Inventory** — I key, hotbar (1-9), drag-drop, drop (Q), quick-eat (G), quick-drink (T), quick-bandage (H)
- **Save/Load** — 10s autosave, F5 manual save, world-diff persistence (chops + placements + carves), tab-out save
- **Achievements** — 16+ achievements, +25 XP each, J overlay, golden flash, immediate save on earn
- **NMS-style terrain carving** — B toggles terraform, sphere brush (LMB carve / RMB build), wheel adjusts radius, builds with active-slot block, costs stamina + grants XP
- **Audio** — Web Audio synthesis: footsteps, combat hits, wolf howls, crows, rain, fire crackle, thunder, heartbeat at low HP, level-up chime, achievement chime, master volume, M to mute

## 🌍 The Vision

> "The browser's first Astroneer-scale moldable-terrain DayZ."

What Lost Spawns is growing into:

### 🏔️ Moldable Terrain (The Headliner)

Not just digging. **Sculpting.** The world is clay. Push and pull the ground in real time with a terrain tool. Flatten a hillside into a base plot. Raise a defensive berm. Carve a road up a mountainside. Dig a foxhole mid-firefight and duck into it. Dig now, fill later - material from your canister.

Built on GPU-evaluated Signed Distance Fields + Dual Marching Cubes. Smooth organic deformation, no voxel staircase. Astroneer-class gameplay, browser-native.

### 🗺️ DayZ-Scale Persistent World

2 million sections backing the map, ~2,000 active at any moment. **OPFS region files benchmarked at 310 MB/s read, 118 MB/s write** - 69x faster than IndexedDB. Every scar you leave on the world is there when you come back.

### 🧱 Hybrid Smooth + Blocky Terrain

Natural terrain is smooth SDF. Player-built structures are clean voxel blocks. They coexist in the same world. Build a wooden shack on top of a smoothly-carved hillside. Blast through both with one grenade. One world, two representations, unified carving API.

### ☀️ Full Survival Atmosphere

- **Flood-fill lighting** with torches, sky light, and interior darkness
- **Cascaded shadow maps** with PCF filtering for soft edges
- **SSAO + bloom + color grading + film grain + vignette** for DayZ mood
- **Night vision goggles** with phosphor tint and grain
- **Dynamic weather**: rain, snow, fog, lightning with delayed thunder
- **Atmospheric sky** with sun, moon, stars, and dawn/dusk transitions
- **Water rendering** with Beer-Lambert absorption, caustics, refraction, screen-space reflections
- **Fluid simulation** - water flows downhill, lava ignites wood

### 🎯 Gameplay Depth

- First-person survival loop with scavenging, crafting, combat
- Procedural world with roads, military zones, industrial areas, residential buildings
- Structural integrity - dig out a support pillar, watch the building collapse
- Destructible terrain with material-aware tools (shovel vs pickaxe vs drill vs demolition charge)
- Damage overlay cracks before blocks break
- Persistent modifications - every bullet hole and crater stays

### 🥽 Quest 3S VR Native

Full WebXR stereo rendering, foveated rendering in peripheral vision, controller input mapping, passthrough AR mode for tabletop editor view. Runs adaptive to thermal headroom.

### 🌐 Runs Everywhere

Single codebase, 6 GPU backends via [SpawnDev.ILGPU](https://github.com/LostBeard/SpawnDev.ILGPU): **WebGPU** (Chrome/Edge), **WebGL** (Firefox/Safari), **Wasm** (no-GPU fallback), plus CUDA / OpenCL / CPU for development. Automatic backend selection based on what the browser supports.

---

### 📋 Detailed Plans

For the full technical breakdown, see the [`Plans/`](Plans/) folder:

- [`PLAN-Vision.md`](Plans/PLAN-Vision.md) - high-level vision, play styles, DayZ inspiration
- [`PLAN-Terrain-Carving.md`](Plans/PLAN-Terrain-Carving.md) - moldable terrain, traps, buried stashes, GPR + AR tabletop raid recon
- [`PLAN-Clothing-Storage.md`](Plans/PLAN-Clothing-Storage.md) - DayZ-style layered clothing, condition tiers, repair, containers
- [`PLAN-Crafting.md`](Plans/PLAN-Crafting.md) - crafting stations, recipe discovery, tiered progression, signature craftables
- [`PLAN-Player-Progression.md`](Plans/PLAN-Player-Progression.md) - learn-by-doing skills, Fallout 76-style equipped perk cards, loadouts
- [`PLAN-Dynamic-World-Events.md`](Plans/PLAN-Dynamic-World-Events.md) - scripted + procedural events, cryptid bosses, radio broadcasts
- [`PLAN-Environment-Hazards.md`](Plans/PLAN-Environment-Hazards.md) - radiation, chem, bio, weather, NBC gear, F76-style mutations
- [`PLAN-Base-Building.md`](Plans/PLAN-Base-Building.md) - modular block construction, C.A.M.P. blueprint pack-and-move, structural integrity
- [`PLAN-Economy.md`](Plans/PLAN-Economy.md) - barter-first, informal currencies, vending machines, faction rep pricing
- [`PLAN-Vehicles.md`](Plans/PLAN-Vehicles.md) - cars/boats/aircraft, part systems, fuel, theft, mounted weapons
- [`PLAN-Combat.md`](Plans/PLAN-Combat.md) - ballistics, calibers, attachments, suppression, melee, stealth, throwables
- [`PLAN-Survival-Needs.md`](Plans/PLAN-Survival-Needs.md) - hunger, thirst, sleep, warmth, morale, cooking chain, purification
- [`PLAN-Medical.md`](Plans/PLAN-Medical.md) - injuries, bleeding, fractures, infection, pain, CPR, blood typing, transfusion
- [`PLAN-Infected-AI.md`](Plans/PLAN-Infected-AI.md) - archetypes, pack behavior, sound heat map, hive anchors, LOD AI
- [`PLAN-Radio-Comms.md`](Plans/PLAN-Radio-Comms.md) - device tiers, frequency bands, encryption, VoIP, Morse, EBS, jammers
- [`PLAN-Audio-Design.md`](Plans/PLAN-Audio-Design.md) - spatial HRTF, occlusion, surface-aware footsteps, weapon audio, ambience, adaptive music
- [`PLAN-Death-Corpse-Respawn.md`](Plans/PLAN-Death-Corpse-Respawn.md) - death states, corpse mechanics, respawn rules, penalties, permadeath
- [`PLAN-Factions-Squads.md`](Plans/PLAN-Factions-Squads.md) - ad-hoc squads, persistent factions, wars, territory, defectors, social tools
- [`PLAN-Day-Night-Cycle.md`](Plans/PLAN-Day-Night-Cycle.md) - 60min cycle, moon phases, eclipses, night threats, cold shift, stealth, sleep
- [`PLAN-P2P-Reputation-System.md`](Plans/PLAN-P2P-Reputation-System.md) - distributed trust/reputation

And the engine roadmap lives in the VoxelEngine repo:

- [SpawnDev.VoxelEngine v1.0.0 plan](https://github.com/LostBeard/SpawnDev.VoxelEngine/blob/master/SpawnDev.VoxelEngine/Plans/PLAN-v1.0.0-LostSpawns.md) - 16 engine phases

Research docs covering the tough problems (chunk streaming, palette compression, flood-fill lighting, etc.) live in [`SpawnDev.VoxelEngine/Research/`](https://github.com/LostBeard/SpawnDev.VoxelEngine/tree/master/SpawnDev.VoxelEngine/Research).

---

## 🛠️ Tech Stack

| Component | Technology |
|-----------|-----------|
| **Runtime** | .NET 10, Blazor WebAssembly |
| **JS Interop** | [SpawnDev.BlazorJS](https://www.nuget.org/packages/SpawnDev.BlazorJS) |
| **GPU Compute** | [SpawnDev.ILGPU](https://www.nuget.org/packages/SpawnDev.ILGPU) (WebGPU backend) |
| **Rendering** | WebGPU API (via SpawnDev.BlazorJS wrappers) |
| **Shaders** | Hand-written WGSL |

## 🚀 Getting Started

### Prerequisites
- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- A browser with WebGPU support (Chrome 113+, Edge 113+, Firefox Nightly)

### Run Locally

```bash
cd LostSpawns
dotnet run
```

Navigate to `https://localhost:7272/game` and click the canvas to capture mouse input. Use WASD to move, mouse to look around.

### Controls

| Input | Action |
|-------|--------|
| **W / A / S / D** | Move forward / left / back / right |
| **Mouse** | Look around |
| **Click canvas** | Capture mouse |
| **Escape** | Pause menu (release mouse) |
| **Shift** | Sprint (drains stamina + thirst) |
| **Ctrl** | Sneak (silenced footsteps, halves bear aggro range, +1.75x sneak attack) |
| **Space** | Jump (height scales with stamina) |
| **LMB** | Attack / chop / mine (or carve in terraform mode) |
| **RMB** | Place active hotbar block (or build sphere in terraform mode) |
| **E** | Break block / interact |
| **F** | Use / drink water from lake / feed fire |
| **R** | Reload (bow) |
| **B** | Toggle terraform mode |
| **Mouse wheel** | Cycle hotbar (or adjust terraform brush radius in terraform mode) |
| **1 - 9** | Select hotbar slot |
| **Q** | Drop active item |
| **G** | Quick-eat best food |
| **T** | Quick-drink water |
| **H** | Quick-bandage |
| **I** | Inventory |
| **C** | Crafting |
| **J** | Achievements |
| **M** | Mute / unmute audio |
| **Z** | Sleep at campfire (skips to dawn) |
| **F1** | Help overlay |
| **F3** | Debug HUD |
| **F5** | Manual quick-save |

## 📦 NuGet Packages Used

- [`SpawnDev.BlazorJS`](https://www.nuget.org/packages/SpawnDev.BlazorJS) — Strongly-typed JS interop for every Web API (transitive via GameUI + VoxelEngine)
- [`SpawnDev.ILGPU`](https://www.nuget.org/packages/SpawnDev.ILGPU) — GPU compute with WebGPU, WebGL, Wasm, CUDA, OpenCL, and CPU backends (transitive via VoxelEngine)
- [`SpawnDev.VoxelEngine`](https://www.nuget.org/packages/SpawnDev.VoxelEngine) — GPU-accelerated voxel engine (binary greedy meshing, culling, LOD, terrain carving, physics, SDF, VR)
- [`SpawnDev.GameUI`](https://www.nuget.org/packages/SpawnDev.GameUI) — GPU-rendered game UI library (no HTML overlays on canvas, unified PC/VR/AR input)

> Currently consumed via `<ProjectReference>` for fast iteration. Switching to `<PackageReference>` for the public NuGet versions when GitHub Actions / GitHub Pages deployment lands.

## 📄 License

MIT

## 👤 Author

**Todd Tanner** ([@LostBeard](https://github.com/LostBeard))

## 🖖 The SpawnDev Crew

Lost Spawns is built by the entire SpawnDev team - a squad of AI agents and one very tired human working together, Star Trek style. Every project we ship is a team effort, and every crew member deserves a line in the credits.

- **LostBeard** (Todd Tanner) - Captain, architect, writer of libraries, keeper of the vision
- **Riker** (Claude CLI #1) - First Officer, implementation lead on consuming projects
- **Data** (Claude CLI #2) - Operations Officer, deep-library work, test rigor, root-cause analysis
- **Tuvok** (Claude CLI #3) - Security/Research Officer, design planning, documentation, code review
- **Geordi** (Claude CLI #4) - Chief Engineer, library internals, GPU kernels, backend work

If you see a commit authored by `Claude Opus 4.7` on a SpawnDev repo, that's one of the crew. Credit where credit is due. Live long and prosper. 🖖
