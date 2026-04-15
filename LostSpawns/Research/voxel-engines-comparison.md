# Voxel Engine Comparison - Reference for AubsCraft and Lost Spawns

**Date:** 2026-04-13
**Author:** Data
**For:** Captain, Geordi, Tuvok

---

## Engine Comparison Table

| Engine | Language | Graphics API | Platform | Visual Style | Open Source | HD/Realistic | WebGPU | VR | Active | Best Reference For |
|--------|----------|-------------|----------|-------------|-------------|-------------|--------|-----|--------|-------------------|
| **Teardown** | C++ | OpenGL 3.3 | Desktop | HD/Realistic | No | YES | No | No | Yes | Lighting, ray-traced AO, material system |
| **Veloren** | Rust | wgpu (Vulkan/Metal/DX12/WebGPU) | Desktop | Stylized RPG | Yes (GPL3) | Partial | Possible | No | Very active | wgpu architecture, LOD, multiplayer |
| **Octo** | Rust | WebGPU | Desktop + **Browser** | HD/Realistic | Partial | YES | **YES** | No | Active | **Ray marching on WebGPU, path-traced GI, SVO** |
| **WebGPU-Voxel-Engine** | TypeScript | WebGPU | **Browser** | HD with PBR | Yes | YES | **YES** | No | Recent | **PBR materials, atmospheric effects on WebGPU** |
| **Divine Voxel Engine** | TypeScript | WebGPU/Babylon/Three | **Browser** | Blocky+ | Yes (MIT) | No | **YES** | No | Active | **Multi-threaded browser arch, renderer-agnostic** |
| **Aokana** | C#/HLSL | Vulkan (Unity 6) | Desktop | HD | Academic | YES | No | No | 2025 | **SVDAG, 10B voxels at 6ms, streaming LOD** |
| **VoxelRT** | C#/.NET | OpenGL/GLSL | Desktop | Multiple | Yes | YES | No | No | Active | **C# reference! Multiple ray tracing techniques** |
| **Vercidium** | C# | OpenGL | Desktop | Blocky | Yes | No | No | No | Active | **C# mesh generation, real-time destruction** |
| **IOLITE** | C/C++ | Path tracing | Desktop | Photorealistic | Partial | YES | No | No | Active | Path-traced GI without RT hardware |
| **Avoyd** | C++ | OpenGL+Vulkan | Desktop | Photorealistic | No | YES | No | No | Active | GPU path tracing, SVO DAG, PBR export |
| **Luanti (Minetest)** | C++ | OpenGL | Desktop+Android | Blocky | Yes (LGPL) | No | No | No | Very active | Modding architecture, Lua API |
| **Vintage Story** | C# (.NET) | OpenGL | Desktop | Detailed blocky | Partial | Partial | No | No | Active | **C#/.NET reference, deep modding** |
| **MagicaVoxel** | ? | OpenGL 3.3 | Desktop | Photorealistic render | No | YES | No | No | Active | Path tracing proves voxels can be photorealistic |
| **Voxel Farm** | C++ | DX/OpenGL | Desktop+Mobile | HD smooth | No (commercial) | YES | No | No | Active | Marching cubes LOD, procedural |
| **Cube 2: Sauerbraten** | C++ | OpenGL | Desktop | HD smooth | Yes (zlib) | Partial | No | No | Mature | Octree-based deformable geometry |
| **ClassiCube** | C | WebGL | Desktop + **Browser** | Blocky | Yes | No | No | No | Active | Extremely portable, WASM build |
| **WAVE** | C/WASM | WebGL | **Browser** | Blocky+lighting | Yes | No | No | No | Available | **WASM voxel engine, RLE, greedy meshing, LOD** |
| **All is Cubes** | Rust | WebGL+WASM | Desktop + **Browser** | Stylized | Yes | No | No | No | Active | Rust+WASM dual target, recursive blocks |
| **Rezcraft** | Rust | wgpu | Desktop + **Browser** | Blocky | Yes | No | **YES** | No | Active | **Rust+wgpu dual target, greedy meshing** |
| **BrickMap** | C++/CUDA | CUDA | Desktop (NVIDIA) | Realistic | Yes | YES | No | No | Available | 75B voxels in 2.2GB, brickmap structure |
| **Dust Engine** | Rust | Vulkan (HW RT) | Desktop | Realistic+GI | Yes | YES | No | No | Dev | Real-time GI, surfel irradiance |
| **Atomontage** | ? | ? | Desktop+Web | Near-photorealistic | No | YES | No | No | Active | Volumetric simulation, real-time editing |
| **Gravitas** | Zig | WebGPU | Desktop + **Browser** | ? | Yes | ? | **YES** | No | Dev | Zig+WebGPU voxel engine |
| **NOA Engine** | JavaScript | WebGL (Babylon) | **Browser** | Blocky | Yes | No | No | No | Maintained | Lightweight browser voxel framework |
| **Godot Voxel Tools** | C++ | Godot renderer | Desktop | Blocky+Smooth | Yes | Partial | No | Partial | Active | Transvoxel LOD, Godot integration |
| **Qubatron** | C/OpenGL | OpenGL | Desktop | Photorealistic | Yes | YES | No | No | Dev | Micro-voxel ray tracing, skeletal anim |
| **Cubiquity** | C++ | OpenGL | Desktop | Micro-voxel | Yes (PD) | YES | No | No | Available | ESVO raycasting, geometry instancing |
| **WebGPU-.vox** | JS | WebGPU | **Browser** | Path-traced | Yes | YES | **YES** | No | Available | **WebGPU path tracing, PBR, Cook-Torrance** |

