using Aetherium.Core;

namespace Aetherium.Server.Combat
{
    /// <summary>
    /// A player's down-state (engine gap-analysis §4.11, Phase 2 — see
    /// wire-death-respawn-live). Distinct from the monster <see cref="Dying"/>/<see cref="Corpse"/>
    /// lifecycle on purpose: a downed player is recovered (respawn or, under a permadeath policy,
    /// becomes a <see cref="Corpse"/>) rather than following the creature-death path. While this
    /// component is present, every player command is rejected (see
    /// <c>GameMapGrain.IsActionable</c>).
    /// </summary>
    public class Downed : Component
    {
        public int TicksRemaining { get; set; }

        public Downed() { }
        public Downed(int ticksRemaining) { TicksRemaining = ticksRemaining; }
    }

    /// <summary>
    /// Brief post-respawn protection (engine gap-analysis §4.11, <see cref="DeathPolicy.RespawnInvulnerabilityTicks"/>)
    /// so a respawn can't be instantly re-downed by whatever killed the player. Attackers targeting
    /// an entity carrying this component are rejected the same way a <see cref="Dying"/>/<see
    /// cref="Corpse"/> target is.
    /// </summary>
    public class RespawnInvulnerable : Component
    {
        public int TicksRemaining { get; set; }

        public RespawnInvulnerable() { }
        public RespawnInvulnerable(int ticksRemaining) { TicksRemaining = ticksRemaining; }
    }
}
