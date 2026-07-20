using Aetherium.Core;

namespace Aetherium.Components
{
    /// <summary>
    /// Marks a tile as a transit station — a stop on a line where a scheduled service's train parks so
    /// players board/alight (add-transit-networks Phase 1). A lightweight, <em>non-obstructing</em> marker:
    /// it labels the platform (the train's <c>Boardable</c> exterior is the actual board hotspot) and lets
    /// systems and the client find where a line stops (<c>Has&lt;Station&gt;()</c> scans, like
    /// <see cref="Settlement"/>). Placed at a settlement's core cell, co-located with the settlement.
    /// </summary>
    public class Station : Component
    {
        /// <summary>The line this station belongs to (e.g. "rail"), matching the service's LineId.</summary>
        public string LineId { get; set; } = string.Empty;

        /// <summary>The station's ordinal position along the line, so the ordered route is recoverable.</summary>
        public int StopIndex { get; set; }

        /// <summary>Display name of the station — usually the settlement it serves.</summary>
        public string Name { get; set; } = string.Empty;

        public Station() : base() { }
    }
}
