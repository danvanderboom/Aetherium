using Orleans;

namespace Aetherium.Model
{
    /// <summary>
    /// Result of an attack, carried across the mutation gateway and (for grain-bound sessions) the
    /// grain boundary. Standalone so it can cross SignalR.
    /// </summary>
    [GenerateSerializer]
    public class AttackResultDto
    {
        [Id(0)] public bool Success { get; set; }
        [Id(1)] public string? Reason { get; set; }
        [Id(2)] public int Damage { get; set; }
        [Id(3)] public int RemainingHealth { get; set; }
        [Id(4)] public bool TargetDefeated { get; set; }
        [Id(5)] public string TargetType { get; set; } = string.Empty;
        [Id(6)] public string TargetEntityId { get; set; } = string.Empty;

        /// <summary>Entity id of loot dropped where the target fell, or null if nothing dropped (P3-7 slice 2).</summary>
        [Id(7)] public string? DroppedLootEntityId { get; set; }

        /// <summary>Type name of the dropped loot (e.g. "SwordItem"), or null when nothing dropped.</summary>
        [Id(8)] public string? DroppedLootType { get; set; }

        public static AttackResultDto Fail(string reason) => new() { Success = false, Reason = reason };
    }

    /// <summary>
    /// Rolling combat analytics for a single map (P3-7 slice 2): how many monsters have fallen
    /// on it and the total damage players have dealt. Persisted with the map so it survives grain
    /// reactivation.
    /// </summary>
    [GenerateSerializer]
    public class CombatStatsDto
    {
        [Id(0)] public int MonstersDefeated { get; set; }
        [Id(1)] public long TotalDamageDealt { get; set; }
    }
}
