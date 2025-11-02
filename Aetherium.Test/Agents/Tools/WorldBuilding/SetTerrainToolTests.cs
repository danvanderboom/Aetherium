using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;
using Microsoft.Extensions.DependencyInjection;
using Aetherium.Server.Agents.Tools;
using Aetherium.Server.Agents.Tools.WorldBuilding;
using Aetherium.Core;
using Aetherium.Components;
using Aetherium.WorldBuilders;

namespace Aetherium.Test.Agents.Tools.WorldBuilding
{
    [TestFixture]
    public class SetTerrainToolTests
    {
        private SetTerrainTool _tool;
        private World _world;
        private IServiceProvider _serviceProvider;

        [SetUp]
        public void SetUp()
        {
            _tool = new SetTerrainTool();
            _world = new World();
            
            // Register terrain types
            var tileTypes = new List<TileType>
            {
                new TileType { Name = "Plains", Settings = new Dictionary<string, string>() },
                new TileType { Name = "Forest", Settings = new Dictionary<string, string>() },
                new TileType { Name = "Mountain", Settings = new Dictionary<string, string>() },
                new TileType { Name = "Water", Settings = new Dictionary<string, string>() }
            };
            _world.AddTileTypes(tileTypes);
            
            var terrainTypes = new List<TerrainType>
            {
                new TerrainType { Name = "Plains", TileType = tileTypes[0] },
                new TerrainType { Name = "Forest", TileType = tileTypes[1] },
                new TerrainType { Name = "Mountain", TileType = tileTypes[2] },
                new TerrainType { Name = "Water", TileType = tileTypes[3] }
            };
            _world.AddTerrainTypes(terrainTypes);
            
            var services = new ServiceCollection();
            _serviceProvider = services.BuildServiceProvider();
        }

        [Test]
        public void ToolId_ShouldBeSetterrain()
        {
            Assert.That(_tool.ToolId, Is.EqualTo("setterrain"));
        }

        [Test]
        public void Description_ShouldNotBeEmpty()
        {
            Assert.That(_tool.Description, Is.Not.Empty);
        }

        [Test]
        public void Categories_ShouldIncludeWorldBuilding()
        {
            Assert.That(_tool.Categories, Does.Contain("worldbuilding"));
            Assert.That(_tool.Categories, Does.Contain("terrain_management"));
        }

        [Test]
        public void RequiredCapabilities_ShouldIncludeWorldEdit()
        {
            Assert.That(_tool.RequiredCapabilities, Does.Contain("world_edit"));
        }

        [Test]
        public void GetParameterSchema_ShouldHaveRequiredParameters()
        {
            var schema = _tool.GetParameterSchema();
            Assert.That(schema.Properties, Contains.Key("x"));
            Assert.That(schema.Properties, Contains.Key("y"));
            Assert.That(schema.Properties, Contains.Key("z"));
            Assert.That(schema.Properties, Contains.Key("terrainType"));
            Assert.That(schema.Required, Does.Contain("x"));
            Assert.That(schema.Required, Does.Contain("y"));
            Assert.That(schema.Required, Does.Contain("terrainType"));
        }

        [Test]
        public async Task ExecuteAsync_ShouldFailWithoutWorldBuildingToolContext()
        {
            var context = new ToolExecutionContext
            {
                GrantedCapabilities = new HashSet<string> { "world_edit" },
                ServiceProvider = _serviceProvider
            };
            var args = new Dictionary<string, object>
            {
                ["x"] = 0,
                ["y"] = 0,
                ["terrainType"] = "Plains"
            };

            var result = await _tool.ExecuteAsync(context, args);

            Assert.That(result.Success, Is.False);
            Assert.That(result.Message, Does.Contain("WorldBuildingToolContext"));
        }

        [Test]
        public async Task ExecuteAsync_ShouldFailWithoutWorldEditCapability()
        {
            var context = new WorldBuildingToolContext(_world, _serviceProvider);
            // Remove world_edit capability
            context.GrantedCapabilities.Clear();
            
            var args = new Dictionary<string, object>
            {
                ["x"] = 0,
                ["y"] = 0,
                ["terrainType"] = "Plains"
            };

            var result = await _tool.ExecuteAsync(context, args);

            Assert.That(result.Success, Is.False);
            Assert.That(result.Message, Does.Contain("world_edit"));
        }

