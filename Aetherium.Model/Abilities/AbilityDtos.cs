using System.Collections.Generic;
using Orleans;

namespace Aetherium.Model.Abilities
{
    /// <summary>Result of an <c>IGameMapGrain.UseAbilityAsync</c> call — mirrors <c>AttackResultDto</c>'s
    /// shape so callers handle a cast and a swing uniformly.</summary>
    [GenerateSerializer]
    public class AbilityResultDto
    {
        [Id(0)] public bool Success { get; set; }
        [Id(1)] public string? Reason { get; set; }
        [Id(2)] public string? AbilityId { get; set; }
        [Id(3)] public string? TargetEntityId { get; set; }
        [Id(4)] public bool TargetDefeated { get; set; }
        [Id(5)] public double DamageDealt { get; set; }

        public static AbilityResultDto Fail(string reason) => new() { Success = false, Reason = reason };
    }

    /// <summary>Read-model snapshot of one resource pool's live state, for the vitals/HUD read accessor.</summary>
    [GenerateSerializer]
    public class ResourcePoolDto
    {
        [Id(0)] public string Tag { get; set; } = string.Empty;
        [Id(1)] public double Current { get; set; }
        [Id(2)] public double Max { get; set; }
        [Id(3)] public bool IsInverse { get; set; }
    }

    /// <summary>A player's resource pools, for the read accessor (<c>GetResourcePoolsAsync</c>).</summary>
    [GenerateSerializer]
    public class ResourcePoolsDto
    {
        [Id(0)] public List<ResourcePoolDto> Pools { get; set; } = new();
    }
}
