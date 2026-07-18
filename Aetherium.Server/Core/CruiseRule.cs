using System;

namespace Aetherium.Core
{
    /// <summary>
    /// A semicircular cruising-altitude rule (as real VFR pilots use): opposing traffic is separated by
    /// altitude, so a dense sky of autonomous flyers self-separates without per-pair avoidance. Per-world
    /// data — plan generators pick a leg's band from its heading via <see cref="BandForHeading"/>.
    /// </summary>
    public class CruiseRule
    {
        /// <summary>Bands used by broadly east/north-bound traffic (e.g. even bands).</summary>
        public int[] EastboundBands { get; set; } = new[] { 2, 4 };

        /// <summary>Bands used by broadly west/south-bound traffic (e.g. odd bands).</summary>
        public int[] WestboundBands { get; set; } = new[] { 3, 5 };

        /// <summary>
        /// Preferred cruise band for a horizontal heading (dx,dy). East/north-bound headings draw from
        /// <see cref="EastboundBands"/>; west/south-bound from <see cref="WestboundBands"/>. Returns null for
        /// no horizontal movement.
        /// </summary>
        public int? BandForHeading(int dx, int dy)
        {
            if (dx == 0 && dy == 0)
                return null;

            // Note: screen Y increases southward, so northbound is dy < 0.
            bool eastOrNorth = dx > 0 || (dx == 0 && dy < 0);
            var bands = eastOrNorth ? EastboundBands : WestboundBands;
            return bands.Length > 0 ? bands[0] : (int?)null;
        }
    }
}
