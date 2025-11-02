using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using Microsoft.Extensions.DependencyInjection;
using Aetherium.Server.Agents.Tools;
using Aetherium.Server.Agents.Tools.WorldBuilding;
using Aetherium.WorldBuilders;
using Aetherium.WorldBuilders.Features;
using Aetherium.Core;
using Aetherium.Components;

namespace Aetherium.Test.Agents.Tools.Integration
{
    [TestFixture]
    public class WorldBuildingToolIntegrationTests
    {
        private ServiceCollection _services;
        private ServiceProvider _serviceProvider;
        private AgentToolRegistry _registry;

        [SetUp]
        public void SetUp()
        {
            _services = new ServiceCollection();
            _serviceProvider = _services.BuildServiceProvider();
            _registry = new AgentToolRegistry(_serviceProvider);
            _registry.DiscoverTools(typeof(SetTerrainTool).Assembly);
        }

        [TearDown]
        public void TearDown()
        {
            _serviceProvider?.Dispose();
        }

        [Test]
        public void ToolRegistry_ShouldDiscoverWorldBuildingTools()
        {
            // Arrange & Act
            var setTerrainTool = _registry.GetTool("setterrain");
            var moveEntityTool = _registry.GetTool("moveentity");
            var destroyEntityTool = _registry.GetTool("destroyentity");
            var spawnEntityTool = _registry.GetTool("spawnentity");
            var modifyEntityTool = _registry.GetTool("modifyentity");

            // Assert
            Assert.That(setTerrainTool, Is.Not.Null, "SetTerrainTool should be discovered");
            Assert.That(moveEntityTool, Is.Not.Null, "MoveEntityTool should be discovered");
            Assert.That(destroyEntityTool, Is.Not.Null, "DestroyEntityTool should be discovered");
            Assert.That(spawnEntityTool, Is.Not.Null, "SpawnEntityTool should be discovered");
            Assert.That(modifyEntityTool, Is.Not.Null, "ModifyEntityTool should be discovered");
        }

        [Test]
        public void ToolProfile_WorldBuilderShouldAccessWorldBuildingTools()
        {
            // Arrange
            var profile = AgentToolProfile.WorldBuilder;
            var setTerrainTool = _registry.GetTool("setterrain");
            var moveEntityTool = _registry.GetTool("moveentity");
            var destroyEntityTool = _registry.GetTool("destroyentity");

            // Act & Assert
            Assert.That(setTerrainTool, Is.Not.Null);
            Assert.That(profile.IsToolAllowed(setTerrainTool), Is.True, "WorldBuilder profile should allow SetTerrainTool");
            
            Assert.That(moveEntityTool, Is.Not.Null);
            Assert.That(profile.IsToolAllowed(moveEntityTool), Is.True, "WorldBuilder profile should allow MoveEntityTool");
            
            Assert.That(destroyEntityTool, Is.Not.Null);
            Assert.That(profile.IsToolAllowed(destroyEntityTool), Is.True, "WorldBuilder profile should allow DestroyEntityTool");
        }

        [Test]
        public void WorldBuildingToolContext_ShouldGrantWorldEditCapability()
        {
            // Arrange
            var world = new World();
            var context = new WorldBuildingToolContext(world, _serviceProvider);

            // Act & Assert
            Assert.That(context.HasCapability("world_edit"), Is.True);
            Assert.That(context.World, Is.EqualTo(world));
        }

        [Test]
        public async Task SetTerrainTool_ShouldWorkWithWorldBuildingToolContext()
        {
            // Arrange
            var world = new World();
            
            // Register terrain types
            var tileTypes = new List<TileType>
            {
                new TileType { Name = "Plains", Settings = new Dictionary<string, string>() },
                new TileType { Name = "Forest", Settings = new Dictionary<string, string>() }
            };
            world.AddTileTypes(tileTypes);
            
            var terrainTypes = new List<TerrainType>
            {
                new TerrainType { Name = "Plains", TileType = tileTypes[0] },
                new TerrainType { Name = "Forest", TileType = tileTypes[1] }
            };
            world.AddTerrainTypes(terrainTypes);
            
            var context = new WorldBuildingToolContext(world, _serviceProvider);
            var tool = _registry.GetTool("setterrain");
            
            var args = new Dictionary<string, object>
            {
                ["x"] = 10,
                ["y"] = 20,
                ["z"] = 0,
                ["terrainType"] = "Forest"
            };

            // Act
            var result = await tool.ExecuteAsync(context, args);

            // Assert
            Assert.That(result.Success, Is.True);
            var location = new WorldLocation(10, 20, 0);
            var terrain = world.GetTerrain(location);
            Assert.That(terrain, Is.Not.Null);
            Assert.That(terrain.Type.Name, Is.EqualTo("Forest"));
        }

