using System.Numerics;

namespace LostSpawns.Services;

/// <summary>
/// One piece of loot sitting on the ground waiting to be picked up. Position
/// is world-space voxel coords; Payload is the InventoryItem that will be
/// TryAdded to the player when they walk close enough.
/// </summary>
public sealed class GroundItem
{
    public int Id { get; init; }
    public Vector3 Position;
    public InventoryItem Payload { get; init; } = null!;
}

/// <summary>
/// Tracks loot sitting on the ground. Entities that die spawn a GroundItem at
/// their death location; the tick checks player proximity and TryAdds the
/// payload into inventory when the player walks close enough. Failed add
/// (full inventory) leaves the ground item in place so the player can
/// retrieve it after emptying a slot.
///
/// Ground items are non-saved for MVP - between reloads the world culls
/// any uncollected loot. A follow-up can persist them the same way
/// Campfires are.
/// </summary>
public class GroundItemService
{
    private int _nextId = 1;
    public List<GroundItem> Items { get; } = new();

    /// <summary>How close the player has to be for auto-pickup (blocks).</summary>
    public float PickupRange { get; set; } = 2.0f;

    /// <summary>Fired after a successful pickup. Payload = picked-up item.</summary>
    public event Action<InventoryItem>? OnPickedUp;

    /// <summary>Fired after a successful pickup with the world-space position of the picked item.</summary>
    public event Action<InventoryItem, Vector3>? OnPickedUpAt;

    /// <summary>Fired after a successful pickup with the ground item id so UI can clean up per-id state (minimap markers, etc).</summary>
    public event Action<int>? OnPickedUpId;

    public GroundItem Drop(Vector3 pos, InventoryItem payload)
    {
        var g = new GroundItem
        {
            Id = _nextId++,
            Position = pos,
            Payload = payload,
        };
        Items.Add(g);
        return g;
    }

    /// <summary>
    /// Check each ground item for proximity to the player and try to auto-
    /// pickup. Loops backwards so we can remove in-place without iterator
    /// issues. Pickups that fail (full inventory) are left on the ground.
    /// </summary>
    public void Tick(Vector3 playerPos, InventoryService inventory)
    {
        for (int i = Items.Count - 1; i >= 0; i--)
        {
            var g = Items[i];
            float dx = playerPos.X - g.Position.X;
            float dz = playerPos.Z - g.Position.Z;
            if (dx * dx + dz * dz > PickupRange * PickupRange) continue;

            if (inventory.TryAdd(g.Payload))
            {
                int id = g.Id;
                var pickupPos = g.Position;
                var payload = g.Payload;
                Items.RemoveAt(i);
                OnPickedUp?.Invoke(payload);
                OnPickedUpId?.Invoke(id);
                OnPickedUpAt?.Invoke(payload, pickupPos);
            }
            // else: inventory full, leave the item for next pass.
        }
    }
}
