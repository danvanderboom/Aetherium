using Orleans;

namespace Aetherium.Model
{
    /// <summary>
    /// A player's own life-state (engine gap-analysis §4.11, Phase 2 — see
    /// wire-death-respawn-live). Deliberately not part of <c>PerceptionDto</c>: perception is
    /// FOV-filtered world-state (what a player sees), vitals are player-state (what's happening to
    /// them) — pushed to the owning session only, via <c>ReceiveDowned</c>/<c>ReceiveRespawn</c>/
    /// <c>ReceiveDied</c> hub signals.
    /// </summary>
    [GenerateSerializer]
    public class PlayerVitalsDto
    {
        [Id(0)] public int Health { get; set; }
        [Id(1)] public int MaxHealth { get; set; }
        [Id(2)] public bool IsDowned { get; set; }
        [Id(3)] public int DownedTicksRemaining { get; set; }
        [Id(4)] public bool IsInvulnerable { get; set; }
    }
}
