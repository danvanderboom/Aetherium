using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Aetherium.Core;
using Aetherium.Components;
using Aetherium.WorldGen;
using Aetherium.WorldGen.Hybrid;
using Aetherium.WorldGen.Passes;

namespace Aetherium.Test.WorldGen.Hybrid
{
    [TestFixture]
    public class HybridLayoutPassTests
    {
        [Test]
        public void HybridLayoutPass_Execute_WithBlockingAnchors_StoresBlockedLocations()
        {
            // Arrange
            var registry = new MapGeneratorRegistry();
            registry.DiscoverTypes(typeof(IMapGenerator).Assembly);

            var request = new WorldGenerationRequest
            {
                LayoutGenerator = "AdvancedDungeon",
                Template = WorldGenerationTemplate.Dungeon,
                Width = 40,
                Height = 40,
                Levels = 1,
                Seed = 12345,
                HybridAnchors = new HybridLayout
                {
                    Anchors = new List<HybridAnchor>
                    {
                        new HybridAnchor
                        {
                            Type = AnchorType.Rectangle,
                            X = 10,
                            Y = 10,
                            Width = 5,
                            Height = 5,
                            ZLevel = 0,
                            IsBlocking = true,
                            Priority = 1
                        }
                    }
                }
            };

            var context = new GeneratorContext(40, 40, 12345);
            var worldContext = new WorldGenerationContext(request, context);
            var pass = new HybridLayoutPass();

            // Act
            pass.Execute(worldContext);

            // Assert
            Assert.That(worldContext.GeneratorContext.PhaseArtifacts, Contains.Key("HybridLayout"));
            Assert.That(worldContext.GeneratorContext.PhaseArtifacts, Contains.Key("BlockedLocations"));
            
            var blockedLocations = worldContext.GeneratorContext.PhaseArtifacts["BlockedLocations"] as HashSet<WorldLocation>;
            Assert.That(blockedLocations, Is.Not.Null);
            Assert.That(blockedLocations.Count, Is.GreaterThan(0));

            // Verify specific blocked location
            var blockedLoc = new WorldLocation(12, 12, 0);
            Assert.That(blockedLocations, Contains.Item(blockedLoc));
        }

        [Test]
        public void HybridLayoutPass_Execute_WithRequiredAnchors_StoresRequiredLocations()
        {
            // Arrange
            var request = new WorldGenerationRequest
            {
                LayoutGenerator = "AdvancedDungeon",
                Template = WorldGenerationTemplate.Dungeon,
                Width = 40,
                Height = 40,
                Levels = 1,
                Seed = 12345,
                HybridAnchors = new HybridLayout
                {
                    Anchors = new List<HybridAnchor>
                    {
                        new HybridAnchor
                        {
                            Type = AnchorType.Point,
                            X = 20,
                            Y = 20,
                            ZLevel = 0,
                            IsBlocking = false,
                            Tags = new List<string> { "entrance" }
                        }
                    }
                }
            };

            var context = new GeneratorContext(40, 40, 12345);
            var worldContext = new WorldGenerationContext(request, context);
            var pass = new HybridLayoutPass();

            // Act
            pass.Execute(worldContext);

            // Assert
            Assert.That(worldContext.GeneratorContext.PhaseArtifacts, Contains.Key("RequiredLocations"));
            
            var requiredLocations = worldContext.GeneratorContext.PhaseArtifacts["RequiredLocations"] as HashSet<WorldLocation>;
            Assert.That(requiredLocations, Is.Not.Null);
            Assert.That(requiredLocations.Count, Is.GreaterThan(0));

            var requiredLoc = new WorldLocation(20, 20, 0);
            Assert.That(requiredLocations, Contains.Item(requiredLoc));
        }

        [Test]
        public void HybridLayoutPass_Execute_WithTaggedAnchors_StoresTaggedAnchors()
        {
            // Arrange
            var request = new WorldGenerationRequest
            {
                LayoutGenerator = "AdvancedDungeon",
                Template = WorldGenerationTemplate.Dungeon,
                Width = 40,
                Height = 40,
                Levels = 1,
                Seed = 12345,
                HybridAnchors = new HybridLayout
                {
                    Anchors = new List<HybridAnchor>
                    {
                        new HybridAnchor
                        {
                            Type = AnchorType.Point,
                            X = 15,
                            Y = 15,
                            ZLevel = 0,
                            Tags = new List<string> { "boss-room", "important" }
                        }
                    }
                }
            };

            var context = new GeneratorContext(40, 40, 12345);
            var worldContext = new WorldGenerationContext(request, context);
            var pass = new HybridLayoutPass();

            // Act
            pass.Execute(worldContext);

            // Assert
            Assert.That(worldContext.GeneratorContext.PhaseArtifacts, Contains.Key("TaggedAnchors"));
            
            var taggedAnchors = worldContext.GeneratorContext.PhaseArtifacts["TaggedAnchors"] as Dictionary<string, List<HybridAnchor>>;
            Assert.That(taggedAnchors, Is.Not.Null);
            Assert.That(taggedAnchors, Contains.Key("boss-room"));
            Assert.That(taggedAnchors["boss-room"], Is.Not.Empty);
            Assert.That(taggedAnchors, Contains.Key("important"));
        }

        [Test]
        public void HybridLayoutPass_Execute_WithoutAnchors_NoErrors()
        {
            // Arrange
            var request = new WorldGenerationRequest
            {
                LayoutGenerator = "AdvancedDungeon",
                Template = WorldGenerationTemplate.Dungeon,
                Width = 40,
                Height = 40,
                Levels = 1,
                Seed = 12345,
                HybridAnchors = null
            };

            var context = new GeneratorContext(40, 40, 12345);
            var worldContext = new WorldGenerationContext(request, context);
            var pass = new HybridLayoutPass();

            // Act & Assert
            Assert.DoesNotThrow(() => pass.Execute(worldContext));
        }
    }
}

