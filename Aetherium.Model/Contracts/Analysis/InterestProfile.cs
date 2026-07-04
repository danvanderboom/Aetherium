using System;
using System.Collections.Generic;
using System.Linq;
using Orleans;

namespace Aetherium.Model.Analysis
{
    /// <summary>
    /// Tracks agent interests based on behavior patterns.
    /// </summary>
    [GenerateSerializer]
    public sealed class InterestProfile
    {
        [Id(0)]
        public string AgentId { get; set; } = string.Empty;
        [Id(1)]
        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
        [Id(2)]
        /// <summary>
        /// Action preferences: action type -> usage count and success rate
        /// </summary>
        public Dictionary<string, ActionPreference> ActionPreferences { get; set; } = new Dictionary<string, ActionPreference>();
        [Id(3)]
        /// <summary>
        /// Entity type interests: entity type -> interaction count
        /// </summary>
        public Dictionary<string, int> EntityInterests { get; set; } = new Dictionary<string, int>();
        [Id(4)]
        /// <summary>
        /// Exploration patterns: areas frequently visited or avoided
        /// </summary>
        public Dictionary<string, AreaPreference> ExplorationPatterns { get; set; } = new Dictionary<string, AreaPreference>();
        [Id(5)]
        /// <summary>
        /// Narrative element interests: quest types, themes, etc.
        /// </summary>
        public Dictionary<string, double> NarrativeInterests { get; set; } = new Dictionary<string, double>();
        [Id(6)]
        /// <summary>
        /// Tools/interactions that engage the agent most
        /// </summary>
        public List<string> EngagingInteractions { get; set; } = new List<string>();
        [Id(7)]
        /// <summary>
        /// Content types that maintain agent attention
        /// </summary>
        public List<string> PreferredContentTypes { get; set; } = new List<string>();
    }

    /// <summary>
    /// Preference data for a specific action type.
    /// </summary>
    [GenerateSerializer]
    public sealed class ActionPreference
    {
        [Id(0)]
        public string ActionType { get; set; } = string.Empty;
        [Id(1)]
        public int UsageCount { get; set; }
        [Id(2)]
        public int SuccessCount { get; set; }
        [Id(3)]
        public double SuccessRate { get; set; }
        [Id(4)]
        public double AverageLatencyMs { get; set; }
        [Id(5)]
        public DateTime LastUsed { get; set; }
        [Id(6)]
        public double PreferenceScore { get; set; } // Higher = more preferred
    }

    /// <summary>
    /// Preference data for a specific area/location pattern.
    /// </summary>
    [GenerateSerializer]
    public sealed class AreaPreference
    {
        [Id(0)]
        public string AreaPattern { get; set; } = string.Empty; // e.g., "center", "edge", "room-type"
        [Id(1)]
        public int VisitCount { get; set; }
        [Id(2)]
        public double AverageTimeSpent { get; set; } // seconds
        [Id(3)]
        public double SuccessRate { get; set; } // success rate in this area
        [Id(4)]
        public bool IsAvoided { get; set; } // true if agent rarely visits
        [Id(5)]
        public double PreferenceScore { get; set; } // Higher = more preferred
    }
}

