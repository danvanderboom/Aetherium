using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using Aetherium.Core;

namespace Aetherium.Components
{
    /// <summary>
    /// One remembered individual (add-identity-recognition): who, of what kind, when first/last seen,
    /// how many encounters, and a familiarity value on the shared memory-stability curve.
    /// </summary>
    public class KnownIndividual
    {
        public string EntityId { get; set; } = string.Empty;
        public string Kind { get; set; } = string.Empty;
        public DateTime FirstMet { get; set; }
        public DateTime LastSeen { get; set; }
        public int Encounters { get; set; }

        /// <summary>Stored familiarity strength (0..1); refreshed to full on spaced re-meeting.</summary>
        public double Strength { get; set; }

        /// <summary>Familiarity's own decay half-life in seconds; 0 ⇒ policy fallback.</summary>
        public double StabilitySeconds { get; set; }

        /// <summary>Once true, familiarity never fades (a face known forever).</summary>
        public bool Permanent { get; set; }
    }

    /// <summary>
    /// Resolves an entity's recognition "kind" (add-identity-recognition): its
    /// <see cref="CreatureTypeTag"/> value when present (so "wolf"/"bandit" don't collapse to
    /// "monster"), else its CLR type name lowercased ("character" for a player). Shared by the
    /// recognition sweep and the recognition read API so both name kinds identically.
    /// </summary>
    public static class RecognitionKind
    {
        public static string Resolve(Entity entity) =>
            entity.Has<CreatureTypeTag>() && !string.IsNullOrWhiteSpace(entity.Get<CreatureTypeTag>().Value)
                ? entity.Get<CreatureTypeTag>().Value
                : entity.GetType().Name.ToLowerInvariant();
    }

    /// <summary>The result of one proximity observation — what the sweep needs to gate events.</summary>
    public sealed class RecognitionObservation
    {
        /// <summary>True only when the individual was recorded for the very first time.</summary>
        public bool FirstMeeting { get; init; }

        /// <summary>True when this observation begins a new encounter (fire-eligible).</summary>
        public bool NewEncounter { get; init; }

        /// <summary>Effective familiarity at observation time (decayed since last seen).</summary>
        public double EffectiveFamiliarity { get; init; }

        public KnownIndividual Individual { get; init; } = new();
    }

    /// <summary>
    /// A character's individual-recognition memory (add-identity-recognition): the set of other
    /// characters it has encountered, keyed by entity id. Familiarity reinforces on spaced re-meetings
    /// and decays between them using the shared <see cref="MemoryPolicy"/> stability math.
    /// </summary>
    public class IndividualRecognition : Component
    {
        public ConcurrentDictionary<string, KnownIndividual> KnownIndividuals { get; } = new();

        public int Count => KnownIndividuals.Count;

        public KnownIndividual? Get(string entityId) =>
            KnownIndividuals.TryGetValue(entityId, out var k) ? k : null;

        /// <summary>
        /// Records or reinforces an encounter with <paramref name="targetId"/> at <paramref name="now"/>.
        /// First meeting records the individual with the policy's meet-strength; a re-meeting spaced at
        /// least <c>MinReinforcementIntervalSeconds</c> after last-seen grows familiarity stability and
        /// refreshes strength, latching permanence past the threshold. The returned effective
        /// familiarity is measured BEFORE reinforcement (decayed to now), i.e. how well the individual
        /// is recognized at the moment of the encounter. Enforces <c>MaxIndividuals</c> weakest-first.
        /// </summary>
        public RecognitionObservation Observe(string targetId, string targetKind, DateTime now, RecognitionPolicy policy)
        {
            var existing = Get(targetId);
            if (existing == null)
            {
                var indiv = new KnownIndividual
                {
                    EntityId = targetId,
                    Kind = targetKind,
                    FirstMet = now,
                    LastSeen = now,
                    Encounters = 1,
                    Strength = policy.MeetStrength,
                    StabilitySeconds = 0,
                    Permanent = false,
                };
                KnownIndividuals[targetId] = indiv;
                EnforceCap(policy.MaxIndividuals, now, policy.FamiliarityHalfLifeSeconds);

                return new RecognitionObservation
                {
                    FirstMeeting = true,
                    NewEncounter = true,
                    EffectiveFamiliarity = policy.MeetStrength, // age 0
                    Individual = indiv,
                };
            }

            var elapsed = now - existing.LastSeen;
            // Recognition decision uses familiarity decayed to now, before this sighting refreshes it.
            var effective = MemoryPolicy.EffectiveStrength(
                existing.Strength, elapsed, existing.StabilitySeconds, existing.Permanent, policy.FamiliarityHalfLifeSeconds);
            var newEncounter = elapsed.TotalSeconds > policy.EncounterTimeoutSeconds;

            // Spaced re-meeting grows durability; massed contact (continuous) does not.
            if (!existing.Permanent && elapsed.TotalSeconds >= policy.MinReinforcementIntervalSeconds)
            {
                existing.StabilitySeconds = MemoryPolicy.ReinforceStability(
                    existing.StabilitySeconds, policy.FamiliarityHalfLifeSeconds, policy.StabilityGrowthFactor);
                existing.Strength = 1.0;
                if (existing.StabilitySeconds >= policy.PermanenceThresholdSeconds)
                    existing.Permanent = true;
            }

            if (newEncounter)
                existing.Encounters++;
            existing.LastSeen = now;

            return new RecognitionObservation
            {
                FirstMeeting = false,
                NewEncounter = newEncounter,
                EffectiveFamiliarity = effective,
                Individual = existing,
            };
        }

        /// <summary>Effective familiarity of a known individual at <paramref name="now"/> (pure read).</summary>
        public double EffectiveFamiliarity(KnownIndividual k, DateTime now, double fallbackHalfLifeSeconds) =>
            MemoryPolicy.EffectiveStrength(k.Strength, now - k.LastSeen, k.StabilitySeconds, k.Permanent, fallbackHalfLifeSeconds);

        private void EnforceCap(int maxIndividuals, DateTime now, double fallbackHalfLifeSeconds)
        {
            if (maxIndividuals <= 0 || KnownIndividuals.Count <= maxIndividuals)
                return;

            // Prune the weakest by current effective familiarity until within the cap. Permanent
            // individuals sort highest (never decay), so they are pruned last.
            var toRemove = KnownIndividuals.Values
                .OrderBy(k => EffectiveFamiliarity(k, now, fallbackHalfLifeSeconds))
                .Take(KnownIndividuals.Count - maxIndividuals)
                .Select(k => k.EntityId)
                .ToList();

            foreach (var id in toRemove)
                KnownIndividuals.TryRemove(id, out _);
        }
    }
}
