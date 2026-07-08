using System.Collections.Generic;
using Orleans;

namespace Aetherium.Model.Factions
{
    /// <summary>A declarative rank threshold: standing at or above <see cref="MinStanding"/> grants
    /// <see cref="RankId"/> (monotonic — once granted, a rank is kept even if standing later falls).
    /// Rank *effects* (items, titles, abilities) are a later slice; see docs/factions-reputation.md.</summary>
    [GenerateSerializer]
    public class RankRule
    {
        [Id(0)] public double MinStanding { get; set; }
        [Id(1)] public string RankId { get; set; } = string.Empty;
    }

    /// <summary>
    /// Declarative definition of one faction (engine gap-analysis §4.6). Pure serializable data —
    /// the server's <c>FactionCompiler</c> compiles it into the runtime <c>Faction</c>/<c>FactionDoctrine</c>.
    /// <see cref="DoctrineDeltas"/> maps action tags (e.g. <c>"kill:zombie"</c> — see
    /// docs/factions-reputation.md §3 for the tag-family convention) to standing deltas; tags carry no
    /// engine-assigned moral valence, and a faction with no rule for a tag is unaffected by it.
    /// </summary>
    [GenerateSerializer]
    public class FactionDefinition
    {
        [Id(0)] public string Id { get; set; } = string.Empty;
        [Id(1)] public string Name { get; set; } = string.Empty;
        [Id(2)] public List<string> Tags { get; set; } = new();

        /// <summary>Action tag → standing delta. This faction's values, entirely as data.</summary>
        [Id(3)] public Dictionary<string, double> DoctrineDeltas { get; set; } = new();

        [Id(4)] public List<RankRule> RankRules { get; set; } = new();

        /// <summary>The standing every character starts with toward this faction at join.</summary>
        [Id(5)] public double StartingStanding { get; set; }
    }

    /// <summary>Model mirror of the server's <c>FactionDisposition</c> enum.</summary>
    [GenerateSerializer]
    public enum FactionDispositionKind
    {
        Neutral,
        War,
        Cold,
        Ally,
        Subordinate,
    }

    /// <summary>One directed inter-faction relation. <see cref="Mutual"/> sets the disposition in
    /// both directions (for naturally bilateral relations — war, ally); leave it false for
    /// inherently one-directional dispositions like <see cref="FactionDispositionKind.Subordinate"/>.</summary>
    [GenerateSerializer]
    public class FactionRelationDefinition
    {
        [Id(0)] public string FromFactionId { get; set; } = string.Empty;
        [Id(1)] public string ToFactionId { get; set; } = string.Empty;
        [Id(2)] public FactionDispositionKind Disposition { get; set; }
        [Id(3)] public bool Mutual { get; set; }
    }

    /// <summary>A named standing range (docs/factions-reputation.md §3, layer 3): the public
    /// vocabulary reputation consumers bind to instead of raw numbers. A reputation's band is the
    /// band with the highest <see cref="MinStanding"/> at or below its standing. World-level —
    /// shared by all the world's factions, one vocabulary rather than per-faction dialects.</summary>
    [GenerateSerializer]
    public class StandingBand
    {
        [Id(0)] public string Id { get; set; } = string.Empty;
        [Id(1)] public double MinStanding { get; set; }
    }

    /// <summary>
    /// A world's faction content (engine gap-analysis §4.6): its factions (doctrines, rank rules,
    /// starting standings), inter-faction relations, and standing bands. Threaded through world
    /// creation exactly like <c>DeathPolicy</c>/<c>AbilityConfig</c>/<c>ProgressionConfig</c>; null
    /// anywhere means the world has no factions. The engine ships none — it is campaign data. This
    /// is tier T0 of the faction vision in docs/factions-reputation.md.
    /// </summary>
    [GenerateSerializer]
    public class FactionConfig
    {
        [Id(0)] public List<FactionDefinition> Factions { get; set; } = new();
        [Id(1)] public List<FactionRelationDefinition> Relations { get; set; } = new();
        [Id(2)] public List<StandingBand> Bands { get; set; } = new();
    }
}
