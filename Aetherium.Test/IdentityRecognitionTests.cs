using System;
using System.Linq;
using NUnit.Framework;
using Aetherium.Components;
using Aetherium.Core;

namespace Aetherium.Test
{
    /// <summary>
    /// Unit tests for the individual-recognition model (add-identity-recognition): familiarity
    /// recording/reinforcement, kind-dependent acuity, encounter gating, permanence, and caps. Each
    /// test maps to an OpenSpec requirement under changes/add-identity-recognition/specs/identity-recognition.
    /// Timing is controlled via explicit <c>now</c> values, so these are deterministic and do not sleep.
    /// </summary>
    [TestFixture]
    public class IdentityRecognitionTests
    {
        private static readonly DateTime T0 = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        private static RecognitionPolicy Policy(
            double meetStrength = 0.5,
            double ownKind = 0.9,
            double otherKind = 0.4,
            double threshold = 0.25,
            double encounterTimeout = 300,
            double familiarityHalfLife = 86400,
            double growth = 2.0,
            double minInterval = 60,
            double permanenceThreshold = 2592000,
            int maxIndividuals = 1000) =>
            new RecognitionPolicy
            {
                Enabled = true,
                MeetStrength = meetStrength,
                OwnKindAcuity = ownKind,
                OtherKindAcuity = otherKind,
                RecognitionThreshold = threshold,
                EncounterTimeoutSeconds = encounterTimeout,
                FamiliarityHalfLifeSeconds = familiarityHalfLife,
                StabilityGrowthFactor = growth,
                MinReinforcementIntervalSeconds = minInterval,
                PermanenceThresholdSeconds = permanenceThreshold,
                MaxIndividuals = maxIndividuals,
            };

        // Spec: identity-recognition / Individual Recognition Memory — Scenario "First meeting records the individual"
        [Test]
        public void Observe_FirstMeeting_RecordsIndividual()
        {
            var rec = new IndividualRecognition();
            var policy = Policy();

            var obs = rec.Observe("wolf-7", "wolf", T0, policy);

            Assert.That(obs.FirstMeeting, Is.True);
            Assert.That(obs.NewEncounter, Is.True);
            Assert.That(obs.EffectiveFamiliarity, Is.EqualTo(policy.MeetStrength).Within(1e-9));
            var known = rec.Get("wolf-7");
            Assert.That(known, Is.Not.Null);
            Assert.That(known!.Encounters, Is.EqualTo(1));
            Assert.That(known.Strength, Is.EqualTo(0.5).Within(1e-9));
        }

        // Spec: identity-recognition / Individual Recognition Memory — Scenario "Spaced re-meetings reinforce"
        [Test]
        public void Observe_SpacedReMeeting_Reinforces_ContinuousDoesNot()
        {
            var rec = new IndividualRecognition();
            var policy = Policy();

            rec.Observe("wolf-7", "wolf", T0, policy);

            // Spaced re-meeting (> min interval): stability grows base→base×2, strength refreshes.
            rec.Observe("wolf-7", "wolf", T0.AddSeconds(120), policy);
            var known = rec.Get("wolf-7")!;
            Assert.That(known.StabilitySeconds, Is.EqualTo(policy.FamiliarityHalfLifeSeconds * 2).Within(1e-6));
            Assert.That(known.Strength, Is.EqualTo(1.0).Within(1e-9));

            // Continuous contact (< min interval): no further stability growth.
            var stabilityBefore = known.StabilitySeconds;
            rec.Observe("wolf-7", "wolf", T0.AddSeconds(130), policy);
            Assert.That(known.StabilitySeconds, Is.EqualTo(stabilityBefore).Within(1e-9),
                "massed re-contact must not compound familiarity stability");
        }

        // Spec: identity-recognition / Encounter-Gated Recognition Events —
        //       Scenario "No re-fire during continuous contact" + "New encounter after separation"
        [Test]
        public void Observe_EncounterGating_ByTimeout()
        {
            var rec = new IndividualRecognition();
            var policy = Policy(encounterTimeout: 300);

            var first = rec.Observe("bear-1", "bear", T0, policy);
            Assert.That(first.NewEncounter, Is.True, "the first meeting is a new encounter");

            // Still in contact 10s later — same encounter, not fire-eligible.
            var contact = rec.Observe("bear-1", "bear", T0.AddSeconds(10), policy);
            Assert.That(contact.NewEncounter, Is.False);

            // Reappear after the timeout — a new encounter.
            var reappear = rec.Observe("bear-1", "bear", T0.AddSeconds(10 + 400), policy);
            Assert.That(reappear.NewEncounter, Is.True);
            Assert.That(rec.Get("bear-1")!.Encounters, Is.EqualTo(2), "encounters increment only on a new encounter");
        }

