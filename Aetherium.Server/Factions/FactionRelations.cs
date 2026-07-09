using System.Collections.Generic;

namespace Aetherium.Server.Factions
{
    public enum FactionDisposition
    {
        Neutral,
        War,
        Cold,
        Ally,
        Subordinate,
    }

    /// <summary>
    /// A sparse, directed inter-faction disposition matrix (engine gap-analysis §4.6). Directed —
    /// not symmetric — because "subordinate" is inherently one-directional (A answers to B does not
    /// imply B answers to A); <see cref="SetMutual"/> is a convenience for the dispositions that
    /// usually are bilateral (war, cold, ally, neutral). Unset pairs default to <see cref="FactionDisposition.Neutral"/>.
    /// </summary>
    public class FactionRelations
    {
        private readonly Dictionary<(string From, string To), FactionDisposition> _relations = new();

        public void SetDisposition(string fromFactionId, string toFactionId, FactionDisposition disposition)
            => _relations[(fromFactionId, toFactionId)] = disposition;

        /// <summary>Sets the same disposition in both directions — a convenience for naturally
        /// bilateral relations (war, cold, ally, neutral); do not use for <see cref="FactionDisposition.Subordinate"/>.</summary>
        public void SetMutual(string factionAId, string factionBId, FactionDisposition disposition)
        {
            SetDisposition(factionAId, factionBId, disposition);
            SetDisposition(factionBId, factionAId, disposition);
        }

        public FactionDisposition GetDisposition(string fromFactionId, string toFactionId)
            => _relations.TryGetValue((fromFactionId, toFactionId), out var disposition)
                ? disposition
                : FactionDisposition.Neutral;
    }
}
