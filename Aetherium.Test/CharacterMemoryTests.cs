using System;
using NUnit.Framework;
using Aetherium.Components;
using Aetherium.Core;

namespace Aetherium.Test
{
    /// <summary>
    /// Unit tests for the Memory component and MemoryPolicy. Each test maps to an OpenSpec
    /// requirement under changes/add-character-memory/specs/character-memory.
    /// </summary>
    [TestFixture]
    public class CharacterMemoryTests
    {
        // Spec: character-memory / Perception-Time Memory Recording
        //       Scenario "Record visible terrain and entities" (regression for the AddMemory
        //       copy-mutation bug that silently dropped every new memory)
        [Test]
        public void Remember_StoresNewMemory()
        {
            var memory = new Memory();
            var loc = new WorldLocation(3, 4, 0);

            memory.Remember(loc, "terrain", "Forest");

            Assert.That(memory.LocationsTracked, Is.EqualTo(1));
            Assert.That(memory.SpaceTimeMemoriesTracked, Is.EqualTo(1));
            Assert.That(memory.Knows(loc), Is.True);
            Assert.That(memory.Knowledge(loc)[0].Content, Is.EqualTo("Forest"));
        }

        // Spec: character-memory / Perception-Time Memory Recording
        //       Scenario "Reinforcement on re-encounter"
        [Test]
        public void Remember_IdenticalContent_BumpsImpressionsWithoutDuplicating()
        {
            var memory = new Memory();
            var loc = new WorldLocation(1, 1, 0);

            memory.Remember(loc, "terrain", "Cave");
            memory.Remember(loc, "terrain", "Cave");
            memory.Remember(loc, "terrain", "Cave");

            Assert.That(memory.SpaceTimeMemoriesTracked, Is.EqualTo(1), "identical content should not duplicate");
            Assert.That(memory.Knowledge(loc)[0].Impressions, Is.EqualTo(3));
        }

        // Spec: character-memory / Memory Decay and Caps — Scenario "Lazy strength decay"
        [Test]
        public void EffectiveStrength_HalvesPerHalfLife()
        {
            Assert.That(MemoryPolicy.EffectiveStrength(1.0, TimeSpan.FromSeconds(60), 60), Is.EqualTo(0.5).Within(1e-9));
            Assert.That(MemoryPolicy.EffectiveStrength(1.0, TimeSpan.FromSeconds(120), 60), Is.EqualTo(0.25).Within(1e-9));
            Assert.That(MemoryPolicy.EffectiveStrength(0.8, TimeSpan.Zero, 60), Is.EqualTo(0.8).Within(1e-9));
        }

        // Spec: character-memory / Memory Decay and Caps — Scenario "Decay disabled"
        [Test]
        public void EffectiveStrength_NoDecayWhenHalfLifeNonPositive()
        {
            Assert.That(MemoryPolicy.EffectiveStrength(0.7, TimeSpan.FromHours(100), 0), Is.EqualTo(0.7));
            Assert.That(MemoryPolicy.EffectiveStrength(0.7, TimeSpan.FromHours(100), -5), Is.EqualTo(0.7));
        }
    }
}
