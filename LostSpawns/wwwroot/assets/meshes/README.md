# Mesh assets (CC0)

See [THIRD_PARTY_NOTICES.md](../../../THIRD_PARTY_NOTICES.md) for licenses.

## Layout

- `wildlife/*.glb` - Quaternius Ultimate Animated Animals (CC0) via AnimaSim GLB export
- `tools/*.glb` - Kenney drop-ins (not shipped; add axe/pickaxe/bow when ready)

## Catalog / runtime

Runtime mapping: `Content/MeshCatalog.cs`.

1. `GltfMeshService` fetches GLBs into JS `ArrayBuffer`s (not the .NET heap).
2. `GlbGpuUploader` reads only the JSON chunk into managed memory, then
   `writeBuffer`s POSITION / NORMAL / JOINTS_0 / WEIGHTS_0 / index views.
3. `GlbSkinRuntime` keeps IBM + Walk/Attack/Idle curves; evaluates a 64-bone
   palette each draw (Walk when moving, Attack when charging, else Idle).
4. `EntityMeshPipeline` skins in WGSL inside the voxel depth pass.
5. HUD billboards are skipped per-kind once a GPU mesh is ready (HP/name stay).
