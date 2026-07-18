using System.Collections.Generic;
using System.Linq;
using Aetherium.Components;
using Aetherium.Core;

namespace Aetherium.Server.Flying
{
    /// <summary>
    /// Builds Patterned <see cref="FlightPlan"/>s (data). The follower walks the resulting legs one tile per
    /// tick; legs need not be adjacent.
    /// </summary>
    public static class FlightPlanGenerator
    {
        /// <summary>Corners of a square ring of the given Chebyshev radius around a center at a fixed band.</summary>
        public static List<WorldLocation> Orbit(WorldLocation center, int radius, int band) => new List<WorldLocation>
        {
            new WorldLocation(center.X - radius, center.Y - radius, band),
            new WorldLocation(center.X + radius, center.Y - radius, band),
            new WorldLocation(center.X + radius, center.Y + radius, band),
            new WorldLocation(center.X - radius, center.Y + radius, band),
        };

        public static FlightPlan OrbitPlan(WorldLocation center, int radius, int band) => new FlightPlan
        {
            Source = FlightPlanSource.Patterned,
            PatternId = "orbit",
            Loop = LoopMode.Loop,
            Legs = Orbit(center, radius, band)
        };

        public static FlightPlan PatrolPlan(IEnumerable<WorldLocation> anchors, LoopMode loop = LoopMode.PingPong) => new FlightPlan
        {
            Source = FlightPlanSource.Patterned,
            PatternId = "patrol",
            Loop = loop,
            Legs = anchors.ToList()
        };

        /// <summary>
        /// A patrol whose legs are re-banded per the cruise rule for each leg's heading, so the route rides
        /// heading-appropriate altitude lanes.
        /// </summary>
        public static FlightPlan PatrolPlanWithCruiseBands(IReadOnlyList<WorldLocation> anchors, CruiseRule rule, LoopMode loop = LoopMode.PingPong)
        {
            var legs = new List<WorldLocation>();
            for (int i = 0; i < anchors.Count; i++)
            {
                var a = anchors[i];
                var band = a.Z;
                if (i > 0)
                {
                    var prev = anchors[i - 1];
                    var b = rule.BandForHeading(System.Math.Sign(a.X - prev.X), System.Math.Sign(a.Y - prev.Y));
                    if (b.HasValue) band = b.Value;
                }
                legs.Add(new WorldLocation(a.X, a.Y, band));
            }

            return new FlightPlan { Source = FlightPlanSource.Patterned, PatternId = "patrol", Loop = loop, Legs = legs };
        }

        public static FlightPlan HoverPlan(WorldLocation cell) => new FlightPlan
        {
            Source = FlightPlanSource.Patterned,
            PatternId = "hover",
            Loop = LoopMode.Loop,
            Legs = new List<WorldLocation> { cell }
        };

        public static FlightPlan WanderPlan(WorldLocation home, int radius) => new FlightPlan
        {
            Source = FlightPlanSource.Patterned,
            PatternId = "wander",
            Home = home,
            WanderRadius = radius
        };
    }
}
