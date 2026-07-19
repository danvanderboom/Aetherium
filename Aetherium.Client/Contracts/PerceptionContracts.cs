using System;
using System.Collections.Generic;

namespace Aetherium.Client.Contracts
{
    // Hand-written mirrors of the server's wire DTOs (Aetherium.Model), matching property
    // names (PascalCase) and shapes exactly. The client deliberately does NOT reference
    // Aetherium.Model — it carries Orleans.Sdk attributes and dependencies that must never
    // enter a Unity build. Drift is retired structurally: Aetherium.Client.Tests references
    // BOTH assemblies and (a) round-trips fully-populated server DTOs through the hub's JSON
    // into these mirrors asserting field equality, (b) reflection-sweeps every server wire
    // property for a mirror counterpart. A new server field breaks that build, not a game.

    public class WorldLocationDto
    {
        public int X { get; set; }
        public int Y { get; set; }
        public int Z { get; set; }

        public WorldLocationDto() { }

        public WorldLocationDto(int x, int y, int z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public override bool Equals(object? obj) =>
            obj is WorldLocationDto other && X == other.X && Y == other.Y && Z == other.Z;

        public override int GetHashCode() => (X, Y, Z).GetHashCode();

        public override string ToString() => $"({X}, {Y}, {Z})";
    }

    public class RectangleDto
    {
        public int X { get; set; }
        public int Y { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
    }

    public class TileTypeDto
    {
        public string Name { get; set; } = string.Empty;
        public Dictionary<string, string> Settings { get; set; } = new Dictionary<string, string>();
    }

    public class VisualDto
    {
        public WorldLocationDto Location { get; set; } = new WorldLocationDto();
        /// <summary>Terrain by reference into the frame's <c>TileTypes</c> palette (full name+settings),
        /// null when the cell has no terrain. Replaced an embedded per-cell TileTypeDto that measured
        /// ~43% of a frame's bytes; <see cref="PerceptionStore"/> resolves it back to a full terrain type
        /// against the palette when folding a frame. (perception efficiency)</summary>
        public string? TileTypeId { get; set; }
        public List<TileTypeDto> Entities { get; set; } = new List<TileTypeDto>();
        public double LightLevel { get; set; } = 1.0;
        public Dictionary<VisualType, int> ThingsSeen { get; set; } = new Dictionary<VisualType, int>();
    }

    /// <summary>
    /// A visible monster/NPC or other player. Creature identity rides in
    /// <see cref="Name"/>/<see cref="Tile"/>.Name as <c>Creature:&lt;contentId&gt;</c> —
    /// theme binding keys off that content id.
    /// </summary>
    public class CharacterDto
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = "Character";
        public TileTypeDto? Tile { get; set; }
        public bool IsHostile { get; set; }
        public WorldLocationDto? Location { get; set; }
    }

    public class ItemDto
    {
        public string Id { get; set; } = string.Empty;
        public string Label { get; set; } = "Item";
        public string Icon { get; set; } = "?";
        public string? KeyId { get; set; }
        public WorldLocationDto? Location { get; set; }
    }

    public class InventoryDto
    {
        public int Capacity { get; set; }
        public List<ItemDto> Items { get; set; } = new List<ItemDto>();
    }

    public class AffordanceUsageDto
    {
        public string UsageId { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public string? TargetId { get; set; }
    }

    public class AffordanceDto
    {
        public string Action { get; set; } = string.Empty;
        public string ActorId { get; set; } = string.Empty;
        public string? TargetId { get; set; }
        public string? ItemId { get; set; }
        public string? RequiresKeyId { get; set; }
        public List<AffordanceUsageDto> UsageOptions { get; set; } = new List<AffordanceUsageDto>();
    }

    public class NavigationDataDto
    {
        public bool HasCompass { get; set; }
        public int HeadingDegrees { get; set; }
        public WorldDirection CardinalDirection { get; set; }
    }

    public class AmbientEmitterDto
    {
        public int X { get; set; }
        public int Y { get; set; }
        public int Z { get; set; }
        public string TrackName { get; set; } = string.Empty;
        public float Volume { get; set; } = 0.5f;
        public bool Loop { get; set; } = true;
    }

    public class AudioPerceptionDto
    {
        public string? Biome { get; set; }
        public float DangerLevel { get; set; }
        public string ReverbPreset { get; set; } = "outdoor";
        public float Occlusion { get; set; }
        public Dictionary<string, AmbientEmitterDto> AmbientEmitters { get; set; } = new Dictionary<string, AmbientEmitterDto>();
        public string? SuggestedMusicTrack { get; set; }
        public string FootstepMaterial { get; set; } = "stone";
    }

    /// <summary>Interoception — the player's own body state (add-interoception-channel).
    /// Null on frames from servers that never populated it.</summary>
    public class InteroceptionDto
    {
        public int Health { get; set; }
        public int MaxHealth { get; set; }
        public List<SelfStatusDto> Statuses { get; set; } = new List<SelfStatusDto>();
        public List<ResourcePoolStateDto> Pools { get; set; } = new List<ResourcePoolStateDto>();
        public List<AbilityReadinessDto> Cooldowns { get; set; } = new List<AbilityReadinessDto>();
    }

