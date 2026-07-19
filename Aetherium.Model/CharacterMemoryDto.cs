using System;
using System.Collections.Generic;

namespace Aetherium.Model
{
    /// <summary>
    /// A character's accumulated memories (operator/debug read; absolute coordinates).
    /// Serialized as JSON by <c>IGameManagementGrain.GetMemoryAsync</c>.
    /// </summary>
    public class CharacterMemoryDto
    {
        public string SessionId { get; set; } = string.Empty;
        public int LocationsTracked { get; set; }
        public int TotalMemories { get; set; }
        public int TotalImpressions { get; set; }
        public List<MemoryEntryDto> Memories { get; set; } = new List<MemoryEntryDto>();
    }

    public class MemoryEntryDto
    {
        public WorldLocationDto Location { get; set; } = new WorldLocationDto();
        public string ContentType { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public double Strength { get; set; }
        /// <summary>Stored strength after lazy decay (see MemoryPolicy.EffectiveStrength).</summary>
        public double EffectiveStrength { get; set; }
        public int Impressions { get; set; }
        public DateTime LastEventTime { get; set; }
        /// <summary>This memory's own decay half-life in seconds (add-memory-dynamics); 0 ⇒ world fallback.</summary>
        public double StabilitySeconds { get; set; }
        /// <summary>True once the memory has become permanent through familiarity — it no longer decays.</summary>
        public bool Permanent { get; set; }
    }
}
