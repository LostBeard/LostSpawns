# PLAN: VR Render Architecture

**Status:** Locked 2026-04-22. Primary target dominant; fallback demoted to defensive code.
**Owner:** Data (VoxelEngine editor), consumer impact spans WorldService / VoxelEngineService / RenderService.
**Depends on:** Captain's uncommitted `VoxelEngineService.cs` + `WorldService.cs` WIP landing.
**Related:** `PLAN-VR-Controls.md` (input), `PLAN-Terrain-Carving.md` (carving gameplay), `PLAN-Performance-Targets.md` (budgets).

**Update 2026-04-22 afternoon:** Captain empirically verified on Quest 3S native Meta Quest Browser v146.0: `XRGPUBinding` class present AND prototype methods (`createProjectionLayer`, `getViewSubImage`, `getPreferredColorFormat`) all present. `RenderPath: FullWebGpu`. Tentatively promoted primary `FullWebGpu` path to Quest shipping path.

**Update 2026-04-22 later afternoon (reversal):** Built barebones WebXR+WebGPU prototype at `/vr-prototype-xr` per canonical `immersive-web/webxr-samples/webgpu/vr-barebones.html` pattern. Empirically:
- Canonical sample requests `requiredFeatures: ['webgpu']` - this is the session-level feature the spec defines.
- Quest 3S Meta Quest Browser v146.0: session request throws *"The session request contains requiredFeatures and could not be fulfilled."* The 'webgpu' feature is not fulfillable by the runtime.
- Meta Immersive Web Emulator: same rejection.

**Conclusion:** Meta exposed the `XRGPUBinding` class + prototype method definitions in v146.0 but has NOT wired up the session-level `'webgpu'` feature integration. The API surface is a forward-compat stub - enough to pass our method-existence probe, not enough to actually run a WebXR+WebGPU session. The afternoon's "FullWebGpu is the Quest shipping path" call was premature. **Reversal: the hybrid fallback path (`HybridWebGl2`) IS the Quest 3S shipping path for Lost Spawns v1.** Primary `FullWebGpu` path remains valid for desktop Chrome Canary with both WebXR flags enabled, and for future browser versions that ship the session-feature integration.

The probe's meaning has been re-interpreted: "`FullWebGpu` detected by class + method existence" now means *"API surface present; functional session integration is a separate question that only a live session attempt can answer."* RenderPathDetector's next hardening pass should attempt a brief test session with `['webgpu']` feature to distinguish "API stub" from "fully functional" - but that requires a user gesture, so it can only happen on-demand at consumer init time, not at page load.

---

## Decision

**WebGPU-only for the browser target across every consumer (VoxelEngine, Lost Spawns, AubsCraft, SpawnScene, ILGPU.ML).** WebGL and Wasm compute backends remain in the `SpawnDev.ILGPU` library for portability (other consumers may want them) but are not shipping targets for any SpawnDev app from 2026-04-22 forward.

For VR specifically:

