using System;
using System.Linq;
using Aetherium.Server.Agents.Telemetry;
using Aetherium.WorldGen;
using Aetherium.WorldGen.Hybrid;
using Aetherium.WorldGen.Passes;
using NUnit.Framework;

namespace Aetherium.Test
{
    /// <summary>
    /// Phase 3 convergence/debt regression tests: the shared worldgen pass catalog
    /// (P1-6) and the replay-storage growth cap (P1-15).
    /// </summary>
    [TestFixture]
    public class Phase3ConvergenceTests
    {
        [Test]
        public void Catalog_Dungeon_Includes_Theming_Population_Story_Audio_Portals()
        {
            var passes = WorldGenerationPassCatalog.BuildPasses(WorldGenerationTemplate.Dungeon);

            Assert.That(passes.Any(p => p is HybridLayoutPass), "hybrid layout missing");
            Assert.That(passes.Any(p => p is DungeonLayoutPass), "layout missing");
            Assert.That(passes.Any(p => p is DungeonThemingPass), "theming missing");
            Assert.That(passes.Any(p => p is DungeonPopulationPass), "population missing");
            Assert.That(passes.Any(p => p is EnvironmentalStoryPass), "environmental story missing");
            Assert.That(passes.Any(p => p is AudioGenerationPass), "audio missing");
            Assert.That(passes.Any(p => p is DungeonInteractionsPass), "interactions missing");
            Assert.That(passes.Any(p => p is PortalNetworkPass), "portal network missing");
            Assert.That(passes.Any(p => p is DungeonValidationPass), "validation missing");
        }

        [Test]
        public void Catalog_Outdoor_Includes_Theming_Population_Story_Audio_Portals()
        {
            var passes = WorldGenerationPassCatalog.BuildPasses(WorldGenerationTemplate.Outdoor);

            Assert.That(passes.Any(p => p is HybridLayoutPass), "hybrid layout missing");
            Assert.That(passes.Any(p => p is OutdoorLayoutPass), "layout missing");
            Assert.That(passes.Any(p => p is OutdoorThemingPass), "theming missing");
            Assert.That(passes.Any(p => p is OutdoorPopulationPass), "population missing");
            Assert.That(passes.Any(p => p is EnvironmentalStoryPass), "environmental story missing");
            Assert.That(passes.Any(p => p is AudioGenerationPass), "audio missing");
            Assert.That(passes.Any(p => p is OutdoorInteractionsPass), "interactions missing");
            Assert.That(passes.Any(p => p is PortalNetworkPass), "portal network missing");
            Assert.That(passes.Any(p => p is OutdoorValidationPass), "validation missing");
        }

        [Test]
        public void ReplayStorage_Json_Evicts_Oldest_Beyond_Cap()
        {
            // The cap is 200; store 250 and verify the earliest entries were evicted
            // while the newest are still retrievable.
            var ids = Enumerable.Range(0, 250)
                .Select(i => ReplayStorage.StoreReplayJson($"{{\"n\":{i}}}"))
                .ToList();

            Assert.That(ReplayStorage.GetReplayJson(ids[0]), Is.Null,
                "oldest JSON replay should have been evicted");
            Assert.That(ReplayStorage.GetReplayJson(ids[^1]), Is.Not.Null,
                "newest JSON replay must survive eviction");
        }

        [Test]
        public void ReplayStorage_Structured_Count_Stays_At_Or_Below_Cap()
        {
            for (int i = 0; i < 250; i++)
            {
                ReplayStorage.StoreReplay(new ReplayData
                {
                    AgentId = "cap-test-agent",
                    BenchmarkName = $"bench-{i}",
                });
            }

            Assert.That(ReplayStorage.GetReplayCount(), Is.LessThanOrEqualTo(200));
        }
    }
}
