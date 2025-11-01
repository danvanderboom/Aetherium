using System;
using System.Collections.Generic;
using Orleans;

namespace Aetherium.Server.MetaProgression
{
    /// <summary>
    /// Meta-progression state tracking discoveries, unlocks, and criteria.
    /// </summary>
    [GenerateSerializer]
    public class MetaProgressionState
    {
        [Id(0)] public string PlayerId { get; set; } = string.Empty;
        [Id(1)] public HashSet<string> DiscoveredWorldTemplates { get; set; } = new HashSet<string>();
        [Id(2)] public HashSet<string> DiscoveredTags { get; set; } = new HashSet<string>();
        [Id(3)] public HashSet<string> VisitedWorldIds { get; set; } = new HashSet<string>();
        [Id(4)] public HashSet<string> VisitedMapIds { get; set; } = new HashSet<string>();
        [Id(5)] public HashSet<string> CompletedQuestIds { get; set; } = new HashSet<string>();
        [Id(6)] public HashSet<string> CompletedCrossWorldQuestIds { get; set; } = new HashSet<string>();
        [Id(7)] public HashSet<string> UnlockedGenerators { get; set; } = new HashSet<string>();
        [Id(8)] public Dictionary<string, UnlockCriteria> UnlockCriteriaDefinitions { get; set; } = new Dictionary<string, UnlockCriteria>();
        [Id(9)] public Dictionary<string, int> TagVisitCounts { get; set; } = new Dictionary<string, int>(); // Tag -> visit count
        [Id(10)] public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        [Id(11)] public DateTime LastUpdatedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Criteria that must be met to unlock a generator template.
    /// </summary>
    [GenerateSerializer]
    public class UnlockCriteria
    {
        [Id(0)] public string CriteriaId { get; set; } = string.Empty;
        [Id(1)] public string UnlocksGenerator { get; set; } = string.Empty; // Generator template name
        [Id(2)] public int? MinWorldVisits { get; set; } // Minimum total world visits
        [Id(3)] public int? MinWorldsOfTag { get; set; } // Minimum worlds visited with a specific tag
        [Id(4)] public string? RequiredTag { get; set; } // Tag that must be discovered
        [Id(5)] public int? MinCrossWorldQuests { get; set; } // Minimum cross-world quests completed
        [Id(6)] public List<string>? RequiredQuestIds { get; set; } // Specific quests that must be completed
        [Id(7)] public List<string>? RequiredWorldTemplates { get; set; } // World templates that must be discovered
        [Id(8)] public Dictionary<string, int>? TagVisitRequirements { get; set; } // Tag -> minimum visit count
    }
}

