using System.Collections.Generic;
using System.Linq;
using Aetherium.Model.Factions;

namespace Aetherium.Server.Factions
{
    /// <summary>Grants ranks from declarative thresholds (engine gap-analysis §4.6 — see
    /// wire-factions-live). Monotonic: a rule whose <see cref="RankRule.MinStanding"/> is at or
    /// below the current standing grants its rank if absent; nothing is ever revoked when standing
    /// later falls (revocation semantics can become a config flag if a game wants them).</summary>
    public static class RankEvaluator
    {
        public static void Apply(Reputation reputation, IEnumerable<RankRule>? rules)
        {
            if (rules is null)
                return;

            foreach (var rule in rules)
            {
                if (reputation.Standing >= rule.MinStanding && !reputation.Ranks.Contains(rule.RankId))
                    reputation.Ranks.Add(rule.RankId);
            }
        }
    }

    /// <summary>Resolves a standing value to its named band (docs/factions-reputation.md §3,
    /// layer 3): the band with the highest <see cref="StandingBand.MinStanding"/> at or below the
    /// standing. Null when the world declares no bands, or when the standing sits below every band's
    /// threshold.</summary>
    public static class BandResolver
    {
        public static string? Resolve(double standing, IReadOnlyList<StandingBand>? bands)
        {
            if (bands is null || bands.Count == 0)
                return null;

            StandingBand? best = null;
            foreach (var band in bands)
            {
                if (band.MinStanding <= standing && (best is null || band.MinStanding > best.MinStanding))
                    best = band;
            }

            return best?.Id;
        }
    }
}
