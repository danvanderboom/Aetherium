using System;
using System.Collections.Generic;

namespace ConsoleGame.WorldGen
{
    /// <summary>
    /// Describes a single world generation invocation, including target template,
    /// generator identifiers, dimensions, and narrative constraints.
    /// </summary>
    public sealed class WorldGenerationRequest
    {
        public string LayoutGenerator { get; set; } = string.Empty;
        public string? OutdoorGenerator { get; set; }
            = null; // Optional override for outdoor template when layout differs.
        public WorldGenerationTemplate Template { get; set; } = WorldGenerationTemplate.Dungeon;
        public int Width { get; set; } = 80;
        public int Height { get; set; } = 80;
        public int Levels { get; set; } = 1;
        public int? Seed { get; set; }
            = null;
        public string GeneratorVersion { get; set; } = "1.0.0";
        public Dictionary<string, string> Parameters { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public NarrativeGenerationConstraints Narrative { get; set; } = new NarrativeGenerationConstraints();
        public TimeSpan PhaseTimeout { get; set; } = TimeSpan.FromSeconds(2);
        public bool EnableMetrics { get; set; } = true;
        public bool EnableLogging { get; set; } = true;
    }
}


