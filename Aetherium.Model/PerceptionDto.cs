using System;
using System.Collections.Generic;

namespace Aetherium.Model
{
    public class PerceptionDto
    {
        public WorldLocationDto PlayerLocation { get; set; } = new WorldLocationDto();
        public WorldDirection PlayerHeading { get; set; } = WorldDirection.North;
        
        /// <summary>
        /// Heading in degrees (0-359). 0 = North, 90 = East, 180 = South, 270 = West.
        /// More precise than the cardinal PlayerHeading enum.
        /// </summary>
        public int HeadingDegrees { get; set; } = 0;
        
        /// <summary>
        /// Whether directional vision mode is active.
        /// When true, the player can only see within a forward-facing cone.
        /// </summary>
        public bool IsDirectionalVision { get; set; } = false;
        
        /// <summary>
        /// Field of view in degrees when directional vision is active.
        /// </summary>
        public int FieldOfViewDegrees { get; set; } = 120;
        
        /// <summary>
        /// The world's tiling (docs/grid-topologies.md): "square" (default) | "hex" | "tri" |
        /// (later) "h3". The client picks its cell-layout math from this. Relative coordinate
        /// keys, <see cref="VisibleBounds"/>, and <see cref="RelativeDirection"/> are unchanged
        /// on every topology; only the client-side axial→screen transform differs.
        /// </summary>
        public string Topology { get; set; } = "square";

        /// <summary>
        /// On a triangular world, whether the perceiver's own cell points up (0) or down (1) —
        /// its <c>(X+Y)&amp;1</c> parity. Relative deltas alone can't tell the client which way its
        /// triangle faces, and absolute coordinates are deliberately hidden, so this one bit is
        /// surfaced explicitly. Null on non-triangular worlds (square/hex cells have no parity).
        /// </summary>
        public int? SelfCellParity { get; set; }

        public Dictionary<string, VisualDto> Visuals { get; set; } = new Dictionary<string, VisualDto>();
        public RectangleDto VisibleBounds { get; set; } = new RectangleDto();
        public Guid UpdateTimestamp { get; set; } = Guid.NewGuid();
        public Dictionary<string, TileTypeDto> TileTypes { get; set; } = new Dictionary<string, TileTypeDto>();

        // Inventory and interactions (AI-friendly)
        public InventoryDto? Inventory { get; set; }
        public List<ItemDto> VisibleItems { get; set; } = new List<ItemDto>();

        /// <summary>
        /// Other characters (monsters/NPCs and co-located players) visible in the
        /// player's field of view, with relative coordinates. The perceiving player
        /// is excluded — they are always the center marker. Parallels
        /// <see cref="VisibleItems"/>; the client renders these above terrain.
        /// </summary>
        public List<CharacterDto> VisibleCharacters { get; set; } = new List<CharacterDto>();

        public List<AffordanceDto> Affordances { get; set; } = new List<AffordanceDto>();

        // Navigation data (compass, maps, etc.)
        public NavigationDataDto? NavigationData { get; set; }

        // Lighting and vision modes
        public LightingMode CurrentLightingMode { get; set; } = LightingMode.Torch;
        public VisionMode CurrentVisionMode { get; set; } = VisionMode.Normal;
        public double GameTimeOfDay { get; set; } = 12.0; // 0-24 hours
        public (double r, double g, double b) AmbientTint { get; set; } = (1.0, 1.0, 1.0);
        public string Weather { get; set; } = "Clear";
        public string Season { get; set; } = "spring";

        // Audio perception
        public AudioPerceptionDto? Audio { get; set; }

        /// <summary>
        /// Interoception — the perceiver's own body state (health, felt statuses, resource
        /// pools, ability readiness). Null when the frame was computed without a perceiving
        /// self entity (openspec/changes/add-interoception-channel); always self-only.
        /// </summary>
        public InteroceptionDto? Interoception { get; set; }

        /// <summary>
        /// Monotonic count of the perceiving player's own successful anchor-changing moves
        /// (steps + level changes) at the moment this frame was computed. Starts at 1; a
        /// value of 0 means an unsequenced legacy producer. Lets clients order frames
        /// against their own movement: a frame carrying an older count than the client's
        /// move tally is stale (computed pre-move, delivered post-response) and must not
        /// be folded into position-anchored state.
        /// </summary>
        public long MoveSequence { get; set; }

        public PerceptionDto()
        {
        }
    }
}


