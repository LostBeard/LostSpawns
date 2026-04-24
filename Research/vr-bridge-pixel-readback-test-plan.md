# VR Bridge Pixel Readback Test Plan (TJ's suggestion 2026-04-23)

**Status:** Draft 2026-04-23. Math-level tests already landed in `VoxelEngineTestBase.VrMatrixMath.cs`; this plans the INTEGRATION layer.
**Owner:** Data.

## Why

Matrix math tests (`VrMatrix_*`) cover pure coordinate-convention correctness. They caught one real bug (ZFlip convention mismatch, 2026-04-23). They do NOT catch:

- Byte-level mismatch between what WebGPU writes to `OffscreenCanvas` and what WebGL2 reads via `texImage2D(canvas)` (Chromium's non-destructive GPU-GPU bridge).
- Y-flip errors on upload (WebGL's `UNPACK_FLIP_Y_WEBGL` default behavior vs canvas natural top-down).
- Color channel swizzle (BGRA vs RGBA between WebGPU preferred format and WebGL2 texture upload).
- Alpha handling (opaque vs premultiplied) and gamma (sRGB vs linear) on the handoff.
- Viewport stereo-split correctness (left eye reads left half of canvas, not right).

These are the kind of bugs that render "visually correct-ish" but fail specific-pixel checks - exactly the kind that TJ flagged could be caught via render-and-readback verification.

## Scope

**In:**
- A test that drives WebGPU to fill `OffscreenCanvas(W, H)` with a known four-quadrant pattern (top-left = red, top-right = green, bottom-left = blue, bottom-right = yellow).
- A test that exercises the `gl.texImage2D(TEXTURE_2D, 0, RGBA, RGBA, UNSIGNED_BYTE, offscreen)` bridge onto a WebGL2 texture.
- A test that draws the presented texture via a fullscreen triangle into a WebGL2 framebuffer (NOT the canvas - a render-to-texture so we can read back).
- A test that calls `gl.readPixels` and asserts the four quadrant corners land in the expected screen quadrants.
- Stereo-half variant: render left-half blue, right-half yellow, verify eye-viewport presents only the assigned half.

**Out (for this first pass):**
- Validation on desktop backends (CPU, CUDA, OpenCL) - the bridge is browser-only.
- Real XR session validation (needs hardware).
- Depth-buffer bridge validation (Phase D concern, depth isn't bridged today).
- Performance measurements (separate instrumentation HUD).

## Test layout

Lives in `SpawnDev.VoxelEngine/SpawnDev.VoxelEngine.Demo.Shared/UnitTests/VoxelEngineTestBase.VrBridgePixels.cs`. Because the bridge requires WebGPU + WebGL2 in the same browser context, mark tests with a SpawnDev.UnitTesting `[BrowserOnly]`-style filter or throw `UnsupportedTestException` on desktop backends (matches existing pattern for browser-only features).

## Test primitives

```csharp
// 1. WebGPU setup.
var adapter = await navigator.Gpu.RequestAdapter();
var device = await adapter.RequestDevice();
var offscreen = new OffscreenCanvas(W, H);
var ctx = offscreen.GetWebGPUContext();
ctx.Configure(new GPUCanvasConfiguration { Device = device, Format = "bgra8unorm", AlphaMode = "opaque" });

// 2. Render a known four-quadrant pattern to offscreen via WebGPU.
var shaderWgsl = @"
@vertex fn vs(@builtin(vertex_index) i: u32) -> @builtin(position) vec4<f32> {
  var p = array<vec2<f32>, 3>(vec2<f32>(-1,-1), vec2<f32>(3,-1), vec2<f32>(-1,3));
  return vec4<f32>(p[i], 0, 1);
}
@fragment fn fs(@builtin(position) pos: vec4<f32>) -> @location(0) vec4<f32> {
  let half_w = f32(${W / 2});
  let half_h = f32(${H / 2});
  if (pos.x < half_w && pos.y < half_h) { return vec4<f32>(1, 0, 0, 1); }  // TL red
  if (pos.x >= half_w && pos.y < half_h) { return vec4<f32>(0, 1, 0, 1); } // TR green
  if (pos.x < half_w && pos.y >= half_h) { return vec4<f32>(0, 0, 1, 1); } // BL blue
  return vec4<f32>(1, 1, 0, 1);                                             // BR yellow
}
";
// ... dispatch render pass ...

// 3. WebGL2 setup with render-to-texture framebuffer.
var gl = canvas.GetContext<WebGL2RenderingContext>("webgl2");
var tex = gl.CreateTexture();
// ... upload offscreen via texImage2D(OffscreenCanvas overload) ...

// 4. Fullscreen-triangle presentation.
// Vertex shader same as VrPrototype.razor's PresentVertSrc.
// Fragment just samples u_tex at v_uv (no y-flip for this test; we WANT to see
// whether the default unpack behavior matches our production flip).

// 5. Read pixels.
var pixels = new byte[W * H * 4];
gl.ReadPixels(0, 0, W, H, GL.RGBA, GL.UNSIGNED_BYTE, pixels);

// 6. Assert quadrants.
AssertPixel(pixels, x: W/4,     y: H/4,    expected: RED);       // top-left
AssertPixel(pixels, x: 3*W/4,   y: H/4,    expected: GREEN);     // top-right
AssertPixel(pixels, x: W/4,     y: 3*H/4,  expected: BLUE);      // bottom-left
AssertPixel(pixels, x: 3*W/4,   y: 3*H/4,  expected: YELLOW);    // bottom-right
```

## What this test catches

- Y-flip: if the present shader flips correctly, TL/TR quadrants read back at TL/TR positions in `pixels` (where pixels are stored bottom-up per WebGL's storage convention). Off-by-Y-flip would swap TL<->BL and TR<->BR.
- Color swizzle: BGRA-stored canvas displayed through RGBA-sampled texture. If the swizzle is wrong, red becomes blue (reading BGRA as RGBA flips R and B channels).
- Out-of-viewport bleed: if viewport math is off, corners read a different quadrant than expected.

## Open questions

1. SpawnDev.UnitTesting: how does browser-only test marking work? Check `VoxelEngineTestBase.Rendering.cs` for an existing browser-only pattern.
2. Render-to-texture vs render-to-canvas: reading canvas pixels vs framebuffer pixels - do we get the same answer? Test should use FBO-backed texture for determinism.
3. Tolerance: compare bytes exactly, or allow LINEAR sampling blur (1-byte tolerance)?

## Not doing

- Running against a real XR session - too much infrastructure setup for automated tests. Left to on-Quest verification.

Proposal: defer Phase D implementation until bridge pixel tests are added + green, so Phase D work (porting overlays to WebGPU) is built on a verified bridge.
