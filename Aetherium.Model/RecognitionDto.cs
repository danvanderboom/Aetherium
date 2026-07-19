using System;
using System.Collections.Generic;

namespace Aetherium.Model
{
    /// <summary>
    /// A character's individual-recognition memory (add-identity-recognition; operator/debug read).
    /// Serialized as JSON by <c>IGameManagementGrain.GetRecognitionAsync</c>. Works for player
    /// characters and NPCs alike, read from the canonical world.
    /// </summary>
    public class RecognitionDto
    {
        public string WorldId { get; set; } = string.Empty;
        public string EntityId { get; set; } = string.Empty;
        public string Kind { get; set; } = string.Empty;
        public int KnownCount { get; set; }
        public List<KnownIndividualDto> Individuals { get; set; } = new List<KnownIndividualDto>();
    }

    public class KnownIndividualDto
    {
        public string EntityId { get; set; } = string.Empty;
        public string Kind { get; set; } = string.Empty;
        public DateTime FirstMet { get; set; }
        public DateTime LastSeen { get; set; }
        public int Encounters { get; set; }
        public double Strength { get; set; }
        /// <summary>Familiarity after decay to read time (see MemoryPolicy.EffectiveStrength).</summary>
        public double EffectiveFamiliarity { get; set; }
        public double StabilitySeconds { get; set; }
        public bool Permanent { get; set; }
    }
}
