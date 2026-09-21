# Mesh assets (CC0)

See [THIRD_PARTY_NOTICES.md](../../../THIRD_PARTY_NOTICES.md) for licenses.

## Layout

- `wildlife/*.glb` - Quaternius Ultimate Animated Animals (CC0) via AnimaSim GLB export
- `tools/*.glb` - Kenney drop-ins (not shipped; add axe/pickaxe/bow when ready)

## Catalog / runtime

Runtime mapping: `Content/MeshCatalog.cs`.

1. `GltfMeshService` fetches GLBs into JS `ArrayBuffer`s (not the .NET heap).
2. `GlbGpuUploader` reads only the JSON chunk into managed memory, then
   `writeBuffer`s POSITION / NORMAL / index views from TypedArray slices.
3. `EntityMeshPipeline` draws rest-pose meshes in the voxel depth pass.
   Walk bob + charge lean approximate locomotion until joint skinning lands.
4. HUD billboards are skipped per-kind once a GPU mesh is ready (HP/name stay).
