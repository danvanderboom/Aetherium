using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;
using Microsoft.Extensions.DependencyInjection;
using Aetherium;
using Aetherium.Server.Agents.Tools;
using Aetherium.Server.Agents.Tools.WorldBuilding;
using Aetherium.Core;
using Aetherium.Components;

namespace Aetherium.Test.Agents.Tools.WorldBuilding
{
    /// <summary>
    /// Unit tests for ConfigureCharacterTool (add-identity-recognition): setting memory/recognition
    /// profile fields on a live entity. Maps to identity-recognition / Runtime Profile Configuration.
    /// </summary>
    [TestFixture]
    public class ConfigureCharacterToolTests
    {
        private ConfigureCharacterTool _tool = null!;
        private World _world = null!;
        private IServiceProvider _serviceProvider = null!;
        private Character _npc = null!;

        [SetUp]
        public void SetUp()
        {
            _tool = new ConfigureCharacterTool();
            _world = new World();
            _serviceProvider = new ServiceCollection().BuildServiceProvider();

            _npc = new Character { EntityId = "npc-1" };
            _npc.Set(new WorldLocation(0, 0, 0));
            _world.AddEntity(_npc);
        }

        private WorldBuildingToolContext Context() => new(_world, _serviceProvider);

        // Spec: identity-recognition / Runtime Profile Configuration — Scenario "Configure an NPC live"
        [Test]
        public async Task Execute_SetsMemoryAndRecognitionProfiles()
        {
            var args = new Dictionary<string, object>
            {
                ["entityId"] = "npc-1",
                ["halfLifeMultiplier"] = "0.2",
                ["stabilityGrowthMultiplier"] = "3.0",
                ["maxLocationsOverride"] = "50",
                ["recognitionEnabled"] = "true",
                ["recognitionRange"] = "4",
                ["ownKindAcuity"] = "0.95",
                ["otherKindAcuity"] = "0.1",
            };

            var result = await _tool.ExecuteAsync(Context(), args);
            Assert.That(result.Success, Is.True, result.Message);

            Assert.That(_npc.Has<MemoryProfile>(), Is.True);
            var mem = _npc.Get<MemoryProfile>();
            Assert.That(mem.HalfLifeMultiplier, Is.EqualTo(0.2).Within(1e-9));
            Assert.That(mem.StabilityGrowthMultiplier, Is.EqualTo(3.0).Within(1e-9));
            Assert.That(mem.MaxLocationsOverride, Is.EqualTo(50));

            Assert.That(_npc.Has<RecognitionProfile>(), Is.True);
            var rec = _npc.Get<RecognitionProfile>();
            Assert.That(rec.EnabledOverride, Is.True);
            Assert.That(rec.RangeTilesOverride, Is.EqualTo(4));
            Assert.That(rec.OwnKindAcuityOverride, Is.EqualTo(0.95).Within(1e-9));
            Assert.That(rec.OtherKindAcuityOverride, Is.EqualTo(0.1).Within(1e-9));
        }

        // Spec: identity-recognition / Runtime Profile Configuration — Scenario "Configure an NPC live"
        //       (a forgetful override actually lowers this character's retained memory)
        [Test]
        public async Task Execute_ForgetfulProfile_LowersEffectiveStrength()
        {
            var args = new Dictionary<string, object>
            {
                ["entityId"] = "npc-1",
                ["halfLifeMultiplier"] = "0.2",
            };
            var result = await _tool.ExecuteAsync(Context(), args);
            Assert.That(result.Success, Is.True, result.Message);

            var policy = new MemoryPolicy { DynamicsEnabled = true, DecayHalfLifeSeconds = 3600 };
            var dyn = policy.ResolveDynamics(_npc.Get<MemoryProfile>().HalfLifeMultiplier);
            var age = TimeSpan.FromSeconds(3600);
            var forgetful = MemoryPolicy.EffectiveStrength(1.0, age, 0, false, dyn.BaseHalfLifeSeconds);
            var normal = MemoryPolicy.EffectiveStrength(1.0, age, 0, false, 3600);
            Assert.That(forgetful, Is.LessThan(normal));
        }

        // Spec: identity-recognition / Runtime Profile Configuration — Scenario "Unknown entity"
        [Test]
        public async Task Execute_UnknownEntity_Fails()
        {
            var args = new Dictionary<string, object> { ["entityId"] = "nope", ["ownKindAcuity"] = "0.5" };
            var result = await _tool.ExecuteAsync(Context(), args);
            Assert.That(result.Success, Is.False);
            Assert.That(result.Message, Does.Contain("nope"));
        }

        [Test]
        public async Task Execute_NoFields_Fails()
        {
            var args = new Dictionary<string, object> { ["entityId"] = "npc-1" };
            var result = await _tool.ExecuteAsync(Context(), args);
            Assert.That(result.Success, Is.False);
            Assert.That(result.Message, Does.Contain("field"));
        }
    }
}
