using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Aetherium.Model.Eca;
using Aetherium.Server.Eca;

namespace Aetherium.Test.Eca
{
    /// <summary>
    /// Verifies "Closed Rule Vocabulary" and "Pure Evaluation"
    /// (openspec/changes/add-eca-scripting/specs/eca-scripting/spec.md): the evaluator is a pure
    /// function from an event to resolved action requests — trigger matching, AND-ed conditions,
    /// deterministic chance, and killer/victim target resolution — with no world access.
    /// </summary>
    [TestFixture]
    public class EcaRuntimeTests
    {
        private static EcaEventContext WolfDeath(string? killer = "killer-1", string? victim = "victim-1") => new()
        {
            TriggerKind = CreatureDiedTrigger.Id,
            VictimCreatureType = "wolf",
            VictimEntityId = victim,
            KillerEntityId = killer,
            VictimX = 5,
            VictimY = 7,
            VictimZ = 0,
        };

        private static EcaConfig Config(params EcaRule[] rules) => new() { Rules = rules.ToList() };

        private static EcaRule SpawnRule(string creatureId, params EcaConditionDescriptor[] conditions) => new()
        {
            Id = "r",
            When = CreatureDiedTrigger.Id,
            If = conditions.ToList(),
            Do = new List<EcaActionDescriptor>
            {
                new() { Kind = SpawnCreatureAction.Id, CreatureId = creatureId },
            },
        };

        [Test]
        public void Evaluate_NoConditions_FiresOnTrigger()
        {
            var runtime = new EcaRuntime(Config(SpawnRule("wolf")), seed: 1);

            var requests = runtime.Evaluate(WolfDeath());

            var request = requests.Single();
            Assert.That(request.Kind, Is.EqualTo(SpawnCreatureAction.Id));
            Assert.That(request.CreatureId, Is.EqualTo("wolf"));
            Assert.That((request.X, request.Y, request.Z), Is.EqualTo((5, 7, 0)), "Spawns at the death location.");
        }

        [Test]
        public void Evaluate_TriggerMismatch_EmitsNothing()
        {
            var runtime = new EcaRuntime(Config(SpawnRule("wolf")), seed: 1);

            var requests = runtime.Evaluate(WolfDeath() with { TriggerKind = "some_other_event" });

            Assert.That(requests, Is.Empty);
        }

        [Test]
        public void CreatureTypeIs_GatesByVictimType()
        {
            var rule = SpawnRule("wolf", new EcaConditionDescriptor
            {
                Kind = CreatureTypeIsCondition.Id,
                CreatureType = "cult_acolyte",
            });
            var runtime = new EcaRuntime(Config(rule), seed: 1);

            Assert.That(runtime.Evaluate(WolfDeath()), Is.Empty, "A wolf death must not satisfy creature_type_is: cult_acolyte.");
            Assert.That(runtime.Evaluate(WolfDeath() with { VictimCreatureType = "cult_acolyte" }), Has.Count.EqualTo(1));
        }

        [Test]
        public void Chance_Zero_EmitsNothing_Chance_One_EmitsAction()
        {
            EcaRuntime WithChance(double p) => new(Config(SpawnRule("wolf", new EcaConditionDescriptor
            {
                Kind = ChanceCondition.Id,
                Probability = p,
            })), seed: 12345);

            Assert.That(WithChance(0.0).Evaluate(WolfDeath()), Is.Empty, "probability 0 never fires.");
            Assert.That(WithChance(1.0).Evaluate(WolfDeath()), Has.Count.EqualTo(1), "probability 1 always fires.");
        }

        [Test]
        public void Evaluate_ResolvesKillerAndVictimTargets()
        {
            var rule = new EcaRule
            {
                Id = "r",
                When = CreatureDiedTrigger.Id,
                Do = new List<EcaActionDescriptor>
                {
                    new() { Kind = DealDamageAction.Id, Target = EcaActionTarget.Killer, Amount = 10 },
                    new() { Kind = ApplyStatusAction.Id, Target = EcaActionTarget.Victim, StatusId = "slowed", DurationTicks = 5 },
                },
            };
            var runtime = new EcaRuntime(Config(rule), seed: 1);

            var requests = runtime.Evaluate(WolfDeath(killer: "K", victim: "V"));

            var damage = requests.Single(r => r.Kind == DealDamageAction.Id);
            Assert.That(damage.TargetEntityId, Is.EqualTo("K"));
            Assert.That(damage.DamageType, Is.EqualTo("physical"), "Unspecified damage type defaults to physical.");
            var status = requests.Single(r => r.Kind == ApplyStatusAction.Id);
            Assert.That(status.TargetEntityId, Is.EqualTo("V"));
            Assert.That(status.StatusId, Is.EqualTo("slowed"));
        }

        [Test]
        public void Evaluate_SkipsAction_WhenTargetUnresolved()
        {
            var rule = new EcaRule
            {
                Id = "r",
                When = CreatureDiedTrigger.Id,
                Do = new List<EcaActionDescriptor>
                {
                    new() { Kind = DealDamageAction.Id, Target = EcaActionTarget.Killer, Amount = 10 },
                },
            };
            var runtime = new EcaRuntime(Config(rule), seed: 1);

            // No killer on the event (e.g. an environmental death) → the killer-targeted action is skipped.
            Assert.That(runtime.Evaluate(WolfDeath(killer: null)), Is.Empty);
        }
    }
}
