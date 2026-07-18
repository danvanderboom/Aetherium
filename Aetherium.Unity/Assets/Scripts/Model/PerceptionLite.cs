#nullable enable
using System;
using System.Collections.Generic;

namespace Aetherium.Unity.Model
{
    /// <summary>
    /// Unity-friendly representation of PerceptionDto (subset needed for 2D tilemap rendering).
    /// </summary>
    [Serializable]
    public class PerceptionLite
    {
        public WorldLocationLite PlayerLocation { get; set; } = new WorldLocationLite();
        public WorldDirectionLite PlayerHeading { get; set; } = WorldDirectionLite.North;
        public int HeadingDegrees { get; set; } = 0;
        public RectangleLite VisibleBounds { get; set; } = new RectangleLite();
        public Dictionary<string, VisualLite> Visuals { get; set; } = new Dictionary<string, VisualLite>();
        public Dictionary<string, TileTypeLite> TileTypes { get; set; } = new Dictionary<string, TileTypeLite>();

        /// <summary>
        /// The perceiver's flight envelope (band range + current band), present only when they can fly/pilot.
        /// Drives the HUD altitude gauge. Null for non-flyers — additive, mirrors <c>PerceptionDto.FlightEnvelope</c>.
        /// </summary>
        public FlightEnvelopeLite? FlightEnvelope { get; set; }

        public PerceptionLite()
        {
        }
    }
}

