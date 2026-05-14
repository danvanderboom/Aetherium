using System.Collections.Generic;
using Aetherium.Components;

namespace Aetherium.WorldGen
{
    /// <summary>
    /// Utility that returns the cardinal and 3-D axis-aligned neighbors of a
    /// <see cref="WorldLocation"/>. Centralizes a pattern that was copy-pasted across
    /// <c>FloodFill</c>, <c>EnsureExitsFeature</c>, <c>GenerationValidationService</c>,
    /// and <c>PortalNetworkPass</c>.
    /// </summary>
    public static class WorldLocationNeighbors
    {
        /// <summary>
        /// (dx, dy) offsets for the four cardinal directions. Use when you need both the
        /// neighbor location <em>and</em> the delta (e.g., to step one more cell in the same
        /// direction). For neighbor locations only, prefer <see cref="Cardinal4(WorldLocation)"/>.
        /// </summary>
        public static readonly (int dx, int dy)[] Cardinal4Offsets =
        {
            ( 1,  0),
            (-1,  0),
            ( 0,  1),
            ( 0, -1),
        };

        /// <summary>
        /// Returns the four cardinal (N/S/E/W) neighbors of <paramref name="loc"/>
        /// at the same Z level.
        /// </summary>
        public static IEnumerable<WorldLocation> Cardinal4(WorldLocation loc)
        {
            yield return loc.FromDelta( 1,  0, 0);
            yield return loc.FromDelta(-1,  0, 0);
            yield return loc.FromDelta( 0,  1, 0);
            yield return loc.FromDelta( 0, -1, 0);
        }

        /// <summary>
        /// Returns the six axis-aligned neighbors of <paramref name="loc"/>: the four cardinal
        /// directions in the XY plane plus one step up and one step down in Z.
        /// </summary>
        public static IEnumerable<WorldLocation> Cardinal6(WorldLocation loc)
        {
            yield return loc.FromDelta( 1,  0,  0);
            yield return loc.FromDelta(-1,  0,  0);
            yield return loc.FromDelta( 0,  1,  0);
            yield return loc.FromDelta( 0, -1,  0);
            yield return loc.FromDelta( 0,  0,  1);
            yield return loc.FromDelta( 0,  0, -1);
        }
    }
}
