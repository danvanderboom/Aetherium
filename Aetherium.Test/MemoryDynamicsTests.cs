using System;
using NUnit.Framework;
using Aetherium.Components;
using Aetherium.Core;

namespace Aetherium.Test
{
    /// <summary>
    /// Unit tests for the memory-dynamics model (add-memory-dynamics): stability, spaced
    /// reinforcement, permanence, forgetting, and per-character profiles. Each test maps to an
    /// OpenSpec requirement under changes/add-memory-dynamics/specs/character-memory.
    /// Timing is controlled by passing an explicit <c>now</c> to the AddMemory overload, so these
    /// tests are deterministic and do not sleep.
    /// </summary>
    [TestFixture]
    public class MemoryDynamicsTests
    {
        private static readonly DateTime T0 = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        private static MemoryDynamics Dynamics(
            double baseHalfLife = 3600,
            double growth = 2.0,
            double minInterval = 60,
            double permanenceThreshold = 2592000,
            double forgetThreshold = 0.05,
            int maxLocations = 10000) =>
            new MemoryDynamics
            {
                Enabled = true,
                BaseHalfLifeSeconds = baseHalfLife,
                StabilityGrowthFactor = growth,
                MinReinforcementIntervalSeconds = minInterval,
                PermanenceThresholdSeconds = permanenceThreshold,
                ForgetThreshold = forgetThreshold,
                MaxLocations = maxLocations,
            };

        private static SpaceTimeMemory Record(Memory memory, WorldLocation loc, MemoryDynamics dyn, DateTime now)
        {
            memory.AddMemory(new SpaceTimeMemory
            {
                Location = loc,
                ContentType = "terrain",
                Content = "Cave",
                Strength = 1.0,
                LastEventTime = now
            }, dyn, now);
            return memory.Knowledge(loc)[0];
        }

        // Spec: character-memory / Memory Stability and Reinforcement
        //       Scenario "Spaced re-encounter grows stability" + "Massed re-encounter does not grow stability"
        [Test]
        public void SpacedReencounter_GrowsStability_MassedDoesNot()
        {
            var memory = new Memory();
            var loc = new WorldLocation(0, 0, 0);
            var dyn = Dynamics();

            // First sighting: brand-new memory, no stability yet.
            var m = Record(memory, loc, dyn, T0);
            Assert.That(m.StabilitySeconds, Is.EqualTo(0), "a first sighting has no reinforced stability");
            Assert.That(m.Impressions, Is.EqualTo(1));

            // Spaced re-encounter (2 min later ≥ 60s interval): stability grows base→base×2, strength refreshes.
            m.Strength = 0.3; // simulate prior decay to prove the refresh
            Record(memory, loc, dyn, T0.AddSeconds(120));
            Assert.That(m.StabilitySeconds, Is.EqualTo(7200).Within(1e-9), "spaced re-encounter multiplies stability");
            Assert.That(m.Strength, Is.EqualTo(1.0).Within(1e-9), "reinforcement refreshes strength to full");
            Assert.That(m.Impressions, Is.EqualTo(2));

            // Another spaced re-encounter: 7200→14400.
            Record(memory, loc, dyn, T0.AddSeconds(240));
            Assert.That(m.StabilitySeconds, Is.EqualTo(14400).Within(1e-9));
            Assert.That(m.Impressions, Is.EqualTo(3));

            // Massed re-encounter (10s later < 60s interval): impressions bump, stability unchanged.
            Record(memory, loc, dyn, T0.AddSeconds(250));
            Assert.That(m.StabilitySeconds, Is.EqualTo(14400).Within(1e-9), "massed re-exposure must not compound stability");
            Assert.That(m.Impressions, Is.EqualTo(4), "massed re-encounter still bumps impressions");
        }

