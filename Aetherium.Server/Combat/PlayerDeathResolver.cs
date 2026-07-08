using Aetherium.Model.Combat;

namespace Aetherium.Server.Combat
{
    /// <summary>What a lethal hit against a player does, per the active <see cref="DeathPolicy"/>
    /// (engine gap-analysis §4.11, Phase 2 — see wire-death-respawn-live). All four death models
    /// (instant respawn, auto-respawn after a down timer, instant permadeath, down-then-permadeath)
    /// reduce to <c>Permadeath × DownStateEnabled</c> — see the design doc's proposal table.</summary>
    public enum PlayerDeathOutcome
    {
        /// <summary>No down state: the player respawns immediately.</summary>
        InstantRespawn,
        /// <summary>A down state applies; what happens when it elapses depends on
        /// <see cref="DeathPolicy.Permadeath"/> (respawn, or become a Corpse) — see
        /// <see cref="ResolveDownedOutcome"/>.</summary>
        EnterDowned,
        /// <summary>No down state and permadeath: the player becomes a Corpse immediately.</summary>
        InstantPermadeath,
    }

    /// <summary>What a <see cref="Downed"/> player's expired countdown does.</summary>
    public enum DownedExpiryOutcome
    {
        Respawn,
        Permadeath,
    }

    /// <summary>Pure mapping from a <see cref="DeathPolicy"/> to what a lethal hit — and later, a
    /// Downed countdown's expiry — does. Deliberately has no world/entity dependency so it's
    /// unit-testable in isolation, mirroring <see cref="DeathPolicy.ResolveDyingTicks"/>.</summary>
    public static class PlayerDeathResolver
    {
        public static PlayerDeathOutcome ResolveLethalHitOutcome(DeathPolicy policy)
        {
            if (!policy.DownStateEnabled)
                return policy.Permadeath ? PlayerDeathOutcome.InstantPermadeath : PlayerDeathOutcome.InstantRespawn;

            return PlayerDeathOutcome.EnterDowned;
        }

        public static DownedExpiryOutcome ResolveDownedOutcome(DeathPolicy policy)
            => policy.Permadeath ? DownedExpiryOutcome.Permadeath : DownedExpiryOutcome.Respawn;
    }
}
