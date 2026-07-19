using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Aetherium.Model.Eca;
using Aetherium.Model.Games;
using Aetherium.Server.Eca;
using Aetherium.Server.Games;

namespace Aetherium.Test.Eca
{
    /// <summary>
    /// Verifies the ECA recognition surface (add-identity-recognition): the character_recognized
    /// trigger, its conditions (recognized_kind_is / familiarity_at_least / first_meeting_is), and the
    /// Recognizer/Recognized action targets — evaluated by the pure runtime and accepted by the
    /// vocabulary-driven validator. Each test maps to a requirement under
    /// changes/add-identity-recognition/specs/eca-scripting.
    /// </summary>
    [TestFixture]
    public class RecognitionEcaTests
    {
        private static EcaEventContext Recognition(
            string recognizer = "rec-1", string recognized = "tgt-1",
            string recognizedKind = "wolf", double familiarity = 0.9, bool firstMeeting = false) => new()
        {
            TriggerKind = CharacterRecognizedTrigger.Id,
            RecognizerEntityId = recognizer,
            RecognizerKind = "character",
            RecognizedEntityId = recognized,
            RecognizedKind = recognizedKind,
            Familiarity = familiarity,
            FirstMeeting = firstMeeting,
            EventX = 3, EventY = 4, EventZ = 0,
        };

        private static EcaConfig Config(params EcaRule[] rules) => new() { Rules = rules.ToList() };

        private static EcaRule DamageRecognizedRule(EcaActionTarget target = EcaActionTarget.Recognized,
            params EcaConditionDescriptor[] conditions) => new()
        {
            Id = "r",
            When = CharacterRecognizedTrigger.Id,
            If = conditions.ToList(),
            Do = new List<EcaActionDescriptor>
            {
                new() { Kind = DealDamageAction.Id, Target = target, Amount = 5 },
            },
        };

        // Spec: eca-scripting / Character Recognized Trigger — Scenario "Rule fires on recognition"
        //       + Recognition Action Targets — Scenario "Act on the recognized character"
        [Test]
        public void Recognized_Target_Resolves_ToRecognizedEntity()
        {
            var runtime = new EcaRuntime(Config(DamageRecognizedRule()), seed: 1);

            var request = runtime.Evaluate(Recognition()).Single();

            Assert.That(request.Kind, Is.EqualTo(DealDamageAction.Id));
            Assert.That(request.TargetEntityId, Is.EqualTo("tgt-1"));
        }

        // Spec: eca-scripting / Recognition Action Targets — Scenario "Act on the recognizer"
        [Test]
        public void Recognizer_Target_Resolves_ToRecognizerEntity()
        {
            var runtime = new EcaRuntime(Config(DamageRecognizedRule(EcaActionTarget.Recognizer)), seed: 1);
            var request = runtime.Evaluate(Recognition()).Single();
            Assert.That(request.TargetEntityId, Is.EqualTo("rec-1"));
        }

        // Spec: eca-scripting / Recognition Action Targets — Scenario "Mismatched target skips"
        [Test]
        public void MismatchedTarget_OnRecognitionEvent_Skips()
        {
            // A Killer target has nothing to resolve on a recognition event → the action is skipped.
            var runtime = new EcaRuntime(Config(DamageRecognizedRule(EcaActionTarget.Killer)), seed: 1);
            Assert.That(runtime.Evaluate(Recognition()), Is.Empty);
        }

        // Spec: eca-scripting / Recognition Conditions — Scenario "Kind filter"
        [Test]
        public void RecognizedKindIs_GatesByKind()
        {
            var rule = DamageRecognizedRule(EcaActionTarget.Recognized,
                new EcaConditionDescriptor { Kind = RecognizedKindIsCondition.Id, RecognizedKind = "wolf" });
            var runtime = new EcaRuntime(Config(rule), seed: 1);

            Assert.That(runtime.Evaluate(Recognition(recognizedKind: "wolf")), Has.Count.EqualTo(1));
            Assert.That(runtime.Evaluate(Recognition(recognizedKind: "rabbit")), Is.Empty);
        }

        // Spec: eca-scripting / Recognition Conditions — Scenario "Familiarity gate"
        [Test]
        public void FamiliarityAtLeast_GatesByFamiliarity()
        {
            var rule = DamageRecognizedRule(EcaActionTarget.Recognized,
                new EcaConditionDescriptor { Kind = FamiliarityAtLeastCondition.Id, MinFamiliarity = 0.5 });
            var runtime = new EcaRuntime(Config(rule), seed: 1);

            Assert.That(runtime.Evaluate(Recognition(familiarity: 0.9)), Has.Count.EqualTo(1));
            Assert.That(runtime.Evaluate(Recognition(familiarity: 0.3)), Is.Empty);
        }

        // Spec: eca-scripting / Recognition Conditions — Scenario "Stranger vs known"
        [Test]
        public void FirstMeetingIs_DistinguishesStrangerFromKnown()
        {
            var strangerRule = DamageRecognizedRule(EcaActionTarget.Recognized,
                new EcaConditionDescriptor { Kind = FirstMeetingIsCondition.Id, FirstMeeting = true });
            var runtime = new EcaRuntime(Config(strangerRule), seed: 1);

            Assert.That(runtime.Evaluate(Recognition(firstMeeting: true)), Has.Count.EqualTo(1));
            Assert.That(runtime.Evaluate(Recognition(firstMeeting: false)), Is.Empty);
        }

        // Spec: eca-scripting / Character Recognized Trigger — Scenario "Vocabulary discovery"
        [Test]
        public void Vocabulary_DiscoversRecognitionTiles()
        {
            Assert.That(EcaVocabulary.Contains(CharacterRecognizedTrigger.Id), Is.True);
            Assert.That(EcaVocabulary.Contains(RecognizedKindIsCondition.Id), Is.True);
            Assert.That(EcaVocabulary.Contains(FamiliarityAtLeastCondition.Id), Is.True);
            Assert.That(EcaVocabulary.Contains(FirstMeetingIsCondition.Id), Is.True);
        }

        // Spec: eca-scripting / Character Recognized Trigger — Scenario "Vocabulary discovery"
        //       (the validator accepts a rule using the new trigger, conditions, and targets)
        [Test]
        public void Validator_AcceptsRecognitionRule()
        {
            var definition = new GameDefinition
            {
                Id = "recognition-game",
                Name = "Recognition Game",
                Version = "1.0.0",
                World = new GameWorldDefinition { GeneratorType = "maze" },
                Rules = new EcaConfig
                {
                    Rules =
                    {
                        new EcaRule
                        {
                            Id = "greet_kin",
                            When = CharacterRecognizedTrigger.Id,
                            If =
                            {
                                new EcaConditionDescriptor { Kind = RecognizedKindIsCondition.Id, RecognizedKind = "character" },
                                new EcaConditionDescriptor { Kind = FamiliarityAtLeastCondition.Id, MinFamiliarity = 0.5 },
                                new EcaConditionDescriptor { Kind = FirstMeetingIsCondition.Id, FirstMeeting = false },
                            },
                            Do =
                            {
                                new EcaActionDescriptor { Kind = ApplyStatusAction.Id, Target = EcaActionTarget.Recognized, StatusId = "slowed", DurationTicks = 3 },
                            },
                        },
                    },
                },
            };

            var diagnostics = new GameDefinitionValidator().Validate(definition, "recognition-bundle");
            var errors = diagnostics.Where(d => d.Severity == GameDefinitionDiagnosticSeverity.Error).ToList();
            Assert.That(errors, Is.Empty, string.Join("; ", errors.Select(e => e.Message)));
        }
    }
}
