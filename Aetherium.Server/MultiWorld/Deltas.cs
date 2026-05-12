using Orleans;

namespace Aetherium.Server.MultiWorld
{
    /// <summary>
    /// Base type for every grain-emitted state change pushed to sessions over SignalR.
    /// Sessions dispatch on the runtime type when applying to their local mirror.
    ///
    /// <para>
    /// Phase 2 uses per-event deltas (rather than diff snapshots or perception
    /// resends) so the wire cost is proportional to change volume, not player
    /// count × tick rate. Each subtype is independently <c>[GenerateSerializer]</c>
    /// because Orleans's polymorphic serializer needs each concrete shape
    /// registered with stable Ids.
    /// </para>
    /// </summary>
    [GenerateSerializer]
    public abstract class MapDelta
    {
        /// <summary>
        /// Monotonically increasing per-map sequence number assigned by the grain.
        /// Clients can use this to detect dropped deltas (gap in sequence) and
        /// trigger a resync. Defaults to 0; the grain stamps it before fan-out.
        /// </summary>
        [Id(0)] public long Sequence { get; set; }

        /// <summary>Map ID the delta applies to. Sessions filter by this if they
        /// might be subscribed to multiple maps.</summary>
        [Id(1)] public string MapId { get; set; } = string.Empty;
    }

    /// <summary>
    /// An entity was added to the map. Carries enough <see cref="EntityPlacement"/>
    /// metadata that the receiving session can reconstruct an equivalent entity
    /// via <c>EntityFactory.Create</c>.
    /// </summary>
    [GenerateSerializer]
    public class EntityAddedDelta : MapDelta
    {
        [Id(0)] public EntityPlacement Placement { get; set; } = new();
    }

    /// <summary>An entity was removed from the map.</summary>
    [GenerateSerializer]
    public class EntityRemovedDelta : MapDelta
    {
        [Id(0)] public string EntityId { get; set; } = string.Empty;
        [Id(1)] public int LastX { get; set; }
        [Id(2)] public int LastY { get; set; }
        [Id(3)] public int LastZ { get; set; }
    }

    /// <summary>
    /// An entity moved from one cell to another. Carries both endpoints so the
    /// session can update its location index without inferring the old position.
    /// </summary>
    [GenerateSerializer]
    public class EntityMovedDelta : MapDelta
    {
        [Id(0)] public string EntityId { get; set; } = string.Empty;
        [Id(1)] public int OldX { get; set; }
        [Id(2)] public int OldY { get; set; }
        [Id(3)] public int OldZ { get; set; }
        [Id(4)] public int NewX { get; set; }
        [Id(5)] public int NewY { get; set; }
        [Id(6)] public int NewZ { get; set; }
    }

    /// <summary>
    /// An entity's <c>HasHeading</c> component changed. Sent only to the actor's
    /// own session in phase 2c — other players don't perceive heading by default
    /// (perception-pure principle; a future change can add a "compass reveals
    /// nearby characters' headings" perception filter).
    /// </summary>
    [GenerateSerializer]
    public class EntityHeadingChangedDelta : MapDelta
    {
        [Id(0)] public string EntityId { get; set; } = string.Empty;
        [Id(1)] public int Degrees { get; set; }
    }

    /// <summary>
    /// A door's <c>OpensAndCloses</c> state changed (open/close/lock/unlock).
    /// </summary>
    [GenerateSerializer]
    public class DoorStateChangedDelta : MapDelta
    {
        [Id(0)] public string EntityId { get; set; } = string.Empty;
        [Id(1)] public bool IsOpen { get; set; }
        [Id(2)] public bool IsLocked { get; set; }
    }

    /// <summary>
    /// An item moved between the world and an entity's inventory. Atomic from the
    /// session's perspective — the receiver applies world and inventory updates
    /// in one critical section.
    /// </summary>
    [GenerateSerializer]
    public class ItemTransferredDelta : MapDelta
    {
        /// <summary>Item being transferred.</summary>
        [Id(0)] public string ItemEntityId { get; set; } = string.Empty;

        /// <summary>
        /// True when moving from world → inventory (pickup). False when moving
        /// from inventory → world (drop).
        /// </summary>
        [Id(1)] public bool IntoInventory { get; set; }

        /// <summary>The Character that gained or lost the item.</summary>
        [Id(2)] public string OwnerEntityId { get; set; } = string.Empty;

        /// <summary>For pickups: the world location the item was removed from.
        /// For drops: the world location the item was placed at.</summary>
        [Id(3)] public int X { get; set; }
        [Id(4)] public int Y { get; set; }
        [Id(5)] public int Z { get; set; }

        /// <summary>
        /// For pickups: a placement record describing the item type/IDs so the
        /// owner's session can reconstruct it in inventory without a separate
        /// snapshot fetch. Null for drops (the item already exists in inventory).
        /// </summary>
        [Id(6)] public EntityPlacement? ItemPlacement { get; set; }
    }

    /// <summary>
    /// A heat trail was recorded at a location. Heat is grain-authoritative
    /// (per design D9); sessions maintain a local mirror that converges via these
    /// deltas. Whether a session can <em>perceive</em> a given trail remains a
    /// session-side filter (VisionMode.Infrared vs Normal).
    /// </summary>
    [GenerateSerializer]
    public class HeatRecordedDelta : MapDelta
    {
        [Id(0)] public string EntityId { get; set; } = string.Empty;
        [Id(1)] public int X { get; set; }
        [Id(2)] public int Y { get; set; }
        [Id(3)] public int Z { get; set; }
        [Id(4)] public double GameTimeHours { get; set; }
        [Id(5)] public double Intensity { get; set; }
    }

    /// <summary>A heat trail expired (decayed below the retention threshold).</summary>
    [GenerateSerializer]
    public class HeatExpiredDelta : MapDelta
    {
        [Id(0)] public int X { get; set; }
        [Id(1)] public int Y { get; set; }
        [Id(2)] public int Z { get; set; }
    }
}
