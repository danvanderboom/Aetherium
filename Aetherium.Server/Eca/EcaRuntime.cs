using System;
using System.Collections.Generic;
using Aetherium.Model.Eca;

namespace Aetherium.Server.Eca
{
    /// <summary>
    /// The immutable input to a rule evaluation (add-eca-scripting): everything a `creature_died`
    /// event carries. Pure data so evaluation is a pure function — the grain builds this from the kill
    /// site and executes whatever requests come back.
    /// </summary>
    public sealed record EcaEventContext
    {
        public string TriggerKind { get; init; } = string.Empty;

        // creature_died binds
        public string VictimCreatureType { get; init; } = string.Empty;
        public string? VictimEntityId { get; init; }
        public string? KillerEntityId { get; init; }

        // character_recognized binds (add-identity-recognition)
        public string? RecognizerEntityId { get; init; }
        public string RecognizerKind { get; init; } = string.Empty;
        public string? RecognizedEntityId { get; init; }
        public string RecognizedKind { get; init; } = string.Empty;
        public double Familiarity { get; init; }
        public bool FirstMeeting { get; init; }

        // Trigger-agnostic event location (creature_died: the death site; character_recognized:
        // the recognizer's location). spawn_creature places relative to this.
        public int EventX { get; init; }
        public int EventY { get; init; }
        public int EventZ { get; init; }
    }

    /// <summary>
    /// A fully resolved action for the grain to execute — the union of the fields each action kind
    /// needs, with targets and coordinates already resolved from the event so the executor needs no
    /// rule knowledge. The evaluator's output; the grain's input.
    /// </summary>
    public sealed class EcaActionRequest
    {
        public string Kind { get; init; } = string.Empty;

        // spawn_creature
        public string? CreatureId { get; init; }
        public int X { get; init; }
        public int Y { get; init; }
        public int Z { get; init; }

        // deal_damage / apply_status
        public string? TargetEntityId { get; init; }

        // deal_damage
        public string DamageType { get; init; } = "physical";
        public double Amount { get; init; }

        // apply_status
        public string? StatusId { get; init; }
        public int DurationTicks { get; init; }
        public double Magnitude { get; init; }
    }

    /// <summary>
    /// The runtime (compiled) form of a world's <see cref="EcaConfig"/> (add-eca-scripting).
    /// <see cref="Evaluate"/> is a pure function from an event to an ordered list of resolved action
    /// requests — no world access, no I/O — so rule logic is unit-testable in isolation; the grain owns
    /// execution and delta fan-out (the same split the behavior tree uses). Conditions and actions are
    /// keyed on the vocabulary tile-id constants (<c>EcaVocabulary</c>), so the runtime and the metadata
    /// registry can never name a kind differently.
    /// </summary>
    public sealed class EcaRuntime
    {
        private readonly List<EcaRule> _rules;
        private readonly Random _rng;

        /// <summary>Builds a runtime over a world's rules, seeding the `chance` RNG from the world seed
        /// so a given (seed, event order) reproduces the same firings.</summary>
        public EcaRuntime(EcaConfig? config, int seed)
        {
            _rules = config?.Rules ?? new List<EcaRule>();
            _rng = new Random(seed);
        }

        public IReadOnlyList<EcaRule> Rules => _rules;

        /// <summary>Evaluates every rule against the event, returning one resolved request per action of
        /// each rule whose trigger matches and whose conditions all pass. An action whose target can't
        /// be resolved (e.g. a `killer` target with no killer) is skipped.</summary>
        public List<EcaActionRequest> Evaluate(EcaEventContext ctx)
        {
            var requests = new List<EcaActionRequest>();
            foreach (var rule in _rules)
            {
                if (!string.Equals(rule.When, ctx.TriggerKind, StringComparison.Ordinal))
                    continue;
                if (!ConditionsPass(rule.If, ctx))
                    continue;
                foreach (var action in rule.Do)
                {
                    var request = BuildRequest(action, ctx);
                    if (request is not null)
                        requests.Add(request);
                }
            }
            return requests;
        }

        private bool ConditionsPass(List<EcaConditionDescriptor> conditions, EcaEventContext ctx)
        {
            foreach (var condition in conditions)
            {
                switch (condition.Kind)
                {
                    case CreatureTypeIsCondition.Id:
                        if (!string.Equals(condition.CreatureType, ctx.VictimCreatureType, StringComparison.Ordinal))
                            return false;
                        break;
                    case ChanceCondition.Id:
                        if (_rng.NextDouble() >= condition.Probability)
                            return false;
                        break;
                    case RecognizedKindIsCondition.Id:
                        if (!string.Equals(condition.RecognizedKind, ctx.RecognizedKind, StringComparison.OrdinalIgnoreCase))
                            return false;
                        break;
                    case FamiliarityAtLeastCondition.Id:
                        if (ctx.Familiarity < condition.MinFamiliarity)
                            return false;
                        break;
                    case FirstMeetingIsCondition.Id:
                        if (ctx.FirstMeeting != condition.FirstMeeting)
                            return false;
                        break;
                    default:
                        // Unknown condition (should not occur past validation) fails closed.
                        return false;
                }
            }
            return true;
        }

        private static EcaActionRequest? BuildRequest(EcaActionDescriptor action, EcaEventContext ctx)
        {
            switch (action.Kind)
            {
                case SpawnCreatureAction.Id:
                    return new EcaActionRequest
                    {
                        Kind = action.Kind,
                        CreatureId = action.CreatureId,
                        X = ctx.EventX + action.OffsetX,
                        Y = ctx.EventY + action.OffsetY,
                        Z = ctx.EventZ,
                    };

                case DealDamageAction.Id:
                {
                    var target = ResolveTarget(action.Target, ctx);
                    if (target is null)
                        return null;
                    return new EcaActionRequest
                    {
                        Kind = action.Kind,
                        TargetEntityId = target,
                        DamageType = string.IsNullOrEmpty(action.DamageType) ? "physical" : action.DamageType!,
                        Amount = action.Amount,
                    };
                }

                case ApplyStatusAction.Id:
                {
                    var target = ResolveTarget(action.Target, ctx);
                    if (target is null)
                        return null;
                    return new EcaActionRequest
                    {
                        Kind = action.Kind,
                        TargetEntityId = target,
                        StatusId = action.StatusId,
                        DurationTicks = action.DurationTicks,
                        Magnitude = action.Magnitude,
                    };
                }

                default:
                    return null;
            }
        }

        private static string? ResolveTarget(EcaActionTarget target, EcaEventContext ctx) => target switch
        {
            EcaActionTarget.Killer => ctx.KillerEntityId,
            EcaActionTarget.Victim => ctx.VictimEntityId,
            EcaActionTarget.Recognizer => ctx.RecognizerEntityId,
            EcaActionTarget.Recognized => ctx.RecognizedEntityId,
            _ => null,
        };
    }
}
