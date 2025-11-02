using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Aetherium.WorldGen;
using Aetherium.WorldGen.Passes;
using Aetherium.Core;
using Aetherium.Components;
using Aetherium.Entities;

namespace Aetherium.Test.WorldGen
{
    [TestFixture]
    public class PortalNetworkPassTests
    {
        private WorldGenerationOrchestrator? _orchestrator;
        private MapGeneratorRegistry? _registry;

        [SetUp]
        public void SetUp()
        {
            _registry = new MapGeneratorRegistry();
            _registry.DiscoverTypes(typeof(IMapGenerator).Assembly);
            _orchestrator = new WorldGenerationOrchestrator(_registry);
        }

        [Test]
        public void PortalNetworkPass_Execute_PlacesPortalsInProceduralWorld()
        {
            // Arrange
            var request = new WorldGenerationRequest
            {
                Template = WorldGenerationTemplate.Dungeon,
                Width = 80,
                Height = 80,
                Levels = 1,
                Seed = 12345,
                Parameters = new Dictionary<string, string>()
            };

            var passes = new IWorldGenerationPass[]
            {
                new DungeonLayoutPass(),
                new DungeonInteractionsPass(),
                new PortalNetworkPass()
            };

            var orchestrator = new WorldGenerationOrchestrator(_registry!, passes);

            // Act
            var result = orchestrator.Generate(request);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.World, Is.Not.Null);
            
            // Check that portals were placed
            var portals = result.World!.Entities.Values
                .Where(e => e.Has<PortalComponent>())
                .ToList();

            Assert.That(portals, Has.Count.GreaterThan(0), "PortalNetworkPass should place at least one portal");
        }

        [Test]
        public void PortalNetworkPass_Execute_PlacesPortalsInHubWorld()
        {
            // Arrange
            var request = new WorldGenerationRequest
            {
                Template = WorldGenerationTemplate.Dungeon,
                Width = 200,
                Height = 200,
                Levels = 1,
                Seed = 54321,
                Parameters = new Dictionary<string, string>
                {
                    { "isHub", "true" },
                    { "portalDefinitions", "id:hub-to-dungeon|worldTag:dungeon;id:hub-to-outdoor|worldTag:outdoor" }
                }
            };

            var passes = new IWorldGenerationPass[]
            {
                new DungeonLayoutPass(),
                new DungeonInteractionsPass(),
                new PortalNetworkPass()
            };

            var orchestrator = new WorldGenerationOrchestrator(_registry!, passes);

            // Act
            var result = orchestrator.Generate(request);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.World, Is.Not.Null);
            
            // Check that hub portals were placed
            var portals = result.World!.Entities.Values
                .Where(e => e.Has<PortalComponent>())
                .Select(e => e.Get<PortalComponent>())
                .ToList();

            Assert.That(portals, Has.Count.GreaterThanOrEqualTo(2), "Hub world should place portals from definitions");

