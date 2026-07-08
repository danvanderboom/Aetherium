namespace Aetherium.Server.Combat
{
    public enum DropOnDeathPolicy
    {
        None,
        All,
        Partial,
    }

    public enum RespawnPointPolicy
    {
        LastSafeLocation,
        WorldSpawn,
        PartyLeader,
    }

    public enum XpLossPolicy
    {
        None,
        PercentOfLevel,
        FlatAmount,
    }

    /// <summary>
    /// Per-world configuration for what dying means (engine gap-analysis §4.11). Declarative data
    /// only — see <see cref="ResolveDyingTicks"/> for the one behavior this schema exposes today;
    /// everything else (loot, respawn, XP loss) has no consumer yet (see
    /// openspec/changes/add-death-respawn-policy design.md Non-Goals).
    /// </summary>
    public class DeathPolicy
    {
        public bool Permadeath { get; set; }
        public int CorpseRetentionTicks { get; set; }
        public DropOnDeathPolicy DropOnDeath { get; set; }
        public RespawnPointPolicy RespawnPoint { get; set; }
        public XpLossPolicy XpLossPolicy { get; set; }
        public double XpLossAmount { get; set; }
        public bool DownStateEnabled { get; set; }
        public int ReviveWindowTicks { get; set; }

        /// <summary>Reproduces the behavior `deepen-combat-model` shipped before any policy existed:
        /// a down state, a 3-tick revive window, corpses retained forever (no <see cref="CorpseAge"/>
        /// attached — see <see cref="CorpseExpirySystem"/>), no loot/XP-loss/respawn behavior defined.</summary>
        public static DeathPolicy Default => new()
        {
            Permadeath = false,
            CorpseRetentionTicks = int.MaxValue,
            DropOnDeath = DropOnDeathPolicy.Partial,
            RespawnPoint = RespawnPointPolicy.WorldSpawn,
            XpLossPolicy = XpLossPolicy.None,
            XpLossAmount = 0,
            DownStateEnabled = true,
            ReviveWindowTicks = 3,
        };

        /// <summary>The tick countdown a lethal hit should assign to a target's <see cref="Dying"/>
        /// component: <see cref="ReviveWindowTicks"/> if <see cref="DownStateEnabled"/>, else 0 (an
        /// instant transition straight to <see cref="Corpse"/> — no down state at all).</summary>
        public int ResolveDyingTicks() => DownStateEnabled ? ReviveWindowTicks : 0;
    }
}