    public class SelfStatusDto
    {
        public string Id { get; set; } = string.Empty;
        public int RemainingTicks { get; set; }
    }

    public class ResourcePoolStateDto
    {
        public string Tag { get; set; } = string.Empty;
        public double Current { get; set; }
        public double Max { get; set; }
        public bool IsInverse { get; set; }
    }

    public class AbilityReadinessDto
    {
        public string AbilityId { get; set; } = string.Empty;
        public int RemainingTicks { get; set; }
    }

    /// <summary>
    /// The full perception frame the server pushes (on connect, after your actions, and on
    /// map changes). All coordinates are player-relative — <see cref="PlayerLocation"/> is
    /// always (0,0,0) and <see cref="Visuals"/> keys are "relX,relY,relZ" strings. The
    /// PerceptionStore turns these into stable client-space coordinates via anchoring.
    /// </summary>
    public class PerceptionDto
    {
        public WorldLocationDto PlayerLocation { get; set; } = new WorldLocationDto();
        public WorldDirection PlayerHeading { get; set; } = WorldDirection.North;
        public int HeadingDegrees { get; set; }
        public bool IsDirectionalVision { get; set; }
        public int FieldOfViewDegrees { get; set; } = 120;

        /// <summary>"square" | "hex" | "tri" | "h3" — picks the client cell-layout math.</summary>
        public string Topology { get; set; } = "square";

        /// <summary>Triangle worlds only: the perceiver's own cell parity (0 up, 1 down).</summary>
        public int? SelfCellParity { get; set; }

        public Dictionary<string, VisualDto> Visuals { get; set; } = new Dictionary<string, VisualDto>();
        public RectangleDto VisibleBounds { get; set; } = new RectangleDto();
        public Guid UpdateTimestamp { get; set; }
        public Dictionary<string, TileTypeDto> TileTypes { get; set; } = new Dictionary<string, TileTypeDto>();

        public InventoryDto? Inventory { get; set; }
        public List<ItemDto> VisibleItems { get; set; } = new List<ItemDto>();
        public List<CharacterDto> VisibleCharacters { get; set; } = new List<CharacterDto>();
        public List<AffordanceDto> Affordances { get; set; } = new List<AffordanceDto>();
        public NavigationDataDto? NavigationData { get; set; }

        public LightingMode CurrentLightingMode { get; set; } = LightingMode.Torch;
        public VisionMode CurrentVisionMode { get; set; } = VisionMode.Normal;
        public double GameTimeOfDay { get; set; } = 12.0;

        /// <summary>The server-side tuple's wire quirk: System.Text.Json writes ValueTuple
        /// fields only when configured to — mirrored as a plain object so whichever shape
        /// arrives, deserialization never throws. Drift tests pin the actual behavior.</summary>
        public AmbientTintDto AmbientTint { get; set; } = new AmbientTintDto();

        public string Weather { get; set; } = "Clear";
        public string Season { get; set; } = "spring";

        public AudioPerceptionDto? Audio { get; set; }

        /// <summary>Own body state; null from pre-interoception servers.</summary>
        public InteroceptionDto? Interoception { get; set; }

        /// <summary>Server count of this player's successful anchor-changing moves when the
        /// frame was computed (starts at 1; 0 = unsequenced legacy). The store uses it to
        /// drop stale frames and defer ahead-of-anchor ones.</summary>
        public long MoveSequence { get; set; }
    }

    /// <summary>Mirror of the server's <c>(double r, double g, double b)</c> ambient-tint
    /// tuple (ValueTuple members serialize as Item1/Item2/Item3 when fields are included;
    /// as an empty object otherwise). Defaults to neutral white.</summary>
    public class AmbientTintDto
    {
        public double Item1 { get; set; } = 1.0;
        public double Item2 { get; set; } = 1.0;
        public double Item3 { get; set; } = 1.0;
    }

    public class GameStateDto
    {
        public string PlayerId { get; set; } = string.Empty;
        public WorldDirection PlayerHeading { get; set; } = WorldDirection.North;
        public Dictionary<string, TileTypeDto> TileTypes { get; set; } = new Dictionary<string, TileTypeDto>();

        /// <summary>Secret presented to ResumeSession after a reconnect to rebind to this
        /// session instead of starting over as a fresh spawn.</summary>
        public string ResumeToken { get; set; } = string.Empty;
    }

    /// <summary>Mirror of the server's ResumeSessionResultDto. On success,
    /// <see cref="Perception"/> is the resumed session's current frame — applied in place
    /// of the fresh-session frames the reconnect handshake pushed (those are discarded).</summary>
    public class ResumeSessionResultDto
    {
        public bool Success { get; set; }
        public string? Reason { get; set; }
        public PerceptionDto? Perception { get; set; }
    }

    /// <summary>A player's own life-state, pushed to the owning session only via
    /// ReceiveDowned/ReceiveRespawn/ReceiveDied.</summary>
    public class PlayerVitalsDto
    {
        public int Health { get; set; }
        public int MaxHealth { get; set; }
        public bool IsDowned { get; set; }
        public int DownedTicksRemaining { get; set; }
        public bool IsInvulnerable { get; set; }
    }
}