        // Spec: character-memory / Memory Stability and Reinforcement
        //       Scenario "Stability fallback for legacy entries"
        [Test]
        public void EffectiveStrength_UsesPerMemoryStability_FallsBackWhenZero()
        {
            // Per-memory stability drives decay: age == stability ⇒ half strength.
            Assert.That(
                MemoryPolicy.EffectiveStrength(1.0, TimeSpan.FromSeconds(7200), stabilitySeconds: 7200, permanent: false, fallbackHalfLifeSeconds: 3600),
                Is.EqualTo(0.5).Within(1e-9));

            // Stability 0 ⇒ fall back to the provided (profile-scaled) half-life.
            Assert.That(
                MemoryPolicy.EffectiveStrength(1.0, TimeSpan.FromSeconds(3600), stabilitySeconds: 0, permanent: false, fallbackHalfLifeSeconds: 3600),
                Is.EqualTo(0.5).Within(1e-9));

            // The five-arg overload with stability 0 matches the legacy three-arg overload exactly.
            var age = TimeSpan.FromSeconds(5000);
            Assert.That(
                MemoryPolicy.EffectiveStrength(0.9, age, 0, false, 3600),
                Is.EqualTo(MemoryPolicy.EffectiveStrength(0.9, age, 3600)).Within(1e-12));
        }

        // Spec: character-memory / Memory Permanence Through Familiarity
        //       Scenario "Permanence latch" + "Permanent memories do not decay or cull"
        [Test]
        public void Reinforcement_LatchesPermanence_ThenNeverDecays()
        {
            var memory = new Memory();
            var loc = new WorldLocation(1, 2, 0);
            // Low threshold so two reinforcements cross it: 3600→7200→14400 ≥ 10000.
            var dyn = Dynamics(permanenceThreshold: 10000);

            var m = Record(memory, loc, dyn, T0);
            Record(memory, loc, dyn, T0.AddSeconds(120));   // 3600→7200, not yet permanent
            Assert.That(m.Permanent, Is.False);
            Record(memory, loc, dyn, T0.AddSeconds(240));   // 7200→14400 ≥ 10000 ⇒ permanent
            Assert.That(m.Permanent, Is.True, "stability past the threshold latches permanence");

            // A permanent memory returns full stored strength at any age.
            Assert.That(
                MemoryPolicy.EffectiveStrength(m.Strength, TimeSpan.FromDays(3650), m.StabilitySeconds, m.Permanent, 3600),
                Is.EqualTo(m.Strength).Within(1e-9));

            // Permanent memories are never reinforced further (guarded by !Permanent).
            var stabilityBefore = m.StabilitySeconds;
            Record(memory, loc, dyn, T0.AddSeconds(100000));
            Assert.That(m.StabilitySeconds, Is.EqualTo(stabilityBefore), "permanent memories stop growing");
        }

        // Spec: character-memory / Forgetting Weak Memories
        //       Scenario "Write-time cull at touched locations" + "Culling disabled"
        [Test]
        public void CullForgotten_RemovesSubThreshold_RespectsDisableAndPermanence()
        {
            var memory = new Memory();
            var loc = new WorldLocation(5, 5, 0);

            // A stability-0 memory decayed ~10 half-lives (eff ≈ 0.001 < 0.05) is culled.
            memory.AddMemory(new SpaceTimeMemory { Location = loc, ContentType = "terrain", Content = "Cave", Strength = 1.0, LastEventTime = T0 }, default, T0);
            var removed = memory.CullForgotten(loc, forgetThreshold: 0.05, fallbackHalfLifeSeconds: 3600, now: T0.AddSeconds(36000));
            Assert.That(removed, Is.EqualTo(1));
            Assert.That(memory.Knows(loc), Is.False, "a fully-decayed memory is forgotten");

            // ForgetThreshold 0 disables culling entirely.
            memory.AddMemory(new SpaceTimeMemory { Location = loc, ContentType = "terrain", Content = "Cave", Strength = 1.0, LastEventTime = T0 }, default, T0);
            Assert.That(memory.CullForgotten(loc, forgetThreshold: 0, fallbackHalfLifeSeconds: 3600, now: T0.AddSeconds(36000)), Is.EqualTo(0));
            Assert.That(memory.Knows(loc), Is.True);

            // A permanent memory is never culled, however old.
            memory.Knowledge(loc)[0].Permanent = true;
            Assert.That(memory.CullForgotten(loc, forgetThreshold: 0.05, fallbackHalfLifeSeconds: 3600, now: T0.AddYears(100)), Is.EqualTo(0));
            Assert.That(memory.Knows(loc), Is.True, "permanent memories survive culling");
        }

