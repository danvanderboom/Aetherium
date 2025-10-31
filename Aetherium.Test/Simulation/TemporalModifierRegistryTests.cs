using System;
using System.Threading.Tasks;
using NUnit.Framework;
using Aetherium.Server.Simulation;
using Aetherium.Server.MultiWorld;
using Aetherium.Server.Persistence;

namespace Aetherium.Test.Simulation
{
    [TestFixture]
    public class TemporalModifierRegistryTests
    {
        [Test]
        public void TemporalModifierRegistry_Register_AddsModifier()
        {
            var registry = new TemporalModifierRegistry();
            var modifier = new TestTemporalModifier("test", 10);
            
            registry.Register(modifier);
            
            var modifiers = registry.GetModifiers();
            Assert.AreEqual(1, modifiers.Count);
            Assert.AreEqual("test", modifiers[0].Name);
        }

        [Test]
        public void TemporalModifierRegistry_Register_SortsByPriority()
        {
            var registry = new TemporalModifierRegistry();
            
            var modifier1 = new TestTemporalModifier("mod1", 20);
            var modifier2 = new TestTemporalModifier("mod2", 10);
            var modifier3 = new TestTemporalModifier("mod3", 15);
            
            registry.Register(modifier1);
            registry.Register(modifier2);
            registry.Register(modifier3);
            
            var modifiers = registry.GetModifiers();
            Assert.AreEqual(3, modifiers.Count);
            Assert.AreEqual("mod2", modifiers[0].Name); // Priority 10 (lowest = highest priority)
            Assert.AreEqual("mod3", modifiers[1].Name); // Priority 15
            Assert.AreEqual("mod1", modifiers[2].Name); // Priority 20
        }

        [Test]
        public void TemporalModifierRegistry_ApplyAll_AppliesAllModifiers()
        {
            var registry = new TemporalModifierRegistry();
            var modifier1 = new TestTemporalModifier("mod1", 10);
            var modifier2 = new TestTemporalModifier("mod2", 20);
            
            registry.Register(modifier1);
            registry.Register(modifier2);
            
            // Create a mock region grain
            var mockRegion = new MockMapRegionGrain();
            var snapshot = new RegionStateSnapshot
            {
                RegionId = "region:0,0,0"
            };
            
            registry.ApplyAllAsync(
                mockRegion,
                snapshot,
                TimeSpan.FromHours(1),
                12.0,
                0
            ).Wait();
            
            Assert.AreEqual(1, modifier1.ApplyCount);
            Assert.AreEqual(1, modifier2.ApplyCount);
        }

        // Test helper classes
        private class TestTemporalModifier : ITemporalModifier
        {
            public string Name { get; }
            public int Priority { get; }
            public int ApplyCount { get; private set; }

            public TestTemporalModifier(string name, int priority)
            {
                Name = name;
                Priority = priority;
            }

            public Task ApplyAsync(
                IMapRegionGrain region,
                RegionStateSnapshot regionSnapshot,
                TimeSpan gameTimeElapsed,
                double timeOfDay,
                int day)
            {
                ApplyCount++;
                return Task.CompletedTask;
            }
        }

        private class MockMapRegionGrain : IMapRegionGrain
        {
            public Task InitializeAsync(string mapId, int regionX, int regionY, int zLevel, int regionSize)
                => Task.CompletedTask;

            public Task TickAsync(TimeSpan gameTimeElapsed)
                => Task.CompletedTask;

            public Task<RegionStateSnapshot> GetSnapshotAsync()
                => Task.FromResult(new RegionStateSnapshot());

            public Task LoadSnapshotAsync(RegionStateSnapshot snapshot)
                => Task.CompletedTask;

            public Task ApplyDeltaAsync(RegionDelta delta)
                => Task.CompletedTask;

            public Task RecordTraversalAsync(int x, int y)
                => Task.CompletedTask;

            public Task<System.Collections.Generic.Dictionary<(int x, int y), int>> GetTraversalHeatmapAsync()
                => Task.FromResult(new System.Collections.Generic.Dictionary<(int x, int y), int>());
        }
    }
}
