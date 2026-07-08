using System.Collections.Generic;
using Orleans;

namespace Aetherium.Model.Progression
{
    /// <summary>Read-model snapshot of one progress pool's live state.</summary>
    [GenerateSerializer]
    public class ProgressPoolDto
    {
        [Id(0)] public string Id { get; set; } = string.Empty;
        [Id(1)] public double Xp { get; set; }
        [Id(2)] public int Level { get; set; }
    }

    /// <summary>A player's live progression, for the <c>GetProgressionAsync</c> read accessor.</summary>
    [GenerateSerializer]
    public class ProgressionStateDto
    {
        [Id(0)] public List<ProgressPoolDto> Pools { get; set; } = new();
        [Id(1)] public Dictionary<string, double> Attributes { get; set; } = new();
        [Id(2)] public List<string> UnlockedSkills { get; set; } = new();
        [Id(3)] public List<string> GrantedAbilities { get; set; } = new();
    }

    /// <summary>Result of an <c>IGameMapGrain.UnlockSkillAsync</c> call.</summary>
    [GenerateSerializer]
    public class UnlockSkillResultDto
    {
        [Id(0)] public bool Success { get; set; }
        [Id(1)] public string? Reason { get; set; }
        /// <summary>The service's outcome enum name (e.g. "Unlocked", "PrerequisitesNotMet", "PoolLevelTooLow").</summary>
        [Id(2)] public string? Result { get; set; }
        [Id(3)] public string? SkillId { get; set; }

        public static UnlockSkillResultDto Fail(string reason, string? result = null)
            => new() { Success = false, Reason = reason, Result = result };
    }
}
