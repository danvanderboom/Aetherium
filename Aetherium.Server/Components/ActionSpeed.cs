using Aetherium.Core;

namespace Aetherium.Components
{
    /// <summary>
    /// Per-actor action-point (AP) budget for the continuous, speed-based action pipeline
    /// (engine gap-analysis §4.1). There is no global turn order: each actor's budget refills
    /// by <see cref="Speed"/> every world tick, capped at <see cref="MaxBudget"/>, and an
    /// <see cref="ActionQueue"/>'d action dispatches once its cost is covered.
    /// </summary>
    public class ActionSpeed : Component
    {
        /// <summary>AP added to <see cref="Budget"/> each world tick.</summary>
        public double Speed { get; set; }

        /// <summary>Current available AP.</summary>
        public double Budget { get; set; }

        /// <summary>Upper bound <see cref="Budget"/> refills toward.</summary>
        public double MaxBudget { get; set; }

        public ActionSpeed() { }

        public ActionSpeed(double speed, double maxBudget, double? budget = null)
        {
            Speed = speed;
            MaxBudget = maxBudget;
            Budget = budget ?? maxBudget;
        }

        /// <summary>Adds <see cref="Speed"/> to <see cref="Budget"/>, capped at <see cref="MaxBudget"/>.</summary>
        public void Refill()
        {
            Budget = System.Math.Min(MaxBudget, Budget + Speed);
        }
    }
}