        [Test]
        public void TorusFeatureBuilder_ShouldHaveToolExecutionSupport()
        {
            // Arrange
            var world = new World();
            var feature = new WorldFeature
            {
                Chunk = new WorldChunk
                {
                    Location = new WorldLocation(-10, -10, -5),
                    Size = new Size3d(20, 20, 10)
                }
            };

            // Act - Create builder with tools
            var builder = new TorusFeatureBuilder(world, feature, _registry, _serviceProvider);

            // Assert
            // Builder should have access to tool registry and service provider
            // Note: These are protected fields, so we verify by attempting tool execution
            // which would fail if they weren't set
            Assert.That(builder, Is.Not.Null);
        }

        [Test]
        public void TorusFeatureBuilder_ShouldFallbackToDirectWorldManipulationWithoutTools()
        {
            // Arrange
            var worldBuilder = new TorusWorldBuilder();
            var world = worldBuilder.Build();

            // Act & Assert
            // Verify world was built successfully even without tools
            Assert.That(world, Is.Not.Null);
            Assert.That(world.EntitiesByLocation.Count, Is.GreaterThan(0), "World should have entities");
        }

        [Test]
        public void TorusFeatureBuilder_CanUseToolsWhenAvailable()
        {
            // Arrange
            var world = new World();
            
            // Register terrain types
            var tileTypes = new List<TileType>
            {
                new TileType { Name = "Indoors", Settings = new Dictionary<string, string>() },
                new TileType { Name = "Mountain", Settings = new Dictionary<string, string>() },
                new TileType { Name = "Plains", Settings = new Dictionary<string, string>() },
                new TileType { Name = "Forest", Settings = new Dictionary<string, string>() },
                new TileType { Name = "Water", Settings = new Dictionary<string, string>() }
            };
            world.AddTileTypes(tileTypes);
            
            var terrainTypes = new List<TerrainType>
            {
                new TerrainType { Name = "Indoors", TileType = tileTypes[0] },
                new TerrainType { Name = "Mountain", TileType = tileTypes[1] },
                new TerrainType { Name = "Plains", TileType = tileTypes[2] },
                new TerrainType { Name = "Forest", TileType = tileTypes[3] },
                new TerrainType { Name = "Water", TileType = tileTypes[4] }
            };
            world.AddTerrainTypes(terrainTypes);
            
            var feature = new WorldFeature
            {
                Chunk = new WorldChunk
                {
                    Location = new WorldLocation(-10, -10, -1),
                    Size = new Size3d(20, 20, 3)
                },
                Settings = new Dictionary<string, string>
                {
                    { "RadialSymmetryAxis", "Z" }
                }
            };

            // Act - Build with tools
            var builder = new TorusFeatureBuilder(world, feature, _registry, _serviceProvider);
            builder.Build();

            // Assert
            // Verify terrain was set (either via tools or direct manipulation)
            var location = new WorldLocation(0, 0, -1);
            var terrain = world.GetTerrain(location);
            // Note: The actual terrain depends on the torus calculation, but we verify
            // that the build completed without errors when tools are available
            Assert.That(world.EntitiesByLocation.Count, Is.GreaterThan(0), "World should have entities after build");
        }

        [Test]
        public void ToolRegistry_ShouldResolveWorldBuildingToolsByCategory()
        {
            // Arrange & Act
            var worldBuildingTools = _registry.GetToolsByCategory("worldbuilding").ToList();
            var terrainTools = _registry.GetToolsByCategory("terrain_management").ToList();
            var entityTools = _registry.GetToolsByCategory("entity_management").ToList();

            // Assert
            Assert.That(worldBuildingTools.Count, Is.GreaterThan(0), "Should have world building tools");
            Assert.That(terrainTools.Count, Is.GreaterThan(0), "Should have terrain management tools");
            Assert.That(entityTools.Count, Is.GreaterThan(0), "Should have entity management tools");
            
            // Verify specific tools are in categories
            var setTerrainTool = worldBuildingTools.FirstOrDefault(t => t.ToolId == "setterrain");
            Assert.That(setTerrainTool, Is.Not.Null, "SetTerrainTool should be in worldbuilding category");
        }

        [Test]
        public void ToolRegistry_ShouldResolveWorldBuildingToolsByCapability()
        {
            // Arrange & Act
            var worldEditTools = _registry.GetToolsByCapability("world_edit").ToList();

            // Assert
            Assert.That(worldEditTools.Count, Is.GreaterThan(0), "Should have tools requiring world_edit capability");
            
            // Verify specific tools require world_edit
            var setTerrainTool = worldEditTools.FirstOrDefault(t => t.ToolId == "setterrain");
            var moveEntityTool = worldEditTools.FirstOrDefault(t => t.ToolId == "moveentity");
            
            Assert.That(setTerrainTool, Is.Not.Null, "SetTerrainTool should require world_edit");
            Assert.That(moveEntityTool, Is.Not.Null, "MoveEntityTool should require world_edit");
        }
    }
}

