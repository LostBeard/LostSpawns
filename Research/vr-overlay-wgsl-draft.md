# VR Overlay WGSL Shader Port Draft (Phase D.1 prep)

Draft WGSL translations of the three overlay shader paths currently in `VrPrototype.razor` (GLSL 300 es). Not compiled yet - this is a workbench for when Phase D.1 starts.

Source shaders live at `VrPrototype.razor` around line 250-290 (main GLSL pair + present pair).

## Unified overlay pipeline (D.1 + D.2 + D.3)

Rather than three separate pipelines, one pipeline with a uniform flag `shapeMode` covers: solid cube, grid-textured floor, circular disc mask. This is how the existing GLSL shader does it (`u_gridAmount`, `u_shape`). Same consolidation in WGSL keeps bind-group layout uniform and simplifies D.5 cleanup.

```wgsl
// Uniforms struct matches VertexPullPipeline's 128-byte pattern but smaller.
// Bind group 0, binding 0.
struct OverlayUniforms {
  mvp: mat4x4<f32>,              // 64 bytes
  tint: vec3<f32>,               // 12
  grid_amount: f32,              //  4 (0 = solid, 1 = grid lines)
  shape_mode: u32,               //  4 (0 = cube, 1 = circular disc)
  _pad0: u32,                    //  4
  _pad1: u32,                    //  4
  _pad2: u32,                    //  4
}
@group(0) @binding(0) var<uniform> u: OverlayUniforms;

struct VOut {
  @builtin(position) pos: vec4<f32>,
  @location(0) color: vec3<f32>,
  @location(1) world_pos: vec3<f32>,
  @location(2) local_xz: vec2<f32>,
}

@vertex
fn vs_main(
    @location(0) a_pos: vec3<f32>,
    @location(1) a_color: vec3<f32>,
) -> VOut {
  var out: VOut;
  let world = u.mvp * vec4<f32>(a_pos, 1.0);
  out.world_pos = a_pos;            // already-world cube vertex positions (see note)
  out.local_xz = a_pos.xz;
  out.color = a_color;
  out.pos = world;
  return out;
}

@fragment
fn fs_main(v: VOut) -> @location(0) vec4<f32> {
  if (u.shape_mode == 1u) {
    // Circular disc mask.
    let r = length(v.local_xz);
    if (r > 0.5) { discard; }
    let falloff = 1.0 - smoothstep(0.35, 0.5, r);
    return vec4<f32>(u.tint * (0.55 + 0.45 * falloff), 1.0);
  }

  var col = v.color * u.tint;
  if (u.grid_amount > 0.001) {
    let g = abs(fract(v.world_pos.xz) - vec2<f32>(0.5));
    let line = 1.0 - smoothstep(0.46, 0.50, min(g.x, g.y));
    let g2 = abs(fract(v.world_pos.xz * 4.0) - vec2<f32>(0.5));
    let fine = 1.0 - smoothstep(0.44, 0.50, min(g2.x, g2.y));
    let grid_col = vec3<f32>(0.55, 0.65, 0.80);
    col = mix(col, grid_col, line * u.grid_amount * 0.75);
    col = mix(col, grid_col * 0.7, fine * u.grid_amount * 0.25);
  }
  return vec4<f32>(col, 1.0);
}
```

**Note:** GLSL version computes world pos via `u_model * a_pos`. Our WGSL version should do the same; `out.world_pos = (u.model * vec4(a_pos, 1.0)).xyz` if we bind `u.model` alongside. Alternatively, MVP already folds model so we can reconstruct world via `u.model`. Simpler: pass `u.model` as a separate mat4 in the uniform and compute `out.world_pos = (u.model * vec4(a_pos,1.0)).xyz` for the grid sampling; MVP still does final clip.

Actual uniform layout (revised):
```
mvp: mat4x4<f32>       64
model: mat4x4<f32>     64  -- for grid world sampling
tint: vec3<f32>        12
grid_amount: f32        4
shape_mode: u32         4
_pad: vec3<u32>        12
```
Total = 160 bytes, aligned to 256 for dynamic-offset ring.

## Instanced trigger-drop pipeline (D.4)

Per-instance model matrix + tint, fed via a vertex buffer at `@location(2..6)`. Mirrors the current `VertSrcInst`/`FragSrcInst` GLSL pair.

```wgsl
struct InstancedUniforms {
  view_proj: mat4x4<f32>,        // 64
}
@group(0) @binding(0) var<uniform> u: InstancedUniforms;

struct VOutI {
  @builtin(position) pos: vec4<f32>,
  @location(0) color: vec3<f32>,
}

@vertex
fn vs_main(
    @location(0) a_pos: vec3<f32>,
    @location(1) a_color: vec3<f32>,
    @location(2) a_model_c0: vec4<f32>,
    @location(3) a_model_c1: vec4<f32>,
    @location(4) a_model_c2: vec4<f32>,
    @location(5) a_model_c3: vec4<f32>,
    @location(6) a_tint: vec4<f32>,
) -> VOutI {
  let model = mat4x4<f32>(a_model_c0, a_model_c1, a_model_c2, a_model_c3);
  var out: VOutI;
  out.color = a_color * a_tint.rgb;
  out.pos = u.view_proj * model * vec4<f32>(a_pos, 1.0);
  return out;
}

@fragment
fn fs_main(v: VOutI) -> @location(0) vec4<f32> {
  return vec4<f32>(v.color, 1.0);
}
```

## Present shader (remains as WGSL OR stays WebGL2)

The fullscreen-triangle present is WebGL2 today and stays WebGL2 in Phase D (it's what binds the WebGPU-rendered canvas to the XRWebGLLayer framebuffer). No WGSL needed.

If Variant 2b ever materializes (WebGPU renders DIRECTLY to WebXR), the present shader moves to WGSL + disappears entirely.

## Open questions for when Phase D.1 starts

1. **Reversed-Z for overlays?** The terrain pipeline uses reversed-Z (depthCompare=greater, clearValue=0). Overlays would share the same depth attachment, so they MUST use reversed-Z too. The OverlayUniforms MVP would need to be ZFlip-composed the same way the terrain MVP is. Or the pipeline state for overlays has its OWN depthCompare=greater baked in. Simplest: same ZFlip post-multiply approach as terrain.
2. **One pipeline or three?** Cube / grid-floor / disc consolidation is in the draft above. The alternative is three separate pipelines with distinct shaders. The consolidation is already how the GLSL side works, so it's the path of less surprise.
3. **Dynamic uniform ring for overlays?** The existing `VertexPullPipeline.InitDynamic` pattern could apply: pre-allocate N slots (where N = 3 cubes + 1 floor + 2 controllers + 2 rays + 2 hits = ~10 per eye = 20 max). Each draw picks its slot via dynamic offset. One upload per frame.
4. **How to verify no perf regression?** The overlay cost today is already small (~10 draw calls per eye via WebGL2). Moving to WebGPU shouldn't change much. A per-frame timing HUD (like VR.1a's `RollingMs`) would make any regression visible.

## Non-goals for D.1

- Do NOT port controller models. They stay as cube + ray boxes using the same cube geometry.
- Do NOT add lighting/shading. Flat-shaded tint is fine.
- Do NOT add anti-aliasing. XRWebGLLayer handles MSAA on the WebGL side; equivalent on WebGPU uses `multisampled: true` on the color attachment, which requires a second resolve texture. Defer.
