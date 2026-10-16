using System.Numerics;

namespace LostSpawns.Services;

/// <summary>The player's bedroll, if one is laid: where they wake after dying (otherwise at the start of the island). One at a time; laying another takes the first up.</summary>
public sealed class BedrollService
{
    /// <summary>The surface point it lies on (null: none laid).</summary>
    public Vector3? Position { get; set; }
}