---

## Top Reference Engines for Our Stack (SpawnDev.ILGPU + WebGPU + Blazor WASM)

### Tier 1: Directly Relevant (WebGPU, browser-capable, or C#/.NET)

**1. Octo (Douglas Dwyer) - Rust + WebGPU + WASM**
- Runs IN THE BROWSER on WebGPU
- Ray marching via compute shaders (exactly what ILGPU does)
- Path-traced indirect lighting, SVO acceleration
- Physics with destruction
- LOD optimization
- WASM modding
- **Why study it:** Proves our target architecture works. Ray marching + compute on WebGPU in a browser with realistic lighting. This is the closest existing engine to what we're building.
- https://github.com/DouglasDwyer/octo-release

**2. VoxelRT (dubiousconst282) - C#/.NET + OpenGL**
- Written in C#/.NET - our language
- Implements MULTIPLE ray tracing methods side by side (ESVO, Tree64, MultiDDA, OctantDF)
- Excellent comparative analysis of techniques
- **Why study it:** C# reference implementation of voxel ray tracing. The code patterns translate directly to ILGPU kernels.
- https://github.com/dubiousconst282/VoxelRT
- https://dubiousconst282.github.io/2024/10/03/voxel-ray-tracing/

**3. WebGPU-Voxel-Engine (rowannadon) - TypeScript + WebGPU**
- PBR material system with physically-based atmospheric effects
- Multi-threaded chunk management
- LOD rendering
- **Why study it:** PBR + atmosphere on WebGPU in a browser. Shows what's achievable.
- https://deepwiki.com/rowannadon/WebGPU-Voxel-Engine

**4. Divine Voxel Engine - TypeScript + WebGPU/Babylon/Three**
- Renderer-agnostic (including WebGPU backend)
- Multi-threaded via SharedArrayBuffer across Web Workers
- Headless server support
- **Why study it:** Multi-threaded browser architecture with WebGPU. Solves the same worker problems we're solving.
- https://github.com/Divine-Star-Software/DivineVoxelEngine

**5. Vercidium - C# + OpenGL**
- Written in C# - our language
- Run-based meshing optimized for real-time destruction (relevant to both games)
- 4-byte packed vertices
- 0.48ms per chunk mesh
- **Why study it:** C# mesh generation patterns. The packed vertex format and run-based meshing are directly translatable.
- https://github.com/Vercidium/voxel-mesh-generation
- https://vercidium.com/blog/voxel-world-optimisations/

### Tier 2: Study the Architecture

**6. Aokana (I3D 2025) - C#/Unity + Vulkan**
- State-of-the-art SVDAG rendering
- 10 billion voxels at 6ms/frame
- Streaming LOD with 5% VRAM residency
- **Why study it:** The LOD and streaming architecture is directly applicable. The SVDAG structure could be an ILGPU compute kernel.
- https://arxiv.org/abs/2505.02017

