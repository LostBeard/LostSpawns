# Lost Spawns

A voxel-based 3D survival game built entirely in **Blazor WebAssembly** — showcasing the power of [SpawnDev.BlazorJS](https://github.com/LostBeard/SpawnDev.BlazorJS) and [SpawnDev.ILGPU](https://github.com/LostBeard/SpawnDev.ILGPU) for GPU-accelerated rendering and compute in the browser.

## 🎮 Overview

A **WIP**, Lost Spawns is a post-apocalyptic survival game inspired by DayZ. It runs entirely client-side in your web browser using WebGPU for hardware-accelerated 3D rendering and GPU compute - no plugins, no native code, just C# and Blazor.

The scope is intentionally ambitious: a full DayZ-scale persistent open world with moldable terrain, dynamic weather, hybrid smooth + blocky environments, flood-fill lighting, and Quest 3S VR native support. All from a single codebase running on WebGPU, WebGL, or Wasm depending on the browser. This is a showcase of what Blazor WASM + SpawnDev.ILGPU can do when pushed hard.

## ✨ Features

### GPU Pipeline (SpawnDev.ILGPU + WebGPU)
- **GPU Terrain Generation** — Perlin noise heightmap computed on the GPU via ILGPU compute kernels
- **GPU Mesh Generation** — Voxel face culling and vertex emission done entirely on the GPU with atomic counters
- **GPU→GPU Buffer Copies** — Sub-view peer-to-peer copies for efficient vertex data transfer
- **WebGPU Render Pipeline** — Custom WGSL shaders with dual-light system, distance fog, and per-block color variation

### Rendering
- **Infinite Voxel Terrain** — Procedurally generated, streamed in real-time as you explore
- **Face-Dependent Block Colors** — Grass is green on top, earth-toned on sides
- **Distance Fog** — Smooth quadratic falloff blending terrain into the sky
- **Frustum Culling** — Only visible chunks are drawn
- **Free-List Vertex Buffer** — Sub-allocated GPU buffer with slot reuse and coalescing (zero-stall grow via GPU copy)

### Gameplay
- **First-Person Camera** — WASD movement + mouse look
- **Biome-Based Terrain** — Grass, dirt, stone, sand, water, trees
- **60 FPS** — Smooth, stall-free rendering even while streaming new terrain

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

For the full technical breakdown, see the [`Plans/`](LostSpawns/Plans/) folder:

- [`PLAN-Terrain-Carving.md`](LostSpawns/Plans/PLAN-Terrain-Carving.md) - moldable terrain brainstorm and carving roadmap
- [`PLAN-P2P-Reputation-System.md`](LostSpawns/Plans/PLAN-P2P-Reputation-System.md) - distributed trust/reputation

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
| **W/A/S/D** | Move forward/left/back/right |
| **Mouse** | Look around |
| **Click canvas** | Capture mouse |
| **Escape** | Release mouse |

## 📦 NuGet Packages Used

- [`SpawnDev.BlazorJS`](https://www.nuget.org/packages/SpawnDev.BlazorJS) — Strongly-typed JS interop for every Web API
- [`SpawnDev.ILGPU`](https://www.nuget.org/packages/SpawnDev.ILGPU) — GPU compute (ILGPU) with WebGPU, WebGL, CUDA, and OpenCL backends

## 📄 License

MIT

## 👤 Author

**Todd Tanner** ([@LostBeard](https://github.com/LostBeard))
