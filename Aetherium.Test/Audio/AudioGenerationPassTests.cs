using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Aetherium.Core;
using Aetherium.Entities;
using Aetherium.WorldGen;
using Aetherium.WorldGen.Passes;
using World = Aetherium.Core.World;
using TileType = Aetherium.Core.TileType;
using TerrainType = Aetherium.Core.TerrainType;
using WorldLocation = Aetherium.Components.WorldLocation;

namespace Aetherium.Test.Audio
{
    [TestFixture]
    public class AudioGenerationPassTests
    {
        private World _world = null!;
        private WorldGenerationContext _context = null!;
        private AudioGenerationPass _pass = null!;

        [SetUp]
        public void SetUp()
        {
            _world = new World();
            
            // Add terrain types
            var tileTypes = new List<TileType>
            {
                new TileType { Name = "Forest", Settings = new Dictionary<string, string>() },
                new TileType { Name = "Dungeon", Settings = new Dictionary<string, string>() },
                new TileType { Name = "Wall", Settings = new Dictionary<string, string>() }
            };
            _world.AddTileTypes(tileTypes);

            var terrainTypes = new List<TerrainType>
            {
                new TerrainType { Name = "Forest", TileType = tileTypes[0] },
                new TerrainType { Name = "Dungeon", TileType = tileTypes[1] },
                new TerrainType { Name = "Wall", TileType = tileTypes[2] }
            };
            _world.AddTerrainTypes(terrainTypes);

            // Create request and context
            var request = new WorldGenerationRequest
            {
                LayoutGenerator = "test",
                Width = 10,
                Height = 10,
                Levels = 1,
                Seed = 12345,
                Template = WorldGenerationTemplate.Dungeon,
                Parameters = new Dictionary<string, string>()
            };

            var generatorContext = new GeneratorContext(10, 10, 12345);
            _context = new WorldGenerationContext(request, generatorContext)
            {
                World = _world
            };

            _pass = new AudioGenerationPass();
        }

        [Test]
        public void SupportsTemplate_ReturnsTrue_ForAnyTemplate()
        {
            // Act & Assert
            Assert.That(_pass.SupportsTemplate(WorldGenerationTemplate.Dungeon), Is.True);
            Assert.That(_pass.SupportsTemplate(WorldGenerationTemplate.Outdoor), Is.True);
        }

        [Test]
        public void Execute_AddsError_WhenWorldIsNull()
        {
            // Arrange
            _context.World = null;

            // Act
            _pass.Execute(_context);

            // Assert
            Assert.That(_context.Errors, Is.Not.Empty);
            Assert.That(_context.Errors[0], Does.Contain("world instance"));
        }

        [Test]
        public void Execute_CreatesAudioZones_ForTerrainLocations()
        {
            // Arrange
            _world.SetTerrain("Forest", new WorldLocation(5, 5, 0));
            _world.SetTerrain("Dungeon", new WorldLocation(6, 6, 0));

            // Act
            _pass.Execute(_context);

            // Assert
            Assert.That(_context.SharedData.ContainsKey("audio:zones"), Is.True);
            var zones = _context.SharedData["audio:zones"] as Dictionary<WorldLocation, Aetherium.WorldGen.Passes.AudioZone>;
            Assert.That(zones, Is.Not.Null);
            Assert.That(zones!.Count, Is.GreaterThanOrEqualTo(2));
        }

        [Test]
        public void Execute_CreatesBiomeMapping_ForTerrainLocations()
        {
            // Arrange
            _world.SetTerrain("Forest", new WorldLocation(5, 5, 0));
            _world.SetTerrain("Dungeon", new WorldLocation(6, 6, 0));

            // Act
            _pass.Execute(_context);

            // Assert
            Assert.That(_context.SharedData.ContainsKey("audio:biomeMapping"), Is.True);
            var mapping = _context.SharedData["audio:biomeMapping"] as Dictionary<WorldLocation, string>;
            Assert.That(mapping, Is.Not.Null);
            Assert.That(mapping!.ContainsKey(new WorldLocation(5, 5, 0)), Is.True);
            Assert.That(mapping[new WorldLocation(5, 5, 0)], Is.EqualTo("forest"));
        }

        [Test]
        public void Execute_SkipsWallTerrain_InZones()
        {
            // Arrange
            _world.SetTerrain("Forest", new WorldLocation(5, 5, 0));
            _world.SetTerrain("Wall", new WorldLocation(5, 6, 0));

            // Act
            _pass.Execute(_context);

            // Assert
            var mapping = _context.SharedData["audio:biomeMapping"] as Dictionary<WorldLocation, string>;
            Assert.That(mapping!.ContainsKey(new WorldLocation(5, 5, 0)), Is.True);
            Assert.That(mapping.ContainsKey(new WorldLocation(5, 6, 0)), Is.False);
        }

        [Test]
        public void Execute_AddsMetrics()
        {
            // Arrange
            _world.SetTerrain("Forest", new WorldLocation(5, 5, 0));

            // Act
            _pass.Execute(_context);

            // Assert
            Assert.That(_context.Metrics.HasMetric("audio.zones.count"), Is.True);
            Assert.That(_context.Metrics.HasMetric("audio.biomes.detected"), Is.True);
        }

        [Test]
        public void Execute_PhaseIsAdaptation()
        {
            // Assert
            Assert.That(_pass.Phase, Is.EqualTo(GenerationPhase.Adaptation));
        }
    }
}

