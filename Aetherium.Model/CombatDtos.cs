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

        public static AttackResultDto Fail(string reason) => new() { Success = false, Reason = reason };
    }
}
