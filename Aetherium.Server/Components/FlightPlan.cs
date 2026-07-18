using System.Collections.Generic;
using Aetherium.Core;

namespace Aetherium.Components
{
    /// <summary>How a flight plan is sourced. Manual plans are driven directly by a controller (piloting).</summary>
    public enum FlightPlanSource
    {
        Manual,
        AdHoc,
        Scheduled,
        Patterned
    }

    /// <summary>What happens at the end of a plan's leg list.</summary>
    public enum LoopMode
    {
        Once,
        Loop,
        PingPong
    }

    /// <summary>
    /// An ordered path a flyer follows over time. The follower advances one tile per tick toward the current
    /// leg (<see cref="Cursor"/>) and advances the cursor per <see cref="Loop"/> on arrival. Patterned plans
    /// (orbit/patrol/hover) carry explicit <see cref="Legs"/>; "wander" is evaluated dynamically from
    /// <see cref="Home"/>/<see cref="WanderRadius"/>.
    /// </summary>
    public class FlightPlan : Component
    {
        public FlightPlanSource Source { get; set; } = FlightPlanSource.Patterned;

        /// <summary>For Patterned plans: "orbit", "patrol", "wander", or "hover".</summary>
        public string PatternId { get; set; } = string.Empty;

        public List<WorldLocation> Legs { get; set; } = new List<WorldLocation>();

        public LoopMode Loop { get; set; } = LoopMode.Loop;

        /// <summary>Index of the current target leg.</summary>
        public int Cursor { get; set; } = 0;

        /// <summary>Travel direction for <see cref="LoopMode.PingPong"/> (+1 forward, -1 backward).</summary>
        public int Direction { get; set; } = 1;

        /// <summary>Set true when a <see cref="LoopMode.Once"/> plan reaches its final leg.</summary>
        public bool Complete { get; set; } = false;

        // --- Wander parameters (PatternId == "wander") ---
        /// <summary>Center the flyer wanders around; defaults to its position when the plan starts.</summary>
        public WorldLocation? Home { get; set; }
        /// <summary>Maximum Chebyshev distance from <see cref="Home"/> a wanderer strays.</summary>
        public int WanderRadius { get; set; } = 3;

        public FlightPlan() : base() { }
    }
}