- **Primary render path (shipping target on desktop Chrome Canary with flags):** WebXR + WebGPU binding (full WebGPU end-to-end). Code against this API. As of 2026-04-22 evening, functional only on Chrome Canary with both `WebXR Projection Layers` and `WebXR/WebGPU Bindings` flags enabled. Neither Meta Quest Browser nor Meta's Immersive Web Emulator supports the session-level 'webgpu' feature yet, despite exposing the `XRGPUBinding` class definition.
- **Shipping render path for Quest 3S v1 (and likely v2 until Meta catches up):** WebGPU compute + WebGL2 render (hybrid via `OffscreenCanvas` + `transferToImageBitmap` handoff). Phase VR.1a becomes the real engineering work. WebXR session uses `XRWebGLLayer` as base layer (well-trodden since ~2019, 90fps proven on Quest 3S); WebGPU does compute off-session and hands results over via the ImageBitmap bridge.
- **Dev environment:** No clean WebXR+WebGPU dev rig today. Emulator is WebXR+WebGL2 only (its polyfill doesn't satisfy native `XRGPUBinding` constructor). Chrome Canary PCVR + Quest Link has plumbing bugs (frame presentation hangs). Pragmatic dev loop: build for hybrid path, iterate via Emulator (WebXR+WebGL2 side) and real Quest 3S over HTTPS haproxy (full hybrid stack, real perf numbers).

CPU bridge (readback + re-upload) is explicitly rejected. Violates Rule 4's zero-copy mandate and scales terribly on desktop where PCIe is the bottleneck.

---

## Evidence the decision rests on

Empirically verified 2026-04-22:

### Quest 3S native Meta Quest Browser (v146.0)

WebGPU support without flags. `webgpureport.org` pre-flag run reported:

- `vendor: qualcomm`, `architecture: adreno-7xx`, `isFallbackAdapter: false`
- Features: `shader-f16`, `subgroups` (min 64 / max 128), `timestamp-query`, `indirect-first-instance`, `texture-compression-astc + bc + 3d-sliced`, `dual-source-blending`, `float32-filterable`, `bgra8unorm-storage`
- Limits: `maxBufferSize: 2GB`, `maxStorageBuffersPerShaderStage: 16` (higher than desktop Chrome's 10), `maxComputeWorkgroupStorageSize: 32KB`
- Both high-performance and compatibilityMode adapters expose identical features (no gimped GLES fallback)
- **Workers have full WebGPU**: dedicated + service workers both expose `navigator.gpu`, `requestAdapter`, `requestDevice`, `getContext("webgpu")`, `transferControlToOffscreen` - all successful

**WebXR+WebGPU binding API** (added 2026-04-22 afternoon via `/render-path` probe):
- `XRGPUBinding` class present at global scope: YES
- `XRGPUBinding.prototype.createProjectionLayer` is a function: YES
- `XRGPUBinding.prototype.getViewSubImage` is a function: YES
- `XRGPUBinding.prototype.getPreferredColorFormat` is a function: YES
- RenderPath probe decision: `FullWebGpu`

**Session-level integration** (added 2026-04-22 evening via `/vr-prototype-xr`):
- `navigator.xr.requestSession('immersive-vr', { requiredFeatures: ['webgpu'] })`: **REJECTED** with *"The session request contains requiredFeatures and could not be fulfilled."*
- Same on Meta's Immersive Web Emulator: REJECTED identically.

**Refined conclusion:** Meta shipped the class definition + prototype methods in v146.0 as a forward-compat stub. The session-level 'webgpu' feature wiring is NOT implemented yet. Probe's class+methods detection is necessary but not sufficient; a functional session attempt is the only definitive test.

### Native Quest WebXR + WebGL2 rendering

`immersive-web.github.io/webxr-samples/` Immersive VR Session demo runs at **90 fps** on Quest 3S native browser. Handshake via Meta's OpenXR runtime, WebGL2 render path stable since ~2019.

### Windows Chrome Canary + Meta Horizon Link PCVR path

- WebGPU + WebXR binding API confirmed present (samples page reports "supports WebXR with WebGPU" and "VR support detected" with headset awake)
- Session initiation works (headset acknowledges, Chrome takes over display)
- Frame presentation hangs on both WebGL and WebGPU XR demos with no console errors
- Confirmed: Chrome Canary 149.0.7806.0 + Meta Horizon Link v85.0.0.239.552 combo has plumbing issues
- **Not a WebGPU problem** - symmetric hang in WebGL path rules that out
- Deferred. Not on the shipping path. Revisit if/when needed for full-WebGPU-VR dev parity.

### WebXR + WebGPU binding on native Quest Browser

**Not yet shipped.** Quest Browser v146.0 released 2026-04-21 with experimental WebGPU compute and WebXR depth projection. The WebGPU-binds-to-XR-rendering spec (`immersive-web/WebXR-WebGPU-Binding`) is separate and has no release date from Meta. GitHub issue #14 asking for support has no maintainer response. Plausibly a Q3-Q4 2026 milestone given Meta just shipped the compute half.

---

## Architecture

### Layer 1: Compute (all browsers, including Quest native)

All GPU compute kernels run on **WebGPU**:

- SDF field evaluation (`EvaluateSdfKernel`)
- Dual Marching Cubes mesh generation (`ClassifyActiveCellsKernel`, `GenerateDualVerticesKernel`, `GenerateQuadsKernel`)
- Terrain carving (`ModifySdfSphereKernel`)
- Greedy-mesh block face culling + quad generation
- Frustum / visibility culling
- Indirect draw buffer compaction

No WebGL fallback in the consumer apps. Consumers that need wider compatibility (if any) can bring their own WebGL backend via `SpawnDev.ILGPU`, but Lost Spawns / VoxelEngine demos / AubsCraft / SpawnScene assume WebGPU.

### Layer 2: Rendering

Two paths, capability-detected:

#### Primary: WebXR + WebGPU binding (full WebGPU VR)

```
WebGPU compute buffers  -->  WebGPU vertex/render pipeline  -->  XRProjectionLayer  -->  headset eye buffers
```

Single GPU device, single context, zero crossing. The architecturally clean path.

Targets: desktop Chrome Canary today (experimental flags), native Quest Browser when Meta ships the binding.

#### Fallback: WebGPU compute + WebGL2 render (hybrid)

```
WebGPU compute buffers                             WebGPU render (offscreen canvas)
        |                                                       |
        |                                              transferToImageBitmap()
        |                                                       |
        v                                                       v
WebGL2 vertex buffers (via interop)             WebGL2 texImage2D(imageBitmap)
        |                                                       |
        +-------> WebGL2 render pipeline <----------------------+
                            |
                            v
             WebXR + WebGL2 framebuffer --> headset eye buffers
```

Two concrete variants:

**Variant 2a: per-scene WebGPU render, WebGL2 as XR presentation glue.** WebGPU does the entire scene render including materials and shading, outputs to an `OffscreenCanvas` with `GPUCanvasContext`. Each frame, `transferToImageBitmap()` produces a GPU-resident ImageBitmap (no CPU copy on the common browser implementation path). WebGL2 binds the ImageBitmap via `texImage2D(target, ..., imageBitmap)` and draws two full-screen quads (one per eye) into the WebXR framebuffer. Cost is one GPU-GPU texture bind per eye per frame plus browser synchronization. Fits 11ms budget if browsers implement the ImageBitmap path well. **Needs empirical validation** - no claim that this performance holds until measured.

**Variant 2b: WebGPU compute outputs geometry buffers; WebGL2 renders directly from WebGL2-owned vertex buffers synced from WebGPU via interop textures.** More complex, requires per-attribute encoding. Only pursue if 2a's per-frame transfer cost proves prohibitive.

Targets: Quest Browser native today (until binding lands), any browser where the binding flag isn't enabled.

### Layer 3: Fallback gating

At engine init, `VoxelEngineService.InitAsync` detects capability:

```csharp
var hasWebXrWebGpuBinding = JS.Call<bool>("navigator.xr?.isSessionSupported", "immersive-vr")
    && /* query XRSystem for WebGPU binding support - exact API TBD */;
_renderPath = hasWebXrWebGpuBinding ? RenderPath.FullWebGpu : RenderPath.HybridWebGl2;
```

Single branch point. Every downstream subsystem reads `_renderPath` and takes the appropriate path. No `#if WEBGPU` / `#if WEBGL` in kernel code; compute layer is backend-agnostic because ILGPU handles that.

---

## Dev environment

| Tool | Use case | What it proves |
|------|----------|----------------|
| Desktop Chrome + Immersive Web Emulator (Meta extension) | Daily iteration, algorithm dev, UI polish | "Does the feature work correctly?" Bypasses all OpenXR plumbing. |
| Quest 3S native Meta Quest Browser | Per-feature reality check | "Does it hit the 90fps envelope on target hardware?" |
| Windows + Chrome Canary + Link (experimental) | Full-WebGPU-VR dev parity when binding lands natively | Deferred until Meta Horizon Link + Canary frame-presentation hang is fixed upstream. |

Primary expected dev loop: **Emulator for 90% of work, headset at natural checkpoints.**

---

## Implementation phases

### Phase VR.0 - Capability probe (half day)

- Verify Meta's Immersive Web Emulator installs cleanly in desktop Chrome
- Verify it emulates WebXR sessions correctly with our existing WebGL2 demos
- Verify it surfaces the WebGPU+WebXR binding when the binding flag is on (signal for whether we can develop the primary path in the emulator)
- Write a tiny capability-detection probe: "what render paths does this browser support right now?"

### Phase VR.1 - Hybrid fallback prototype (1-2 days)

- Small standalone demo: WebGPU compute produces a rotating cube mesh, renders to OffscreenCanvas, WebGL2 draws it as a texture quad in a WebXR session
- Measure on Quest 3S native browser: is the imageBitmap transfer cheap enough for 90fps?
- Green-light Variant 2a or escalate to Variant 2b if 2a can't hit budget

### Phase VR.2 - Primary path prototype (1-2 days)

- Same demo via WebXR+WebGPU binding (Chrome Canary + flags + emulator)
- Confirms the API surface our production code targets

### Phase VR.3 - Lost Spawns integration (scope TBD, gated on Captain's WIP landing)

- `VoxelEngineService.GenerateSdfFieldAsync` (Phase C.1 from `PLAN-SDF-Integration-LostSpawns.md`)
- `WorldService.SectionMesh` dual-buffer extension (Phase C.2)
- `RenderService` capability-gated render path (Phase C.3)
- `CarveService` + input wiring (Phase D.1-D.2)

---

## Risks and open questions

### Technical risks

1. **`transferToImageBitmap` + `texImage2D(imageBitmap)` round trip may not be zero-copy on Quest Browser.** Browsers vary on implementation. If it's a GPU-GPU copy, fine. If it's a silent GPU-CPU-GPU round trip, hybrid fallback doesn't meet the 90fps envelope. Mitigation: Phase VR.1 measures this explicitly before committing to Variant 2a.
2. **WebXR+WebGPU binding on Canary has known frame-presentation issues.** Observed 2026-04-22 with Canary 149 + Horizon Link v85. Not blocking since primary dev path is emulator + native Quest. Mitigation: track Chromium issue tracker + Meta Horizon Link updates, revisit quarterly.
3. **Meta's WebXR+WebGPU binding ship date on Quest Browser is unknown.** If it takes longer than 6-12 months, we're on the hybrid fallback in production for that window. Mitigation: design the fallback path to be production-quality, not "temporary."
4. **`maxStorageBuffersPerShaderStage` is 16 on Quest 3S vs 10 on desktop Chrome.** ILGPU already queries this at runtime via `accelerator.MaxStorageBufferBindings`, so kernel authoring just has to respect the runtime-read minimum. No change required; flagging so we don't accidentally hardcode 10 or 16 anywhere.

### Product risks

1. **Users don't install Chrome Canary.** This means the Windows+Link+Canary dev path is never a user-facing shipping target, only a dev tool. Lost Spawns on Windows+Link for end users needs the binding to land on Chrome Stable first (typical Chromium promotion cadence: experimental -> beta -> stable is 3-6 months post-experimental).
2. **Quest users may or may not ship on a version of Meta Quest Browser that has the binding.** Until Meta ships it, Quest users get the hybrid fallback. If the fallback renders cleanly at 90fps, users won't notice. If it doesn't, we need the binding or a native app.

---

## Non-goals

- WebGL / Wasm render paths in any SpawnDev consumer app. (Library-level WebGL/Wasm stays for third-party consumers who want it.)
- PCVR via Link as a shipping target for end users. Dev-tool only, and deferred while plumbing is broken.
- Multi-backend kernel variants in the library. `SpawnDev.ILGPU/CLAUDE.md` rule applies: no backend-specific kernel copies. One kernel, six backends at the library level, but consumers target WebGPU.

---

## Ripple effects in other plans

- **`SpawnDev.ILGPU/Plans/f16-emulation-plan.md`**: Phase 2 (WebGL f16 emulation, tasks W2.1-W2.7) becomes dead code for any SpawnDev consumer. Worth pinging Geordi to confirm descoping. Phase 1 (WebGPU f16 emulation, already at HEAD per ILGPU CLAUDE.md) remains valuable for desktop WebGPU adapters lacking native `shader-f16`.
- **`PLAN-Performance-Targets.md`**: Quest 3S target is 90fps with WebGPU compute + (hybrid or native) render. Measure against that once Phase VR.1 lands.
- **`PLAN-Terrain-Carving.md`**: No change to gameplay design, but the `TerrainCarveService.ApplySphereAsync` dispatch goes through WebGPU on all consumers now.
- **`PLAN-VR-Controls.md`**: Input handling via WebXR controllers works identically on both primary and fallback render paths; no coupling.
- **`PLAN-Networking-Multiplayer.md`**: No impact. WebRTC + WebTorrent stack is independent.

---

## The SpawnDev Crew

- **LostBeard** (Todd Tanner) - Captain, library author, keeper of the vision. Ran the Quest 3S tests that locked this decision in on 2026-04-22.
- **Riker** (Claude CLI #1) - First Officer, RTC/WebTorrent/MultiMedia lead. Not directly in this plan's path but the RTC/WebTorrent 3.1.0 chain unblocks Geordi's P2P work which feeds back into distributed compute scenarios Lost Spawns may eventually use.
- **Data** (Claude CLI #2) - Operations Officer, VoxelEngine + Lost Spawns + GameUI editor. Author of this plan.
- **Tuvok** (Claude CLI #3) - Security/Research Officer. Research input welcome on the `transferToImageBitmap` / ImageBitmap interop measurement questions.
- **Geordi** (Claude CLI #4) - Chief Engineer, ILGPU + UnitTesting. His f16 emulation Phase 1 landed at HEAD today; Phase 2 scope likely shrinks given this plan. Worth a direct ping.
