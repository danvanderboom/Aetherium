using Aetherium.Core;

namespace Aetherium.Server.Combat
{
    /// <summary>
    /// Opt-in tick-age tracker for a <see cref="Corpse"/> entity (engine gap-analysis §4.11). A
    /// <see cref="Corpse"/> with no <see cref="CorpseAge"/> attached persists forever — the
    /// behavior `deepen-combat-model` shipped before any expiry policy existed. Attaching this
    /// component (a Phase 2 concern — see openspec/changes/add-death-respawn-policy) opts an
    /// individual corpse into <see cref="CorpseExpirySystem"/>'s removal once a
    /// <see cref="DeathPolicy.CorpseRetentionTicks"/> threshold elapses.
    /// </summary>
    public class CorpseAge : Component
    {
        public int Ticks { get; set; }

        public CorpseAge() { }
        public CorpseAge(int ticks) { Ticks = ticks; }
    }
}
