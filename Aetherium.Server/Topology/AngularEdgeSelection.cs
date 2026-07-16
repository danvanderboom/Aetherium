// RelativeDirection is spelled in full throughout: the enclosing Aetherium namespace
// also defines an (unrelated) Aetherium.RelativeDirection, which shadows any
// file-scoped using-alias, so the qualified name is the unambiguous choice.
namespace Aetherium.Topology
{
    /// <summary>
    /// The one place relative-movement semantics live (docs/grid-topologies.md, Rule 2):
    /// target angle = heading + {F:0°, R:90°, B:180°, L:270°}; the outgoing edge nearest
    /// that angle wins, with deterministic tie-breaks (a) toward forward, then
    /// (b) clockwise. Shared by every topology so game-feel disputes have a single
    /// method to argue with. On square this reproduces the legacy
    /// cardinalize-then-rotate pair byte-identically (pinned by golden tests) —
    /// including the legacy quirk that non-planar values (Up/Down) resolve as Forward.
    /// </summary>
    internal static class AngularEdgeSelection
    {
        public static RelativeMoveResolution Resolve(IGridTopology topology, GridCoord cell,
            int headingDegrees, Aetherium.Model.RelativeDirection move)
        {
            int offset = move switch
            {
                Aetherium.Model.RelativeDirection.Forward => 0,
                Aetherium.Model.RelativeDirection.Right => 90,
                Aetherium.Model.RelativeDirection.Backward => 180,
                Aetherium.Model.RelativeDirection.Left => 270,
                _ => 0, // legacy quirk preserved: Up/Down fell through the old rotation switch as Forward
            };
            // Snap the heading to a legal facing FIRST, then rotate — mirroring the legacy
            // square path (DegreesToCardinal → rotate), where a 45° heading resolves to a
            // cardinal before the relative turn is applied. On square this makes the offset
            // land exactly on an edge (no tie); on triangle it aligns forward with an actual
            // edge so the tie-breaks below only arbitrate the genuinely-ambiguous Backward.
            int forward = topology.SnapHeading(cell, headingDegrees);
            int target = Normalize(forward + offset);

            EdgeStep? best = null;
            (int Dist, int ForwardDist, int CounterClockwise) bestKey = default;

            foreach (var step in topology.Steps(cell))
            {
                var key = (
                    Dist: AngularDistance(step.HeadingDegrees, target),
                    ForwardDist: AngularDistance(step.HeadingDegrees, forward),
                    // Tie-break (b): prefer the clockwise side of the target angle —
                    // signed delta in (0, 180] means the edge sits clockwise of it.
                    CounterClockwise: IsClockwiseOf(step.HeadingDegrees, target) ? 0 : 1);

                if (best is null || Compare(key, bestKey) < 0)
                {
                    best = step;
                    bestKey = key;
                }
            }

            if (best is null)
                return new RelativeMoveResolution(false, default, headingDegrees, "Cell has no outgoing edges");

            return new RelativeMoveResolution(true, best.Value, best.Value.HeadingDegrees, null);
        }

        public static int Normalize(int degrees)
        {
            degrees %= 360;
            return degrees < 0 ? degrees + 360 : degrees;
        }

        /// <summary>Unsigned angular distance in [0, 180].</summary>
        public static int AngularDistance(int a, int b)
        {
            int d = Normalize(a - b);
            return d > 180 ? 360 - d : d;
        }

        private static bool IsClockwiseOf(int edgeDegrees, int targetDegrees)
        {
            int d = Normalize(edgeDegrees - targetDegrees);
            return d > 0 && d <= 180;
        }

        private static int Compare(
            (int Dist, int ForwardDist, int CounterClockwise) x,
            (int Dist, int ForwardDist, int CounterClockwise) y)
        {
            int c = x.Dist.CompareTo(y.Dist);
            if (c != 0) return c;
            c = x.ForwardDist.CompareTo(y.ForwardDist);
            if (c != 0) return c;
            return x.CounterClockwise.CompareTo(y.CounterClockwise);
        }
    }
}