        // Spec: identity-recognition / Kind-Dependent Recognition Acuity —
        //       Scenario "Good with own kind" + "Poor with other kinds"
        [Test]
        public void Recognition_OwnKindEasier_ThanOtherKind()
        {
            var policy = Policy();
            // After a meeting, familiarity ≈ meet strength (0.5). Own-kind clears the threshold; other-kind doesn't.
            var familiarity = policy.MeetStrength;

            Assert.That(policy.Recognizes(policy.AcuityFor("wolf", "wolf"), familiarity), Is.True,
                "own-kind: 0.9 × 0.5 = 0.45 ≥ 0.25");
            Assert.That(policy.Recognizes(policy.AcuityFor("wolf", "rabbit"), familiarity), Is.False,
                "other-kind: 0.4 × 0.5 = 0.20 < 0.25");
        }

        // Spec: identity-recognition / Kind-Dependent Recognition Acuity — Scenario "Per-kind override"
        [Test]
        public void RecognitionProfile_PerKindOverride_FlipsOutcome()
        {
            var policy = Policy();
            var profile = new RecognitionProfile();
            profile.PerKindAcuity["rabbit"] = 0.95; // this recognizer is unusually good with rabbits

            Assert.That(profile.AcuityFor("wolf", "rabbit", policy), Is.EqualTo(0.95).Within(1e-9));
            Assert.That(policy.Recognizes(profile.AcuityFor("wolf", "rabbit", policy), policy.MeetStrength), Is.True,
                "the per-kind override lifts a normally-unrecognized other-kind over the threshold");

            // Own/other-kind overrides also apply.
            var forgetful = new RecognitionProfile { OwnKindAcuityOverride = 0.1 };
            Assert.That(forgetful.AcuityFor("wolf", "wolf", policy), Is.EqualTo(0.1).Within(1e-9));
        }

        // Spec: identity-recognition / Individual Recognition Memory —
        //       Scenario "Familiarity fades and can become permanent"
        [Test]
        public void Familiarity_Decays_AndReinforcementLatchesPermanence()
        {
            var rec = new IndividualRecognition();
            // Low permanence threshold so two spaced reinforcements cross it: 3600→7200→14400 ≥ 10000.
            var policy = Policy(familiarityHalfLife: 3600, permanenceThreshold: 10000);

            rec.Observe("guard-1", "character", T0, policy);
            var known = rec.Get("guard-1")!;

            // Decays on the shared curve: at one half-life the effective familiarity halves.
            Assert.That(rec.EffectiveFamiliarity(known, T0.AddSeconds(3600), 3600),
                Is.EqualTo(0.25).Within(1e-9));

            rec.Observe("guard-1", "character", T0.AddSeconds(120), policy);   // 3600→7200
            Assert.That(known.Permanent, Is.False);
            rec.Observe("guard-1", "character", T0.AddSeconds(240), policy);   // 7200→14400 ≥ 10000
            Assert.That(known.Permanent, Is.True, "enough spaced reinforcement makes an individual permanently known");

            // Permanent familiarity never fades.
            Assert.That(rec.EffectiveFamiliarity(known, T0.AddYears(50), 3600),
                Is.EqualTo(known.Strength).Within(1e-9));
        }

        // Spec: identity-recognition / Individual Recognition Memory — Scenario "Individual cap"
        [Test]
        public void Observe_OverCap_PrunesWeakestFirst()
        {
            var rec = new IndividualRecognition();
            var policy = Policy(maxIndividuals: 2);

            // A and B are reinforced (strong); C is a fresh weak acquaintance.
            rec.Observe("A", "character", T0, policy);
            rec.Observe("B", "character", T0, policy);
            rec.Observe("A", "character", T0.AddSeconds(120), policy); // reinforce A → strength 1.0
            rec.Observe("B", "character", T0.AddSeconds(120), policy); // reinforce B → strength 1.0

            rec.Observe("C", "character", T0.AddSeconds(240), policy); // 3rd distinct → over cap

            Assert.That(rec.Count, Is.EqualTo(2));
            Assert.That(rec.Get("C"), Is.Null, "the weakest (freshly-met) individual is pruned first");
            Assert.That(rec.Get("A"), Is.Not.Null);
            Assert.That(rec.Get("B"), Is.Not.Null);
        }
    }
}
