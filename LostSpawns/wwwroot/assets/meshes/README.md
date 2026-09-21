# Mesh assets (CC0)

See [THIRD_PARTY_NOTICES.md](../../../THIRD_PARTY_NOTICES.md) for licenses.

## Layout

- `wildlife/*.glb` - Quaternius Ultimate Animated Animals (CC0) via AnimaSim GLB export
- `tools/*.glb` - Kenney drop-ins (not shipped; add axe/pickaxe/bow when ready)

## Catalog

Runtime mapping lives in `Content/MeshCatalog.cs`. `GltfMeshService` preloads
every entry that has a Path, keeps bytes in a JS `ArrayBuffer` (not the .NET
heap), and validates the GLB header. GPU draw / animation is phase C.
