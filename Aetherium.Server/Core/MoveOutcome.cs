using Aetherium.Components;

namespace Aetherium.Core
{
    /// <summary>
    /// Result of a validated movement attempt (<see cref="World.TryMoveSteps"/> /
    /// <see cref="World.TryChangeLevel"/>). A multi-cell request can succeed
    /// partially: <see cref="Success"/> means at least one step was taken;
    /// <see cref="BlockedReason"/> is set whenever fewer steps than requested
    /// were possible (including the zero-step failure case).
    /// </summary>
    public sealed class MoveOutcome
    {
        /// <summary>At least one step was taken.</summary>
        public bool Success { get; init; }

        /// <summary>How many single-cell steps were actually taken.</summary>
        public int StepsTaken { get; init; }

        /// <summary>Where the character ended up (its location when no step was possible).</summary>
        public WorldLocation? FinalLocation { get; init; }

        /// <summary>Why movement stopped before the requested distance, if it did.</summary>
        public string? BlockedReason { get; init; }

        public static MoveOutcome Blocked(WorldLocation? at, string reason) => new()
        {
            Success = false,
            StepsTaken = 0,
            FinalLocation = at,
            BlockedReason = reason,
        };
    }
}