        // Spec: character-memory / Per-Character Memory Profiles
        //       Scenario "Forgetful and sharp characters" + "Profile overrides caps and growth"
        [Test]
        public void MemoryProfile_ScalesDecayAndGrowthAndCap()
        {
            var policy = new MemoryPolicy { DynamicsEnabled = true, DecayHalfLifeSeconds = 3600, StabilityGrowthFactor = 2.0, MaxLocations = 10000 };

            var forgetful = policy.ResolveDynamics(halfLifeMultiplier: 0.2);
            var sharp = policy.ResolveDynamics(halfLifeMultiplier: 5.0);

            // Same content, same age → the smaller half-life multiplier yields lower effective strength.
            var age = TimeSpan.FromSeconds(3600);
            var effForgetful = MemoryPolicy.EffectiveStrength(1.0, age, 0, false, forgetful.BaseHalfLifeSeconds);
            var effSharp = MemoryPolicy.EffectiveStrength(1.0, age, 0, false, sharp.BaseHalfLifeSeconds);
            Assert.That(effForgetful, Is.LessThan(effSharp), "a forgetful character retains less at the same age");

            // Growth multiplier scales the reinforcement factor.
            Assert.That(policy.ResolveDynamics(stabilityGrowthMultiplier: 2.0).StabilityGrowthFactor,
                Is.EqualTo(4.0).Within(1e-9));

            // Location-cap override replaces the world default.
            Assert.That(policy.ResolveDynamics(maxLocationsOverride: 42).MaxLocations, Is.EqualTo(42));
            Assert.That(policy.ResolveDynamics().MaxLocations, Is.EqualTo(10000));
        }

        // Spec: character-memory / Memory Dynamics Opt-In — Scenario "Default off preserves legacy behavior"
        [Test]
        public void DynamicsDisabled_ProducesLegacyBehavior()
        {
            var policy = new MemoryPolicy { DynamicsEnabled = false, DecayHalfLifeSeconds = 3600 };
            var dyn = policy.ResolveDynamics(halfLifeMultiplier: 0.01, stabilityGrowthMultiplier: 99, maxLocationsOverride: 1);

            // Profile is ignored entirely when dynamics are off.
            Assert.That(dyn.Enabled, Is.False);
            Assert.That(dyn.BaseHalfLifeSeconds, Is.EqualTo(3600), "disabled dynamics ignore the profile half-life multiplier");
            Assert.That(dyn.ForgetThreshold, Is.EqualTo(0), "no culling when dynamics are off");
            Assert.That(dyn.MaxLocations, Is.EqualTo(10000), "disabled dynamics ignore the profile cap override");

            // Re-encounter under a disabled dynamics writes no stability and no permanence.
            var memory = new Memory();
            var loc = new WorldLocation(0, 0, 0);
            var m = Record(memory, loc, dyn, T0);
            Record(memory, loc, dyn, T0.AddSeconds(100000)); // would be "spaced" if enabled
            Assert.That(m.StabilitySeconds, Is.EqualTo(0), "disabled dynamics write no stability");
            Assert.That(m.Permanent, Is.False);
            Assert.That(m.Impressions, Is.EqualTo(2), "legacy impression bump still happens");
        }
    }
}