        [Test]
        public async Task ExecuteAsync_ShouldFailWithInvalidX()
        {
            var context = new WorldBuildingToolContext(_world, _serviceProvider);
            var args = new Dictionary<string, object>
            {
                ["y"] = 0,
                ["terrainType"] = "Plains"
            };

            var result = await _tool.ExecuteAsync(context, args);

            Assert.That(result.Success, Is.False);
            Assert.That(result.Message, Does.Contain("x"));
        }

        [Test]
        public async Task ExecuteAsync_ShouldFailWithInvalidY()
        {
            var context = new WorldBuildingToolContext(_world, _serviceProvider);
            var args = new Dictionary<string, object>
            {
                ["x"] = 0,
                ["terrainType"] = "Plains"
            };

            var result = await _tool.ExecuteAsync(context, args);

            Assert.That(result.Success, Is.False);
            Assert.That(result.Message, Does.Contain("y"));
        }

        [Test]
        public async Task ExecuteAsync_ShouldFailWithMissingTerrainType()
        {
            var context = new WorldBuildingToolContext(_world, _serviceProvider);
            var args = new Dictionary<string, object>
            {
                ["x"] = 0,
                ["y"] = 0
            };

            var result = await _tool.ExecuteAsync(context, args);

            Assert.That(result.Success, Is.False);
            Assert.That(result.Message, Does.Contain("terrainType"));
        }

        [Test]
        public async Task ExecuteAsync_ShouldFailWithUnregisteredTerrainType()
        {
            var context = new WorldBuildingToolContext(_world, _serviceProvider);
            var args = new Dictionary<string, object>
            {
                ["x"] = 0,
                ["y"] = 0,
                ["terrainType"] = "NonExistent"
            };

            var result = await _tool.ExecuteAsync(context, args);

            Assert.That(result.Success, Is.False);
            Assert.That(result.Message, Does.Contain("not registered"));
        }

        [Test]
        public async Task ExecuteAsync_ShouldSetTerrainSuccessfully()
        {
            var context = new WorldBuildingToolContext(_world, _serviceProvider);
            var location = new WorldLocation(10, 20, 0);
            var args = new Dictionary<string, object>
            {
                ["x"] = 10,
                ["y"] = 20,
                ["z"] = 0,
                ["terrainType"] = "Forest"
            };

            var result = await _tool.ExecuteAsync(context, args);

            Assert.That(result.Success, Is.True);
            Assert.That(result.Message, Does.Contain("Forest"));
            
            var terrain = _world.GetTerrain(location);
            Assert.That(terrain, Is.Not.Null);
            Assert.That(terrain.Type.Name, Is.EqualTo("Forest"));
        }

        [Test]
        public async Task ExecuteAsync_ShouldUseDefaultZ()
        {
            var context = new WorldBuildingToolContext(_world, _serviceProvider);
            var location = new WorldLocation(5, 5, 0);
            var args = new Dictionary<string, object>
            {
                ["x"] = 5,
                ["y"] = 5,
                ["terrainType"] = "Plains"
                // z not specified, should default to 0
            };

            var result = await _tool.ExecuteAsync(context, args);

            Assert.That(result.Success, Is.True);
            var terrain = _world.GetTerrain(location);
            Assert.That(terrain, Is.Not.Null);
            Assert.That(terrain.Type.Name, Is.EqualTo("Plains"));
        }

        [Test]
        public async Task ExecuteAsync_ShouldReplaceExistingTerrain()
        {
            var context = new WorldBuildingToolContext(_world, _serviceProvider);
            var location = new WorldLocation(15, 15, 0);
            
            // Set initial terrain
            _world.SetTerrain("Plains", location);
            var initialTerrain = _world.GetTerrain(location);
            Assert.That(initialTerrain.Type.Name, Is.EqualTo("Plains"));
            
            // Replace with new terrain
            var args = new Dictionary<string, object>
            {
                ["x"] = 15,
                ["y"] = 15,
                ["z"] = 0,
                ["terrainType"] = "Mountain"
            };

            var result = await _tool.ExecuteAsync(context, args);

            Assert.That(result.Success, Is.True);
            var newTerrain = _world.GetTerrain(location);
            Assert.That(newTerrain.Type.Name, Is.EqualTo("Mountain"));
        }
    }
}

