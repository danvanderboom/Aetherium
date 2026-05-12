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

    /// <summary>
    /// A single numeric, boolean, or string field on a component changed in place.
    /// Generic carrier so we don't need one delta type per (component, field) pair;
    /// receivers dispatch on <see cref="ComponentType"/>+<see cref="FieldName"/> in
    /// <c>GameSession.ApplyDelta</c>.
    ///
    /// <para>
    /// Exactly one of <see cref="NumericValue"/>, <see cref="BoolValue"/>,
    /// <see cref="StringValue"/> is populated per delta; the field's actual type
    /// is implied by the (ComponentType, FieldName) pair. See design.md for the
    /// trade-off vs per-component delta classes.
    /// </para>
    /// </summary>
    [GenerateSerializer]
    public class ComponentFieldChangedDelta : MapDelta
    {
        [Id(0)] public string EntityId { get; set; } = string.Empty;
        [Id(1)] public string ComponentType { get; set; } = string.Empty;
        [Id(2)] public string FieldName { get; set; } = string.Empty;
        [Id(3)] public double? NumericValue { get; set; }
        [Id(4)] public bool? BoolValue { get; set; }
        [Id(5)] public string? StringValue { get; set; }
    }

    /// <summary>
    /// An item was removed from the simulation entirely (consumed to zero uses or
    /// destroyed when durability hit zero). Distinct from
    /// <see cref="ItemTransferredDelta"/> because there is no destination — the
    /// item ceases to exist.
    ///
    /// <para>
    /// When <see cref="OwnerEntityId"/> is set, the item lived in that character's
    /// inventory and the receiver removes it from there. When null, the item lived
    /// in the world and the receiver removes it from <c>World.Entities</c>.
    /// </para>
    /// </summary>
    [GenerateSerializer]
    public class ItemDestroyedDelta : MapDelta
    {
        [Id(0)] public string EntityId { get; set; } = string.Empty;
        [Id(1)] public string? OwnerEntityId { get; set; }
    }

    /// <summary>
    /// An item transitioned from a player's inventory into the world at a location
    /// (e.g. placing a torch). The <see cref="EntityPlacement"/> carries the item's
    /// post-mutation component state so receivers can reconstruct it via
    /// <c>EntityFactory.Create</c> with <c>IsPlaced</c>/<c>IsEnabled</c> flags
    /// already in their new positions.
    ///
    /// <para>
    /// Distinct from <see cref="EntityAddedDelta"/>: that delta represents the
    /// grain spawning a fresh entity. This one represents an instance the
    /// receiver already had in its inventory mirror crossing over to the world.
    /// Receivers must remove the item from <see cref="SourceOwnerEntityId"/>'s
    /// inventory before adding it to the world to avoid a double-reference.
    /// </para>
    /// </summary>
    [GenerateSerializer]
    public class EntityPlacedDelta : MapDelta
    {
        [Id(0)] public EntityPlacement Placement { get; set; } = new();
        [Id(1)] public string? SourceOwnerEntityId { get; set; }
    }
}