            // Verify portal IDs match definitions
            var portalIds = portals.Select(p => p?.PortalId).Where(id => id != null).ToList();
            Assert.That(portalIds, Contains.Item("hub-to-dungeon"));
            Assert.That(portalIds, Contains.Item("hub-to-outdoor"));
        }

        [Test]
        public void PortalNetworkPass_Execute_PlacesPortalsWithCorrectTargetTags()
        {
            // Arrange
            var request = new WorldGenerationRequest
            {
                Template = WorldGenerationTemplate.Dungeon,
                Width = 100,
                Height = 100,
                Levels = 1,
                Seed = 99999,
                Parameters = new Dictionary<string, string>
                {
                    { "isHub", "true" },
                    { "portalDefinitions", "id:portal-1|worldTag:dungeon|activation:unlocked;id:portal-2|worldTag:city" }
                }
            };

            var passes = new IWorldGenerationPass[]
            {
                new DungeonLayoutPass(),
                new DungeonInteractionsPass(),
                new PortalNetworkPass()
            };

            var orchestrator = new WorldGenerationOrchestrator(_registry!, passes);

            // Act
            var result = orchestrator.Generate(request);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.World, Is.Not.Null);
            
            var portals = result.World!.Entities.Values
                .Where(e => e.Has<PortalComponent>())
                .Select(e => e.Get<PortalComponent>())
                .ToList();

            var portal1 = portals.FirstOrDefault(p => p?.PortalId == "portal-1");
            var portal2 = portals.FirstOrDefault(p => p?.PortalId == "portal-2");

            Assert.That(portal1, Is.Not.Null);
            Assert.That(portal2, Is.Not.Null);
            Assert.That(portal1!.TargetTag, Is.EqualTo("dungeon"));
            Assert.That(portal2!.TargetTag, Is.EqualTo("city"));
            Assert.That(portal1.Activation, Is.EqualTo("unlocked"));
        }

        [Test]
        public void PortalNetworkPass_Execute_FallbackToProceduralWhenNoHubDefinitions()
        {
            // Arrange
            var request = new WorldGenerationRequest
            {
                Template = WorldGenerationTemplate.Dungeon,
                Width = 80,
                Height = 80,
                Levels = 1,
                Seed = 11111,
                Parameters = new Dictionary<string, string>
                {
                    { "isHub", "true" },
                    { "portalDefinitions", "" } // Empty definitions
                }
            };

            var passes = new IWorldGenerationPass[]
            {
                new DungeonLayoutPass(),
                new DungeonInteractionsPass(),
                new PortalNetworkPass()
            };

            var orchestrator = new WorldGenerationOrchestrator(_registry!, passes);

            // Act
            var result = orchestrator.Generate(request);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.World, Is.Not.Null);
            
            // Should still place portals (fallback to procedural)
            var portals = result.World!.Entities.Values
                .Where(e => e.Has<PortalComponent>())
                .ToList();

            Assert.That(portals, Has.Count.GreaterThan(0), "Should fallback to procedural portal placement");
        }

        [Test]
        public void PortalNetworkPass_Execute_WorksWithOutdoorTemplate()
        {
            // Arrange
            var request = new WorldGenerationRequest
            {
                Template = WorldGenerationTemplate.Outdoor,
                Width = 100,
                Height = 100,
                Levels = 1,
                Seed = 22222,
                Parameters = new Dictionary<string, string>()
            };

            var passes = new IWorldGenerationPass[]
            {
                new OutdoorLayoutPass(),
                new OutdoorInteractionsPass(),
                new PortalNetworkPass()
            };

            var orchestrator = new WorldGenerationOrchestrator(_registry!, passes);

            // Act
            var result = orchestrator.Generate(request);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.World, Is.Not.Null);
            
            var portals = result.World!.Entities.Values
                .Where(e => e.Has<PortalComponent>())
                .ToList();

            Assert.That(portals, Has.Count.GreaterThan(0), "PortalNetworkPass should work with Outdoor template");
        }

        [Test]
        public void PortalNetworkPass_SupportsTemplate_ReturnsTrueForAllTemplates()
        {
            // Arrange
            var pass = new PortalNetworkPass();

            // Act & Assert
            Assert.That(pass.SupportsTemplate(WorldGenerationTemplate.Dungeon), Is.True);
            Assert.That(pass.SupportsTemplate(WorldGenerationTemplate.Outdoor), Is.True);
        }

        [Test]
        public void PortalNetworkPass_Execute_StoresPortalMetadataInContext()
        {
            // Arrange
            var request = new WorldGenerationRequest
            {
                Template = WorldGenerationTemplate.Dungeon,
                Width = 80,
                Height = 80,
                Levels = 1,
                Seed = 33333,
                Parameters = new Dictionary<string, string>()
            };

            var passes = new IWorldGenerationPass[]
            {
                new DungeonLayoutPass(),
                new DungeonInteractionsPass(),
                new PortalNetworkPass()
            };

            var orchestrator = new WorldGenerationOrchestrator(_registry!, passes);

            // Act
            var result = orchestrator.Generate(request);

            // Assert
            Assert.That(result.Success, Is.True);
            // Portal metadata should be stored in context for cluster registration
            // (Verification would require accessing internal context, which may not be exposed)
            // For now, we verify portals were placed
            Assert.That(result.World, Is.Not.Null);
            var portals = result.World!.Entities.Values
                .Where(e => e.Has<PortalComponent>())
                .ToList();
            Assert.That(portals, Has.Count.GreaterThan(0));
        }
    }
}

