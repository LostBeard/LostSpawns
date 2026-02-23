# Lost Spawns

A voxel-based 3D survival game built entirely in **Blazor WebAssembly** — showcasing the power of [SpawnDev.BlazorJS](https://github.com/LostBeard/SpawnDev.BlazorJS) and [SpawnDev.ILGPU](https://github.com/LostBeard/SpawnDev.ILGPU) for GPU-accelerated rendering and compute in the browser.

## 🎮 Overview

A **WIP**, Lost Spawns is a post-apocalyptic survival game inspired by DayZ. It runs entirely client-side in your web browser using WebGPU for hardware-accelerated 3D rendering and GPU compute — no plugins, no native code, just C# and Blazor.

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
