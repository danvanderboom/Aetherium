using System.Collections.Generic;
using Orleans;

namespace Aetherium.Model.Factions
{
    /// <summary>One actor↔faction standing snapshot, for the reputation read accessor.</summary>
    [GenerateSerializer]
    public class ReputationDto
    {
        [Id(0)] public string FactionId { get; set; } = string.Empty;
        [Id(1)] public double Standing { get; set; }

        /// <summary>The current standing band id, or null when the world declares no bands.</summary>
        [Id(2)] public string? Band { get; set; }

        [Id(3)] public List<string> Ranks { get; set; } = new();
    }

    /// <summary>A player's full reputation ledger, for <c>GetReputationAsync</c>.</summary>
    [GenerateSerializer]
    public class ReputationLedgerDto
    {
        [Id(0)] public List<ReputationDto> Reputations { get; set; } = new();
    }

    [GenerateSerializer]
    public class FactionInfoDto
    {
        [Id(0)] public string Id { get; set; } = string.Empty;
        [Id(1)] public string Name { get; set; } = string.Empty;
        [Id(2)] public List<string> Tags { get; set; } = new();
    }

    [GenerateSerializer]
    public class FactionRelationDto
    {
        [Id(0)] public string FromFactionId { get; set; } = string.Empty;
        [Id(1)] public string ToFactionId { get; set; } = string.Empty;
        [Id(2)] public FactionDispositionKind Disposition { get; set; }
    }

    /// <summary>The world's faction landscape (factions, directed relations, bands), for
    /// <c>GetFactionsAsync</c> — tooling/clients read this, gameplay consumers arrive in later tiers.</summary>
    [GenerateSerializer]
    public class FactionsStateDto
    {
        [Id(0)] public List<FactionInfoDto> Factions { get; set; } = new();
        [Id(1)] public List<FactionRelationDto> Relations { get; set; } = new();
        [Id(2)] public List<StandingBand> Bands { get; set; } = new();
    }
}