**7. Teardown - C++ + OpenGL 3.3**
- Gold standard for realistic voxel lighting
- Ray-traced AO, soft shadows, specular in OpenGL 3.3 (no RT hardware)
- **Why study it:** Proves realistic voxel rendering without hardware ray tracing. The techniques map to WebGPU compute shaders.
- https://acko.net/blog/teardown-frame-teardown/

**8. Vintage Story - C# (.NET) + OpenGL**
- Our ecosystem (C#/.NET)
- Deep modding and extensibility
- Detailed block interaction systems
- **Why study it:** C#/.NET game architecture, modding API patterns.
- https://www.vintagestory.at/

### Tier 3: Community Resources

**9. r/VoxelGameDev** - Active community with technique discussions
- https://www.reddit.com/r/VoxelGameDev/

**10. "Let's Make a Voxel Engine"** - Tutorial series
- https://sites.google.com/site/letsmakeavoxelengine/home

**11. GitHub voxel-engine topic (by stars)**
- https://github.com/topics/voxel-engine?o=desc&s=stars

**12. James Randall's Simple C++ Voxel Engine**
- https://www.jamesdrandall.com/projects/simple_cpp_voxel_engine/

**13. Declan Easton's Voxel Engine**
- https://declaneaston.ca/2020/02/09/voxel-engine/

**14. Quixel - Smooth Voxel Engine (Unity, free for commercial)**
- https://discussions.unity.com/t/quixel-cool-smooth-voxel-engine-ive-found-free-for-commercial-use/561755

**15. HN discussion on voxel engines (2024)**
- https://news.ycombinator.com/item?id=40480022

**16. BTWCE Voxel Engine Forum Thread**
- https://forum.btwce.com/viewtopic.php?t=7035

**17. Ray-traced voxel lighting on custom GPU (r/VoxelGameDev)**
- https://www.reddit.com/r/VoxelGameDev/comments/1i0jd4x/our_voxel_game_with_ray_traced_lighting_on_a/

**18. Wikipedia List of Game Engines**
- https://en.wikipedia.org/wiki/List_of_game_engines

---

## What Makes Voxels Look Realistic (Ranked by Impact)

1. **Lighting quality** - Path tracing or raytraced AO transforms cubes into photorealism. MagicaVoxel proves it.
2. **Voxel size** - Microvoxels make cube faces invisible. Sub-millimeter = surfaces, not cubes.
3. **PBR materials** - Metallic, roughness, emissive per voxel. Wet ground, weathered metal, glass.
4. **Ambient occlusion** - Even per-vertex AO is massive. Raytraced AO is photographic.
5. **Soft shadows** - Jittered shadow rays with temporal accumulation.
6. **Normal softening** - Blur normal buffer near camera to smooth hard edges.
7. **Volumetric effects** - Fog, atmospheric scattering, volumetric light.
8. **Post-processing** - TAA, bloom, DOF, tone mapping.

---

## Sources

All URLs listed inline above. Additional references:
- [Jacco's Ray Tracing with Voxels (8-part series)](https://jacco.ompf2.com/2024/04/24/ray-tracing-with-voxels-in-c-series-part-1/)
- [Exile Voxel Meshing Pipeline](https://thenumb.at/Voxel-Meshing-in-Exile/)
- [John Lin - The Perfect Voxel Engine](https://voxely.net/blog/the-perfect-voxel-engine/)
- [Teardown Frame Teardown (Acko.net)](https://acko.net/blog/teardown-frame-teardown/)
- [Fast Voxel Ray Tracing with Sparse 64-Trees](https://dubiousconst282.github.io/2024/10/03/voxel-ray-tracing/)
- [enkisoftware GPU Voxel Octree Path Tracer](https://www.enkisoftware.com/devlogpost-20230823-1-Implementing-a-GPU-Voxel-Octree-Path-Tracer)
- [NVIDIA VXGI](https://archive.docs.nvidia.com/gameworks/content/gameworkslibrary/visualfx/vxgi/index.html)
- [Voxel Cone Tracing GI](https://github.com/jose-villegas/VCTRenderer)
- [AO for Minecraft-like Worlds](https://0fps.net/2013/07/03/ambient-occlusion-for-minecraft-like-worlds/)
- [awesome-opensource-voxel](https://github.com/DrSensor/awesome-opensource-voxel)
