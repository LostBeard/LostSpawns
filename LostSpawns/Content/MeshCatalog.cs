using LostSpawns.Services;

namespace LostSpawns.Content;

/// <summary>
/// One CC0 (or public-domain) mesh slot. Path is relative to wwwroot.
/// Null Path = keep the current billboard / procedural stand-in.
/// </summary>
public readonly record struct MeshCatalogEntry(
    string Id,
    string? Path,
    string Source,
    string License,
    string Notes);

/// <summary>
/// Shortlist of CC0 wildlife + tool meshes for Lost Spawns.
/// Quaternius Ultimate Animated Animals (via AnimaSim redistrib) for mammals;
/// Kenney packs for tools once dropped under wwwroot/assets/meshes/tools/.
/// Crow / rabbit / boar / bear stay billboard until a matching CC0 GLB is added.
/// </summary>
public static class MeshCatalog
{
    public const string WildlifeRoot = "assets/meshes/wildlife/";
    public const string ToolsRoot = "assets/meshes/tools/";

    /// <summary>Primary wildlife shortlist keyed by EntityKind name.</summary>
    public static IReadOnlyDictionary<EntityKind, MeshCatalogEntry> Wildlife { get; } =
        new Dictionary<EntityKind, MeshCatalogEntry>
        {
            [EntityKind.Wolf] = new(
                "wildlife.wolf",
                WildlifeRoot + "wolf.glb",
                "Quaternius Ultimate Animated Animals (AnimaSim CC0 redistrib)",
                "CC0",
                "Walk/run/attack clips present in GLB; animation wiring is phase C."),
            [EntityKind.Deer] = new(
                "wildlife.deer",
                WildlifeRoot + "deer.glb",
                "Quaternius Ultimate Animated Animals (AnimaSim CC0 redistrib)",
                "CC0",
                "Primary prey silhouette."),
            [EntityKind.Bear] = new(
                "wildlife.bear",
                null, // no CC0 bear yet - husky stand-in looked wrong skinned
                "(billboard)",
                "n/a",
                "Billboard until a real bear GLB is vendored."),
            [EntityKind.Crow] = new(
                "wildlife.crow",
                null,
                "(billboard)",
                "n/a",
                "No CC0 crow GLB in shortlist yet - keep HUD billboard."),
            [EntityKind.Rabbit] = new(
                "wildlife.rabbit",
                null, // fox stand-in was misleading
                "(billboard)",
                "n/a",
                "Billboard until a rabbit GLB is vendored."),
            [EntityKind.Boar] = new(
                "wildlife.boar",
                null, // stag stand-in = antlers, not a boar
                "(billboard)",
                "n/a",
                "Billboard until a boar GLB is vendored. Do not map stag."),
        };

    /// <summary>First-person / held tool meshes (drop files into tools/).</summary>
    public static IReadOnlyList<MeshCatalogEntry> Tools { get; } =
    [
        new("tool.axe", ToolsRoot + "axe.glb", "Kenney (drop-in)", "CC0",
            "Place axe.glb from Kenney Prototype / Nature Kit under assets/meshes/tools/."),
        new("tool.pick", ToolsRoot + "pickaxe.glb", "Kenney (drop-in)", "CC0",
            "Place pickaxe.glb under assets/meshes/tools/."),
        new("tool.bow", ToolsRoot + "bow.glb", "Kenney (drop-in)", "CC0",
            "Place bow.glb under assets/meshes/tools/."),
    ];

    /// <summary>All entries that currently have a Path (candidates for preload).</summary>
    public static IEnumerable<MeshCatalogEntry> PreloadCandidates()
    {
        foreach (var e in Wildlife.Values)
            if (!string.IsNullOrEmpty(e.Path)) yield return e;
        foreach (var e in Tools)
            if (!string.IsNullOrEmpty(e.Path)) yield return e;
    }

    public static bool TryGetWildlife(EntityKind kind, out MeshCatalogEntry entry) =>
        Wildlife.TryGetValue(kind, out entry);
}
