using System;
using System.Collections.Generic;

namespace Aetherium.WorldGen
{
    /// <summary>
    /// Holds narrative-driven constraints that world generation must satisfy
    /// such as required tokens, locations, and thematic preferences.
    /// </summary>
    public sealed class NarrativeGenerationConstraints
    {
        public string NarrativeId { get; set; } = string.Empty;

        /// <summary>
        /// Collection of narrative tokens requested by quests or story beats.
        /// </summary>
        public List<NarrativeTokenRequest> Tokens { get; } = new List<NarrativeTokenRequest>();

        /// <summary>
        /// Required points of interest that must exist in the generated world.
        /// </summary>
        public List<NarrativePointOfInterest> RequiredPoints { get; } = new List<NarrativePointOfInterest>();

        /// <summary>
        /// Optional difficulty pacing curve expressed as expected challenge rating per level depth.
        /// </summary>
        public Dictionary<int, int> DifficultyByDepth { get; } = new Dictionary<int, int>();

        /// <summary>
        /// Topics for lore generation to create coherent historical flavor text.
        /// </summary>
        public List<string> LoreTopics { get; } = new List<string>(); // e.g., ["history", "legend", "journal"]

        /// <summary>
        /// Points of interest for environmental storytelling (ruins, camps, etc.).
        /// </summary>
        public List<NarrativePointOfInterest> StoryPOIs { get; } = new List<NarrativePointOfInterest>();
    }

    public sealed class NarrativeTokenRequest
    {
        public string TokenId { get; set; } = Guid.NewGuid().ToString("N");
        public string TokenType { get; set; } = string.Empty;
        public Dictionary<string, string> Parameters { get; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }

    public sealed class NarrativePointOfInterest
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string Name { get; set; } = string.Empty;
        public string PreferredTerrain { get; set; } = string.Empty;
        public WorldPoiImportance Importance { get; set; } = WorldPoiImportance.Required;
    }

    public enum WorldPoiImportance
    {
        Required,
        Preferred,
        Optional
    }
}



