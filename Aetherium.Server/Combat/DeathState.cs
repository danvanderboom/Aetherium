using Aetherium.Core;

namespace Aetherium.Server.Combat
{
    /// <summary>
    /// A lethally-hit entity's transitional state (engine gap-analysis §4.2): death is a state
    /// transition, not a delete. While <see cref="Dying"/> is present the entity stays in the
    /// world — future interaction affordances (loot, revive, harvest) attach here — until
    /// <see cref="DeathSystem"/> converts it to <see cref="Corpse"/> after <see cref="TicksRemaining"/>
    /// elapses. Permadeath vs. respawn policy is left to a per-world config (engine gap-analysis
    /// §4.11), not decided by this component.
    /// </summary>
    public class Dying : Component
    {
        public int TicksRemaining { get; set; }

        public Dying() { }
        public Dying(int ticksRemaining) { TicksRemaining = ticksRemaining; }
    }

    /// <summary>Terminal marker an entity carries once its <see cref="Dying"/> window elapses.</summary>
    public class Corpse : Component { }
}
