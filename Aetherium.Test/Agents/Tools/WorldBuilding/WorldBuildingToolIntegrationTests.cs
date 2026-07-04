using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Microsoft.Extensions.DependencyInjection;
using Aetherium.Server.Agents.Tools;
using Aetherium.Server.Agents.Tools.WorldBuilding;
using Aetherium.Core;
using Aetherium.Entities;
using Aetherium.Components;
using Aetherium.WorldBuilders;
using Aetherium.WorldBuilders.Features;

namespace Aetherium.Test.Agents.Tools.WorldBuilding
{
    /// <summary>
    /// Integration tests validating that the tool system and world building system
    /// interoperate: the registry resolves world building tools, and feature builders
    /// execute those tools against a real World via WorldBuildingToolContext.
    /// </summary>
    [TestFixture]
    public class WorldBuildingToolIntegrationTests
    {
        private World _world;
        private IServiceProvider _serviceProvider;
        private AgentToolRegistry _registry;

        [SetUp]
        public void SetUp()
        {
            _world = new World();
            RegisterTerrain(_world);

            _serviceProvider = new ServiceCollection().BuildServiceProvider();
            _registry = new AgentToolRegistry(_serviceProvider);
            _registry.DiscoverTools(typeof(SetTerrainTool).Assembly);
        }

        private static void RegisterTerrain(World world)
        {
            var tileTypes = new List<TileType>
            {
                new TileType { Name = "Plains", Settings = new Dictionary<string, string>() },
                new TileType { Name = "Forest", Settings = new Dictionary<string, string>() },
                new TileType { Name = "Water", Settings = new Dictionary<string, string>() },
                new TileType { Name = "Mountain", Settings = new Dictionary<string, string>() },
                new TileType { Name = "Indoors", Settings = new Dictionary<string, string>() }
            };
            world.AddTileTypes(tileTypes);
            world.AddTerrainTypes(new List<TerrainType>
            {
                new TerrainType { Name = "Plains", TileType = tileTypes[0] },
                new TerrainType { Name = "Forest", TileType = tileTypes[1] },
                new TerrainType { Name = "Water", TileType = tileTypes[2] },
                new TerrainType { Name = "Mountain", TileType = tileTypes[3] },
                new TerrainType { Name = "Indoors", TileType = tileTypes[4] }
            });
        }

        [Test]
        public void Registry_ResolvesAllWorldBuildingTools()
        {
            foreach (var id in new[] { "setterrain", "spawnentity", "moveentity", "modifyentity", "destroyentity" })
            {
                Assert.That(_registry.GetTool(id), Is.Not.Null, $"Expected tool '{id}' to be discoverable");
            }
        }

        [Test]
        public void FeatureBuilder_ExecutesTool_ViaRealRegistry()
        {
            var feature = new WorldFeature { Chunk = new WorldChunk(new WorldLocation(0, 0, 0), new Size3d(1, 1, 1)) };
            var builder = new ToolInvokingFeatureBuilder(_world, feature, _registry, _serviceProvider);

            var ok = builder.Invoke("setterrain", new Dictionary<string, object>
            {
                ["x"] = 3,
                ["y"] = 4,
                ["z"] = 0,
                ["terrainType"] = "Forest"
            });

            Assert.That(ok, Is.True);
            var terrain = _world.GetTerrain(new WorldLocation(3, 4, 0));
            Assert.That(terrain, Is.Not.Null);
            Assert.That(terrain!.Type.Name, Is.EqualTo("Forest"));
        }

        [Test]
        public void FeatureBuilder_WithoutRegistry_ReturnsFalse()
        {
            var feature = new WorldFeature { Chunk = new WorldChunk(new WorldLocation(0, 0, 0), new Size3d(1, 1, 1)) };
            var builder = new ToolInvokingFeatureBuilder(_world, feature); // no registry/provider

            var ok = builder.Invoke("setterrain", new Dictionary<string, object>
            {
                ["x"] = 0,
                ["y"] = 0,
                ["terrainType"] = "Plains"
            });

            Assert.That(ok, Is.False);
            Assert.That(_world.GetTerrain(new WorldLocation(0, 0, 0)), Is.Null);
        }

        [Test]
        public void TorusFeatureBuilder_WithRegistry_ProducesTerrainUsingTools()
        {
            // A generous, origin-centered chunk so the torus has interior points both
            // underground (tool path -> Indoors/Mountain) and at ground level.
            var chunk = new WorldChunk(new WorldLocation(-20, -20, -8), new Size3d(40, 40, 16));
            var feature = new WorldFeature
            {
                Chunk = chunk,
                Settings = new Dictionary<string, string> { ["RadialSymmetryAxis"] = "Z" }
            };

            var builder = new TorusFeatureBuilder(_world, feature, _registry, _serviceProvider);
            builder.Build();

            var terrains = _world.Entities.Values.OfType<Terrain>().ToList();
            Assert.That(terrains, Is.Not.Empty, "Torus build should produce terrain");

            // Underground terrain is set through the setterrain tool path.
            Assert.That(terrains.Any(t => t.Type.Name == "Indoors"), Is.True,
                "Expected tool-driven 'Indoors' terrain underground");
        }

        /// <summary>
        /// Minimal feature builder that exposes the protected ExecuteTool helper for testing.
        /// </summary>
        private sealed class ToolInvokingFeatureBuilder : WorldFeatureBuilder
        {
            public ToolInvokingFeatureBuilder(World world, WorldFeature feature)
                : base(world, feature) { }

            public ToolInvokingFeatureBuilder(World world, WorldFeature feature,
                AgentToolRegistry? registry, IServiceProvider? provider)
                : base(world, feature, registry, provider) { }

            public bool Invoke(string toolId, Dictionary<string, object> args) => ExecuteTool(toolId, args);

            public override void Build() { }
        }
    }
}
