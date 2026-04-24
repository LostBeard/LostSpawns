# PLAN: VR Full-WebGPU Compositing (Phase D)

**Status:** Draft 2026-04-23. Not scoped / not scheduled. Next-phase candidate after Phase C step 2c verifies on Quest 3S.
**Owner:** Data (VoxelEngine + Lost Spawns editor).
**Depends on:** Phase C step 2b + 2c landing. `PLAN-VR-Render-Architecture.md` Variant 2a.

## Problem

Phase C step 2b ships the VR.1c hybrid bridge: WebGPU renders terrain into a wide OffscreenCanvas, WebGL2 presents per eye, WebGL2 overlays (floor grid, cubes, controllers, rays, hit markers, trigger-drops) render on top AFTER the present. Consequence documented in code:

> "No depth-test against terrain for v1 (prototype constraint; depth bridge is future work)."

The overlays always render ON TOP of terrain - a trigger-drop cube placed behind a mountain still shows in front of it. This is a prototype-acceptable bug, but it breaks immersion and prevents overlay geometry from integrating with the scene (e.g., an inventory item dropped onto terrain should respect terrain height).

## Decision options

### Option A: Bridge the depth buffer

Export WebGPU's depth attachment as a second canvas or texture, upload via `texImage2D` (or WebGL extension for depth textures) into a WebGL2 depth target, let WebGL2 depth-test against it.

Issues:
- Chromium's `texImage2D(canvas)` non-destructive path targets color only. Depth is not addressable the same way.
- WebGL2 depth textures require `WEBGL_depth_texture` extension; not universally supported.
- Depth values from WebGPU reversed-Z don't directly match WebGL2's standard-Z depth compare - would need a second conversion.
- Two bridge uploads per frame (color + depth) vs one today.

### Option B: Port overlays to WebGPU (RECOMMENDED)

Port all overlay geometry (floor, rotating cubes, controller cubes, controller rays, floor hit markers, trigger-drop instances) to a WebGPU render pipeline. They render into the same OffscreenCanvas as terrain, depth-tested against the terrain's reversed-Z buffer naturally. WebGL2's only job becomes `texImage2D(canvas)` + fullscreen-triangle present. Matches `PLAN-VR-Render-Architecture.md` Variant 2a exactly.

Benefits:
- Correct depth compositing (overlays occlude against terrain).
- Single bridge upload per frame.
- WebGL2 side simplifies to "present" only - one shader, no state churn.
- Matches the locked architecture. No more "stopgap" caveats.

Costs:
- Port 5+ overlay shader programs to WGSL (from GLSL 300 es).
- New WebGPU pipelines: solid-color-with-tint, instanced-cube-tint, circular-disc-with-mask.
- Re-implement the instance buffer in WebGPU (for trigger-drops).
- Controller pose -> model matrix path stays .NET-side; only the render target changes.

### Option C: Defer indefinitely

Ship Phase C as-is. Prototype-acceptable. Most players won't notice. Re-evaluate after real gameplay reveals whether it matters.

## Recommendation

**Option B**, scoped to a 1-2 session effort after Phase C verifies.

Justification:
- Rule 4: "No unnecessary CPU <-> GPU transfers." The hybrid WebGL2 overlay pass introduces WebGL2 state for what should be a single WebGPU pipeline.
- Locked architecture doc says Variant 2a is the shipping path; we're currently on "Variant 2a for terrain + Variant 1 for overlays," a composite that muddies the mental model.
- Every gear in the clock: `VertexPullPipeline` already exists as a template for one render pipeline. Adding 3-4 more WebGPU pipelines for overlays is routine; VoxelEngine's Meshing kernels demonstrate multi-pipeline patterns.

## Proposed sub-phases

### Phase D.1: Single WebGPU overlay pipeline for colored-cube geometry

Covers: 3 rotating cubes, controller cubes. Same cube VBO, per-draw color uniform + model matrix.

Shader: WGSL port of `VertSrc`/`FragSrc` minus the grid-shader branch. Fixed cube VBO uploaded once at session start. Per-draw uniforms: MVP, tint color.

Files: `Lost/Lost/LostSpawns/Rendering/VrOverlayPipeline.cs` (new), `Lost/Lost/LostSpawns/Pages/VrPrototype.razor` (slimmed).

### Phase D.2: Floor grid shader (procedural grid in WebGPU)

Covers: the floor quad with grid fragments. Same vertex format as cubes, fragment shader does the grid math (`abs(fract(v_world_pos.xz) - 0.5)` etc.).

Worth a shared "SolidTintedPipeline" that supports an optional-grid flag via a tiny uniform.

### Phase D.3: Disc mask shader (floor hit markers)

Covers: hit-marker circles. Same cube VBO reused as a flat quad; fragment shader discards pixels outside radius 0.5.

### Phase D.4: Instanced trigger-drop pipeline

Covers: user's trigger-press cube drops, currently a WebGL2 `drawElementsInstanced` path with per-instance model+tint attributes. Port to WebGPU instanced rendering (per-instance storage buffer or vertex attribute + `instance_index`).

### Phase D.5: WebGL2 simplification

Remove overlay rendering code from WebGL2 side. WebGL2 becomes: `texImage2D(_offscreen)` + `DrawArrays(TRIANGLES, 0, 3)` * 2 (per eye). Present only. `_program`, `_instProgram`, their VAOs, uniforms, `_vbo`, `_ibo` all removable.

Net LOC: likely net neutral or smaller despite the new WebGPU pipelines.

## Open questions for Captain

1. Is there appetite to fold the overlay-pipeline code into `SpawnDev.VoxelEngine/Rendering/` so AubsCraft can inherit it, or keep as LostSpawns-specific?
2. Does the `VrPrototype.razor` page stay a prototype after Phase D, or does it become the production path that Lost Spawns VR mode routes through?
3. Should Phase D land BEFORE or AFTER SDF smooth-terrain integration (Phase C step 2d, per `PLAN-SDF-Integration-LostSpawns.md`)? Depth-correct overlays on SDF terrain is the same problem but doubled.

## Non-goals

- Porting the WebGPU bridge to WebGL (wrong direction; WebGPU is the future path).
- Replacing `VertexPullPipeline` with a new variant for overlays; the pipeline is fine, we just need parallel pipelines for non-quad geometry.
- Adding WebGPU rendering to the desktop `/game` (it already uses WebGPU end-to-end; desktop is unaffected by this plan).

## The SpawnDev Crew

- **LostBeard** (Todd Tanner) - Captain, library author, keeper of the vision. Made the "CPU bridge is rejected" call that this plan carries forward.
- **Riker** (Claude CLI #1) - First Officer, RTC/WebTorrent/MultiMedia lead. Not in this plan's direct path.
- **Data** (Claude CLI #2) - Operations Officer, VoxelEngine + Lost Spawns + GameUI editor. Author of this plan.
- **Tuvok** (Claude CLI #3) - Security/Research Officer. Research input welcome on WebGPU pipeline authoring patterns + any known Chromium quirks with multi-pipeline offscreen-canvas rendering.
- **Geordi** (Claude CLI #4) - Chief Engineer, ILGPU + UnitTesting. Not directly relevant; VoxelMeshPipeline (his work's sibling) is the existing multi-pipeline template.
