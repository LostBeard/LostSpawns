# Voxel Engine Optimization - Master Research Document

**Date:** 2026-04-13
**Author:** Data (with research agents)
**For:** AubsCraft (Geordi) and Lost Spawns (Tuvok)
**Reviewed by:** Captain (TJ)

Both engines target WebGPU via SpawnDev.ILGPU. WebGPU is a hard requirement - no fallbacks needed. This simplifies implementation significantly.

---

## Table of Contents

1. [Binary Greedy Meshing](#1-binary-greedy-meshing)
2. [Occlusion Culling](#2-occlusion-culling)
3. [Vertex Pooling and Buffer Management](#3-vertex-pooling-and-buffer-management)
4. [Indirect Draw Calls](#4-indirect-draw-calls-on-webgpu)
5. [Compact Vertex Formats](#5-compact-vertex-formats)
6. [Reversed-Z Depth Buffer](#6-reversed-z-depth-buffer)
7. [LOD and Streaming](#7-lod-and-streaming)
8. [Adaptive Quality for Quest 3S](#8-adaptive-quality-for-quest-3s)
9. [SpawnDev.ILGPU Mapping](#9-spawndevilgpu-mapping---how-to-implement-each-technique)
10. [Implementation Priority](#10-implementation-priority)
11. [Sources](#11-sources)

---

## 1. Binary Greedy Meshing

**Impact: 80-90% vertex reduction. The single biggest optimization available.**

### The Problem

Naive voxel meshing emits one quad per exposed block face. A flat 16x16 grass plain produces 256 quads. Greedy meshing merges them into 1 quad. For typical terrain: 85-97% triangle reduction.

### Original Algorithm (Lysenko, 2012)

Sweep the volume along each of 3 axes. For each perpendicular 2D slice, build a boolean mask where `true` = visible face exists. Then greedily merge:

1. Find first unvisited `true` cell at `(x, y)`
2. Extend width: scan right while same type and unvisited -> `w++`
3. Extend height: check rows below, ALL cells in `[x, x+w)` must match -> `h++`
4. Mark rectangle visited, emit quad `(x, y, w, h)`
5. Continue until mask consumed

Time: O(n) per slice. Each cell visited constant times.

### Binary Variant (cgerikj, 2020/2024)

Replaces the per-cell mask with **64-bit integers** where each bit represents one voxel along an axis. Processes 64 faces simultaneously via bitwise operations.

**Step 1 - Occupancy Mask:** Build `uint64[CS_P * CS_P]` where CS_P = chunk_size + 2 (padding from neighbors). Each 64-bit int is a column of voxels. Bit k = 1 if solid.

**Step 2 - Face Culling (64 faces at once):**

```
// Y+ face (top): solid here AND air above
faceMask[TOP] = columnBits & ~(columnBits << 1)

// Y- face (bottom): solid here AND air below  
faceMask[BOT] = columnBits & ~(columnBits >> 1)

// X+ face: solid here AND air at neighbor column
faceMask[X+] = (col & ~neighborCol_Xplus) >> 1
```

One AND + one NOT = 64 faces culled simultaneously.

**Step 3 - Greedy Merge via bit manipulation:**

```
while (bitsHere != 0) {
    bitPos = ctz(bitsHere);  // Find lowest set bit (trailing zeros)
    type = getBlockType(axis, forward+1, bitPos+1, layer+1);
    
    // Try forward merge (extend to next row)
    if ((bitsNext >> bitPos & 1) && type == nextType) {
        forwardMerged[bitPos]++;
        bitsHere &= ~(1ULL << bitPos);
        continue;
    }
    
    // Try right merge (extend along current row)
    rightMerged = 1;
    for (right = bitPos+1; right < CS; right++) {
        if (!(bitsHere >> right & 1)) break;
        if (forwardMerged[right] != forwardMerged[bitPos]) break;
        if (type != getBlockType(...)) break;
        forwardMerged[right] = 0;
        rightMerged++;
    }
    
    // Clear consumed bits and emit quad
    bitsHere &= ~((1ULL << (bitPos + rightMerged)) - 1);
    emit(x=forward-forwardMerged[bitPos], y=layer, z=bitPos,
         w=forwardMerged[bitPos]+1, h=rightMerged, type);
    forwardMerged[bitPos] = 0;
}
```

### Performance Numbers

| Method | Time per chunk | Triangles (typical terrain) |
|--------|---------------|----------------------------|
| Naive (one quad per face) | ~550 us | Up to 196,608 |
| Traditional greedy | ~2,000-4,000 us | 2,000-10,000 |
| Binary greedy | **50-200 us** | Same as greedy (optimal merge) |

### Texture Atlas Handling

Greedy meshing merges faces with **identical visual properties only**. For per-face textures (grass = green top, dirt bottom, grass_side):

- The merge criterion is the **texture ID for the specific face direction being processed**, not block type
- Since each face direction is processed independently, this is automatic
- Merged quads tile the texture via `fract(worldPos)` in the fragment shader
- **Use texture arrays** (`texture_2d_array`) not atlas - avoids mipmap bleeding entirely

### Transparency and Special Blocks

- **Transparent blocks (water, glass):** Dual occupancy masks. One for all solid, one for opaque-only. Separate mesh pass with alpha blending.
- **Plants/cross-quads:** Cannot participate in greedy meshing. Separate buffer with X-shaped cross quads.
- **Water:** Separate water occupancy mask, separate mesh pass, adjusted Y for surface level.

### T-Junction Fix

Greedy meshing creates T-junctions where merged and unmerged quads meet. Fix: expand each quad by ~1 pixel in the vertex shader. Negligible cost, eliminates hairline gaps.

### GPU Parallelization with SpawnDev.ILGPU

Steps 1-2 are trivially parallel (one thread per column). Step 3 can run per-layer (372 independent layers for a 62-cube chunk with 6 faces). See Section 9 for ILGPU-specific implementation.

---

## 2. Occlusion Culling

### 2a. Sodium's Graph-Based Visibility (CPU-side, best for caves)

Each 16x16x16 section stores a 48-bit **visibility encoding**: for each of 6 entry faces, which of 6 exit faces are reachable through non-opaque blocks. Computed via flood fill on block changes.

**BFS traversal from camera:**

1. Start at camera's section, mark visible
2. Compute outgoing directions from visibility encoding
3. Enqueue neighbors in those directions
4. Each neighbor accumulates `incomingDirections` (OR'd from all paths that reach it)
5. `getConnections(visibilityData, incoming)` computes union of outgoing for all incoming - entirely bitwise, no branches
6. Only traverse outward from camera (prevents re-entering already-visited directions)

**Performance:** 48 bits per section. BFS is CPU-only, runs before any draw calls. Underground with caves: 90-99% culling. Surface: 50-70%.

**Key bit manipulation:**

```
// getConnections: given incoming direction set and visibility encoding,
// which directions can we exit through?
mask = createMask(incoming);  // Expand 6-bit set to 48-bit row selector
data = visibilityData & mask; // Select relevant rows
result = foldRows(data);      // OR all rows into 6-bit outgoing set
```

### 2b. Hierarchical Z-Buffer (Hi-Z) - GPU-side

Build a mip-chain from the depth buffer where each texel = max depth of its 2x2 children. Test chunk AABBs against coarse mip levels.

**Building the pyramid (WGSL compute shader):**

```wgsl
@compute @workgroup_size(8, 8)
fn generateHiZ(@builtin(global_invocation_id) id: vec3<u32>) {
    let base = id.xy * 2u;
    let d0 = textureLoad(previousLevel, base + vec2u(0,0), 0).r;
    let d1 = textureLoad(previousLevel, base + vec2u(1,0), 0).r;
    let d2 = textureLoad(previousLevel, base + vec2u(0,1), 0).r;
    let d3 = textureLoad(previousLevel, base + vec2u(1,1), 0).r;
    textureStore(nextLevel, id.xy, vec4f(max(max(d0, d1), max(d2, d3))));
}
```

**Testing an AABB:**
1. Project 8 corners to screen space
2. Compute bounding rect + nearest depth
3. Select mip level based on rect size: `level = ceil(log2(max(w,h) / 2))`
4. Sample Hi-Z at rect center at that mip level
5. If nearest depth > Hi-Z depth: OCCLUDED

**Two-phase rendering:**
- Phase 1: Render objects visible last frame (no culling needed). Build Hi-Z from depth.
- Phase 2: Test remaining objects against Hi-Z. Render survivors. Update visibility buffer.

**WebGPU note:** `depth24plus` can't be bound as storage texture. Copy to `r32float` first, or use `depth32float` format.

### 2c. Frustum Culling (GPU compute)

P-vertex/N-vertex AABB test against 6 frustum planes. One thread per chunk in a compute shader:

```wgsl
for (var i = 0u; i < 6u; i++) {
    let plane = frustum.planes[i];
    let px = select(aabbMin.x, aabbMax.x, plane.x >= 0.0);
    let py = select(aabbMin.y, aabbMax.y, plane.y >= 0.0);
    let pz = select(aabbMin.z, aabbMax.z, plane.z >= 0.0);
    if (dot(plane.xyz, vec3f(px, py, pz)) + plane.w < 0.0) {
        return; // culled
    }
}
```

### 2d. Fog Culling (cylindrical)

Sodium uses cylindrical fog: `sqrt(dx^2 + dz^2) < maxDist AND abs(dy) < maxDist`. Cheapest test, eliminates most chunks at large draw distances. Run FIRST in the culling pipeline.

### 2e. Face Masking (36% reduction)

Split each chunk mesh into 6 sub-meshes by face normal direction. Skip sub-meshes facing away from camera. This is true back-face culling at the chunk level - no mesh rebuild needed, just skip the draw command.

### Recommended Culling Pipeline

```
Per-section precompute (on block change):
  1. Flood-fill visibility encoding (48 bits)

Per-frame CPU:
  2. BFS graph traversal -> candidate set (front-to-back ordered)

Per-frame GPU (ILGPU compute kernels):
  3. Fog cull (cheapest, eliminates most)
  4. Frustum cull 
  5. Phase 1: render known-visible, build Hi-Z
  6. Phase 2: Hi-Z cull remaining, render survivors
  7. Face masking on draw commands (skip back-facing groups)
```

---

## 3. Vertex Pooling and Buffer Management

### Nick McDonald's Bucket Pool

Single large persistent VBO divided into N fixed-size buckets. FIFO free list for O(1) alloc/free.

**Allocation:** `bucket = freeList.pop_back()` - O(1)
**Deallocation:** `freeList.push_front(bucket)` - O(1)

Each chunk maps to 1-6 buckets (one per face orientation for face masking). Draw commands stored as indirect structs enabling O(1) reorder/erase via end-swap.

**Performance (measured):**

| Scenario | Naive (individual VBOs) | Vertex Pool | Improvement |
|----------|------------------------|-------------|-------------|
| Static render (16^3 chunks) | 11 ms | 6.1 ms | 45% |
| Dynamic remeshing (50/frame) | 79 ms draw, 4200us/mesh | 61 ms draw, 2820us/mesh | 23%/33% |
| With face masking | 8.4 ms | 5.4 ms | 36% |

### Compaction Strategy

When fragmentation exceeds threshold (>30% waste), GPU-copy all live chunks to buffer front via compute shader, update all indirect draw commands' vertex offsets. Expensive but rare.

---

## 4. Indirect Draw Calls on WebGPU

### The 300x Validation Win (CRITICAL)

Chrome's D3D12 backend injects validation compute dispatches before render passes. When 412 draws lived in 412 separate `GPUBuffer`s: **3ms of validation overhead** (50% of frame time). After consolidating all draws into **one** `GPUBuffer`: **~10 microseconds**. 300x improvement.

**Rule: ALL indirect draw commands in ONE GPUBuffer.** Non-negotiable.

**Buffer layout per draw (non-indexed):**
```
Offset 0:  vertexCount    (uint32)
Offset 4:  instanceCount  (uint32)
Offset 8:  firstVertex    (uint32)
Offset 12: firstInstance  (uint32)
```

**multiDrawIndirect extension:** `chromium-experimental-multi-draw-indirect` collapses the draw loop into a single call. Currently experimental. Fallback: individual `drawIndirect()` calls into the same buffer still gets the validation win.

### GPU-Driven Rendering Pipeline

Compute shader iterates all chunks, performs frustum + occlusion culling, writes surviving draw commands into indirect buffer. One render pass consumes the buffer. Data never leaves GPU between stages.

---

## 5. Compact Vertex Formats

### Binary Greedy Meshing: 8 Bytes per Quad

```
First 4 bytes: [x:6][y:6][z:6][width:6][height:6][unused:2]
Second 4 bytes: [voxelType:8][reserved:24]
```

Vertex shader reconstructs 4 corners from packed data using `vertexID % 6`. All chunks in a single storage buffer, rendered via vertex pulling.

### Sodium: 20 Bytes per Vertex

3x20-bit quantized position, RGBA color, 2x15-bit UV, packed light+material. Vs current AubsCraft at 44 bytes/vertex (11 floats). Halving vertex size doubles effective buffer capacity.

### Recommendation

Start with the 8-byte-per-quad format from binary greedy meshing. Vertex pulling from a storage buffer in WGSL. This is the most compact and pairs naturally with the ILGPU compute pipeline.

---

## 6. Reversed-Z Depth Buffer

**Why:** Standard depth wastes precision near the far plane. Reversed-Z maps near=1.0, far=0.0. Float precision near zero cancels the 1/z nonlinearity. Result: zero Z-fighting at any distance.

**NVIDIA testing:** Standard float32: 45% indistinguishable depths, 18% Z-fighting. Reversed-Z float32: 0% and 0%.

**WebGPU implementation:**

```javascript
// Depth texture
format: 'depth32float'

// Pipeline
depthCompare: 'greater'  // REVERSED

// Render pass
depthClearValue: 0.0     // REVERSED (far = 0)
```

**Infinite far plane matrix (left-handed, depth [1,0]):**

```
| cot(fov/2)/aspect  0          0      0     |
| 0                  cot(fov/2) 0      0     |
| 0                  0          0      zNear |
| 0                  0          1      0     |
```

Set zNear = 0.05-0.1. Far plane = infinity. Never see Z-fighting again.

---

## 7. LOD and Streaming

### LOD Levels for Block Voxels

| LOD | Resolution | Super-block size | Vertex reduction |
|-----|-----------|-----------------|-----------------|
| 0 | Full (1 block) | 1x1x1 | 1x |
| 1 | Half (2 blocks) | 2x2x2 | ~4x |
| 2 | Quarter (4 blocks) | 4x4x4 | ~16x |
| 3 | Eighth (8 blocks) | 8x8x8 | ~64x |

**Super-block type selection:** Most common non-air block in the group. Color = average of constituent blocks.

**Seam hiding:** Vertical skirts at LOD boundaries - thin polygons extruded downward from higher-detail edges. Cheap, effective, simple.

### Distant Horizons Approach (for extreme draw distance)

Quadtree over XZ plane. Each level doubles coverage, halves resolution. Uses column-based simplified meshes for distant terrain. LOD data persisted in SQLite/OPFS for instant reload.

### Streaming State Machine

```
UNLOADED -> LOADING -> MESHING -> UPLOADING -> GPU_READY -> VISIBLE
    ^                                                          |
    |_________ UNLOADING <___ HYBRID_CACHED <________________|
```

### Priority Queue

```
Priority = w_dist * (1/distance^2)
         + w_view * dot(chunkDir, cameraForward)
         + w_move * dot(chunkDir, cameraVelocity)
         + w_lod  * lodUrgency
```

### Budget-Based Streaming

| Parameter | Desktop | Quest 3S |
|-----------|---------|----------|
| Max mesh vertices/frame | 50,000 | 10,000 |
| Max GPU uploads/frame | 8 | 2 |
| Max mesh jobs/frame | 4 | 1 |
| Max load bytes/frame | 512 KB | 128 KB |

---

## 8. Adaptive Quality for Quest 3S

### Hardware Reality

- 8 GB RAM shared between OS, browser, app
- ~1.5-2 GB available for WebXR app in browser
- Passive cooling, thermal throttling after 5-10 minutes at peak
- Target 60-70% GPU utilization to avoid throttling
- Triangle budget: 500K-750K (Quest 3S)

### Memory Budget for Voxel Engine on Quest 3S

```
Total available:         ~2.0 GB
  WASM + JS heap:         200 MB
  Chunk voxel data:       200 MB
  GPU vertex buffers:     300 MB
  GPU textures:            64 MB
  GPU compute buffers:    100 MB
  Framework overhead:     100 MB
  Safety margin:         ~1 GB
```

### Recommended Draw Distances with LOD

| LOD | Draw Distance | Chunk Count |
|-----|--------------|-------------|
| 0 (full) | 0-48 blocks | ~60 |
| 1 | 48-128 blocks | ~120 |
| 2 | 128-384 blocks | ~200 |
| 3 | 384-1024 blocks | ~150 |
| Total | 1024 blocks | ~530 chunks |

### Adaptive Quality Controller

```
Quality levels: ULTRA -> HIGH -> MEDIUM -> LOW -> MINIMAL

// Downgrade: fast (any metric exceeds threshold for 3+ checks)
// Upgrade: slow (all metrics below threshold for 30+ checks)

// Frame time controller (asymmetric):
if (frameTimeEMA > MAX_FRAME_TIME)
    drawDistance -= DECREASE_RATE * 4;  // Emergency
else if (frameTimeEMA > TARGET_FRAME_TIME)
    drawDistance -= DECREASE_RATE;       // Over budget
else if (frameTimeEMA < TARGET * 0.85)
    drawDistance += INCREASE_RATE;       // Under budget (slow increase)
```

---

## 9. SpawnDev.ILGPU Mapping - How to Implement Each Technique

WebGPU is a hard requirement for both engines. This simplifies everything - we get compute shaders, shared memory, barriers, atomics, storage buffers, and all ILGPU Algorithms.

### 9a. Binary Greedy Meshing Kernels

**Step 1-2: Occupancy Mask + Face Culling Kernel**

One ILGPU kernel, dispatch `[CS_P, CS_P]`. Each thread processes one (x,z) column pair:

```csharp
static void FaceCullKernel(
    Index2D index,
    ArrayView<long> occupancyMask,  // uint64 columns, CS_P * CS_P
    ArrayView<long> faceMasks,      // output: CS * CS * 6
    int CS, int CS_P)
{
    int a = index.X;
    int b = index.Y;
    if (a < 1 || a >= CS_P - 1 || b < 1 || b >= CS_P - 1) return;
    
    long col = occupancyMask[a * CS_P + b] & P_MASK;
    long colXP = occupancyMask[(a + 1) * CS_P + b];
    long colXN = occupancyMask[(a - 1) * CS_P + b];
    long colZP = occupancyMask[a * CS_P + (b + 1)];
    long colZN = occupancyMask[a * CS_P + (b - 1)];
    
    int ba = (b - 1) * CS + (a - 1);
    faceMasks[ba + 0 * CS * CS] = (col & ~colXP) >> 1;  // +X
    faceMasks[ba + 1 * CS * CS] = (col & ~colXN) >> 1;  // -X
    faceMasks[ba + 2 * CS * CS] = (col & ~colZP) >> 1;  // +Z
    faceMasks[ba + 3 * CS * CS] = (col & ~colZN) >> 1;  // -Z
    faceMasks[ba + 4 * CS * CS] = col & ~(col >> 1);     // +Y
    faceMasks[ba + 5 * CS * CS] = col & ~(col << 1);     // -Y
}
```

This is **perfect ILGPU work** - pure bitwise ops, no shared memory needed, massively parallel.

**Step 3: Greedy Merge Kernel**

Dispatch `[CS, 6]` - one thread per layer per face direction. Each thread runs the sequential merge loop for its slice, writing quads to an output buffer via `Atomic.Add` on a counter.

**Using Lambda Kernels for flexibility:**

```csharp
int chunkSize = 16;
long pMask = ~(1L << 63 | 1L);

var faceCullKernel = accelerator.LoadAutoGroupedStreamKernel<
    Index2D, ArrayView<long>, ArrayView<long>>(
    (index, occupancy, faces) => {
        // Lambda captures chunkSize and pMask as scalars
        // SpawnDev.ILGPU passes them to GPU automatically
        int CS_P = chunkSize + 2;
        // ... face culling logic
    });
```

### 9b. Culling Compute Kernels

**Frustum + Fog Cull Kernel:**

```csharp
static void CullKernel(
    Index1D index,
    ArrayView<ChunkInfo> chunks,        // input: all candidate chunks
    ArrayView<int> visibleCount,         // atomic counter
    ArrayView<int> visibleIndices,       // output: surviving chunk IDs
    ArrayView<float> frustumPlanes,      // 6 planes * 4 floats = 24
    float fogDistSq, float cameraY,     // fog params (captured via lambda)
    float fogMaxVertical)
{
    var chunk = chunks[index];
    
    // Fog cull first (cheapest)
    float dx = chunk.CenterX - cameraX;
    float dz = chunk.CenterZ - cameraZ;
    if (dx * dx + dz * dz > fogDistSq) return;
    if (IntrinsicMath.Abs(chunk.CenterY - cameraY) > fogMaxVertical) return;
    
    // Frustum cull
    for (int i = 0; i < 6; i++) {
        // P-vertex test...
        if (dot < 0) return;
    }
    
    int slot = Atomic.Add(ref visibleCount[0], 1);
    visibleIndices[slot] = index;
}
```

### 9c. DelegateSpecialization for LOD Kernels

One mesh kernel, multiple LOD behaviors via compile-time specialization:

```csharp
static void MeshKernel(
    Index1D index,
    ArrayView<int> blocks,
    ArrayView<long> outputQuads,
    ArrayView<int> counter,
    DelegateSpecialization<Func<int, int, int, int>> getBlock)
{
    // getBlock is specialized at compile time per LOD level
    int blockType = getBlock.Value(x, y, z);
    // ... meshing logic
}

// LOD 0: direct block lookup
static int GetBlockLOD0(int x, int y, int z) => blocks[x + z * 16 + y * 256];

// LOD 1: 2x2x2 super-block (most common type)
static int GetBlockLOD1(int x, int y, int z) => 
    MostCommon(blocks, x*2, y*2, z*2, 2);

// Dispatch with different specializations - body is inlined, no overhead
kernel(size, blocks, quads, counter, new DelegateSpecialization<...>(GetBlockLOD0));
kernel(size, blocks, quads, counter, new DelegateSpecialization<...>(GetBlockLOD1));
```

### 9d. RadixSort for Draw Order

SpawnDev.ILGPU includes RadixSort (tested up to 4M+ elements, all backends). Use it to sort draw commands by distance for front-to-back rendering:

```csharp
// Sort chunks by distance for optimal early-Z
using var distanceKeys = accelerator.Allocate1D<float>(chunkCount);
using var chunkIndices = accelerator.Allocate1D<int>(chunkCount);

// Fill distance keys in a compute kernel
computeDistanceKernel(chunkCount, chunks, cameraPos, distanceKeys, chunkIndices);

// GPU RadixSort - already implemented and tested in SpawnDev.ILGPU
var radixSort = accelerator.CreateRadixSort<float, int>();
radixSort(accelerator.DefaultStream, distanceKeys.View, chunkIndices.View);

// Use sorted indices to order indirect draw commands
orderDrawCommandsKernel(chunkCount, chunkIndices, drawCommands);
```

This is a massive win - sorting thousands of chunks on GPU is microseconds vs milliseconds on CPU.

### 9e. CopyFromJS for Zero-Copy Chunk Upload

When chunk data arrives from the JS data worker via MessagePort:

```csharp
// Receive ArrayBuffer from JS data worker
var jsBuffer = receivedMessage.GetProperty<ArrayBuffer>("data");

// Zero-copy transfer to GPU - no .NET heap allocation
((IBrowserMemoryBuffer)gpuBlockBuffer).CopyFromJS(jsBuffer);

// Dispatch meshing kernel immediately
meshKernel(blockCount, gpuBlockBuffer.View, outputQuads.View, counter.View);
```

Data path: Server -> WebSocket -> JS Worker -> ArrayBuffer -> CopyFromJS -> GPU Buffer -> Kernel. No .NET allocation on the hot path.

### 9f. GpuMatrix4x4 for View/Projection

SpawnDev.ILGPU provides `GpuMatrix4x4` that auto-transposes from .NET's row-major to GPU column-major:

```csharp
// In kernel: transform chunk AABB corners for frustum testing
var clipPos = viewProj.TransformPoint(cornerPos);
```

### 9g. Scan for Prefix Sums (Compaction)

ILGPU Algorithms includes Scan (prefix sum). Use for compacting visible chunk lists:

```csharp
// After frustum cull kernel marks visible chunks (1/0):
var scan = accelerator.CreateScan<int>(ScanKind.Exclusive);
scan(stream, visibilityFlags.View, prefixSums.View);

// prefixSums now contains the compacted write offsets
compactKernel(chunkCount, visibilityFlags, prefixSums, compactedChunks);
```

### 9h. Shared Memory for Face Culling

WebGPU backend supports shared memory and barriers. Use for loading chunk neighborhood data:

```csharp
static void FaceCullWithSharedMem(
    Index1D index,
    ArrayView<int> blocks,
    ArrayView<long> faceMasks)
{
    // Load block data into shared memory for neighbor lookups
    var sharedBlocks = SharedMemory.Allocate1D<int>(18 * 18 * 18); // 16+2 padding
    
    // Cooperative load (all threads in group)
    // ... load with padding from neighbor chunks
    
    Group.Barrier(); // Synchronize before reading neighbors
    
    // Now face culling can read neighbors from fast shared memory
    // instead of slow global memory
}
```

### 9i. Sub-Word Types for Block Storage

Block IDs as `short` (Int16) or `byte` (Int8) using SpawnDev.ILGPU's sub-word support:

```csharp
// Block IDs stored as short - halves memory vs int
using var blockBuffer = accelerator.Allocate1D<short>(16 * 16 * 384);

// Kernel reads short directly - SpawnDev.ILGPU handles the packing
static void MeshKernel(Index1D index, ArrayView<short> blocks, ...) {
    short blockId = blocks[index]; // Correct extraction on all backends
}
```

This halves GPU memory for block data. Combined with CopyFromJS, the Int16Array from JS transfers directly to the short buffer.

---

## 10. Implementation Priority

### Tier 1: Highest Impact, Do First

1. **Binary greedy meshing** (80-90% vertex reduction)
   - Implement as ILGPU compute kernels (Steps 1-2 parallel, Step 3 per-layer)
   - 8-byte quad output format with vertex pulling
   - Face masking (6 groups, skip back-facing)

2. **Single-buffer indirect draws** (300x validation win on Chrome)
   - All draw commands in one GPUBuffer
   - Compute kernel writes surviving commands after culling

3. **Adaptive vertex budget from device limits**
   - Query `device.Limits.MaxBufferSize`
   - Derive all caps from actual hardware
   - Draw distance shrink under pressure

### Tier 2: High Impact, Medium Effort

4. **Sodium-style graph occlusion culling** (50-99% culling)
   - Visibility encoding per section (48 bits)
   - BFS traversal on CPU
   - Massive win for caves/underground

5. **Reversed-Z depth buffer** (zero Z-fighting, simple change)
   - `depth32float`, `depthCompare: 'greater'`, `clearValue: 0.0`
   - Infinite far plane

6. **Hi-Z occlusion culling** (GPU compute)
   - Mip-chain from depth buffer
   - Two-phase rendering
   - Test AABBs in compute shader

### Tier 3: Quest 3S Essential

7. **LOD system** (16x vertex reduction at LOD 2)
   - DelegateSpecialization for LOD-parameterized kernels
   - Pressure-based LOD selection
   - Vertical skirts at boundaries

8. **Streaming budget controller**
   - Max vertices/uploads per frame
   - Priority queue (distance + view direction)
   - Adaptive quality levels

9. **Compact vertex format** (2x buffer capacity)
   - 8-byte per quad with vertex pulling
   - Sub-word block IDs (Int16)

### Tier 4: Polish

10. **RadixSort for draw order** (early-Z optimization)
11. **Buffer compaction** (defragmentation)
12. **Z-prepass** (eliminate overdraw)

---

## 11. Sources

### Binary Greedy Meshing
- [Meshing in a Minecraft Game - 0fps.net (Lysenko 2012)](https://0fps.net/2012/06/30/meshing-in-a-minecraft-game/)
- [Binary Greedy Meshing - cgerikj/GitHub](https://github.com/cgerikj/binary-greedy-meshing)
- [Transparency with Binary Greedy Meshing - EngineersBox](https://engineersbox.github.io/website/2024/09/19/transparency-with-binary-greedy-meshing.html)
- [Texture Atlases, Wrapping and Mip Mapping - 0fps.net](https://0fps.net/2013/07/09/texture-atlases-wrapping-and-mip-mapping/)
- [GPU Voxel Mesh Generation - artnas/UnityVoxelMeshGPU](https://github.com/artnas/UnityVoxelMeshGPU)
- [Binary Greedy Meshing Rust Crate](https://docs.rs/binary-greedy-meshing)
- [T-Junctions - Voxel.Wiki](https://voxel.wiki/wiki/t-junction/)

### Occlusion Culling
- [Advanced Cave Culling Algorithm (Tommo/Checchi)](https://tomcc.github.io/2014/08/31/visibility-1.html)
- [Sodium Chunk Rendering Pipeline (DeepWiki)](https://deepwiki.com/CaffeineMC/sodium/3.1-chunk-rendering)
- [Sodium Source: OcclusionCuller.java](https://github.com/CaffeineMC/sodium/blob/dev/common/src/main/java/net/caffeinemc/mods/sodium/client/render/chunk/occlusion/OcclusionCuller.java)
- [Hierarchical Z-Buffer - RasterGrid](https://www.rastergrid.com/blog/2010/10/hierarchical-z-map-based-occlusion-culling/)
- [Hierarchical Depth Buffers - Mike Turitzin](https://miketuritzin.com/post/hierarchical-depth-buffers/)
- [Two-Pass Occlusion Culling - Milos Kruskonja](https://medium.com/@mil_kru/two-pass-occlusion-culling-4100edcad501)

### Vertex Pooling and Draw Calls
- [High Performance Voxel Engine: Vertex Pooling - Nick McDonald](https://nickmcd.me/2021/04/04/high-performance-voxel-engine/)
- [WebGPU Indirect Draw Best Practices - Toji.dev](https://toji.dev/webgpu-best-practices/indirect-draws.html)
- [GPU-Driven Rendering Pipeline - Vulkan Guide](https://vkguide.dev/docs/gpudriven/gpu_driven_engines/)
- [Compute Based Culling - Vulkan Guide](https://vkguide.dev/docs/gpudriven/compute_culling/)

### Depth Buffer
- [Depth Precision Visualized - Nathan Reed](https://www.reedbeta.com/blog/depth-precision-visualized/)
- [Visualizing Depth Precision - NVIDIA](https://developer.nvidia.com/blog/visualizing-depth-precision/)
- [WebGPU Reversed-Z Sample](https://webgpu.github.io/webgpu-samples/samples/reversedZ/)

### LOD and Streaming
- [Distant Horizons Mod](https://www.curseforge.com/minecraft/mc-mods/distant-horizons)
- [Aokana: GPU-Driven Voxel Rendering (I3D 2025)](https://arxiv.org/abs/2505.02017)
- [A Level of Detail Method for Blocky Voxels - 0fps.net](https://0fps.net/2018/03/03/a-level-of-detail-method-for-blocky-voxels/)
- [Transvoxel Algorithm](https://transvoxel.org/)
- [Voxels and Seamless LOD Transitions - dexyfex.com](https://dexyfex.com/2016/07/14/voxels-and-seamless-lod-transitions/)

### Mobile VR / Quest
- [Notes on VR Performance - GitHub](https://github.com/authorTom/notes-on-VR-performance)
- [Meta Quest Memory Documentation](https://developers.meta.com/horizon/documentation/native/android/po-memory-ram/)
- [Android Thermal API (ADPF)](https://developer.android.com/games/optimize/adpf/thermal)
- [WebXR FFR on Quest - Meta Forums](https://communityforums.atmeta.com/discussions/dev-quest/webxr-and-quest-how-to-enable-fixed-foveated-rendering-ffr/852462)

### General Voxel Rendering
- [Voxel World Optimisations - Vercidium](https://vercidium.com/blog/voxel-world-optimisations/)
- [Exile Voxel Rendering Pipeline - TheNumb.at](https://thenumb.at/Voxel-Meshing-in-Exile/)
- [To Early-Z, or Not To Early-Z - MJP](https://therealmjp.github.io/posts/to-earlyz-or-not-to-earlyz/)
- [OPFS vs IndexedDB Benchmarks - RxDB](https://rxdb.info/articles/localstorage-indexeddb-cookies-opfs-sqlite-wasm.html)

### SpawnDev.ILGPU
- [SpawnDev.ILGPU NuGet](https://www.nuget.org/packages/SpawnDev.ILGPU)
- [SpawnDev.ILGPU GitHub](https://github.com/LostBeard/SpawnDev.ILGPU)
- [SpawnDev.ILGPU Live Demo](https://lostbeard.github.io/SpawnDev.ILGPU/)
- [ILGPU Project](https://github.com/m4rs-mt/ILGPU)
