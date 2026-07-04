using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using Microsoft.Extensions.DependencyInjection;
using Aetherium.Server.Agents.Tools;
using Aetherium.Server.Agents.Tools.WorldBuilding;
using Aetherium.Core;
using Aetherium.Entities;
using Aetherium.Components;

namespace Aetherium.Test.Agents.Tools.WorldBuilding
{
    [TestFixture]
    public class SpawnEntityToolTests
    {
        private SpawnEntityTool _tool;
        private World _world;
        private IServiceProvider _serviceProvider;

        [SetUp]
        public void SetUp()
        {
            _tool = new SpawnEntityTool();
            _world = new World();
            _serviceProvider = new ServiceCollection().BuildServiceProvider();
        }

        [Test]
        public void ToolId_ShouldBeSpawnentity()
        {
            Assert.That(_tool.ToolId, Is.EqualTo("spawnentity"));
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
            Assert.That(schema.Required, Does.Contain("x"));
            Assert.That(schema.Required, Does.Contain("y"));
            Assert.That(schema.Required, Does.Contain("entityType"));
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
                ["x"] = 1,
                ["y"] = 1,
                ["entityType"] = "Item"
            };

            var result = await _tool.ExecuteAsync(context, args);

            Assert.That(result.Success, Is.False);
            Assert.That(result.Message, Does.Contain("WorldBuildingToolContext"));
        }

        [Test]
        public async Task ExecuteAsync_ShouldFailWithoutWorldEditCapability()
        {
            var context = new WorldBuildingToolContext(_world, _serviceProvider);
            context.GrantedCapabilities.Clear();
            var args = new Dictionary<string, object>
            {
                ["x"] = 1,
                ["y"] = 1,
                ["entityType"] = "Item"
            };

            var result = await _tool.ExecuteAsync(context, args);

            Assert.That(result.Success, Is.False);
            Assert.That(result.Message, Does.Contain("world_edit"));
        }

        [Test]
        public async Task ExecuteAsync_ShouldFailWithMissingEntityType()
        {
            var context = new WorldBuildingToolContext(_world, _serviceProvider);
            var args = new Dictionary<string, object> { ["x"] = 1, ["y"] = 1 };

            var result = await _tool.ExecuteAsync(context, args);

            Assert.That(result.Success, Is.False);
            Assert.That(result.Message, Does.Contain("entityType"));
        }

        [Test]
        public async Task ExecuteAsync_ShouldFailWithUnknownEntityType()
        {
            var context = new WorldBuildingToolContext(_world, _serviceProvider);
            var args = new Dictionary<string, object>
            {
                ["x"] = 1,
                ["y"] = 1,
                ["entityType"] = "Dragon"
            };

            var result = await _tool.ExecuteAsync(context, args);

            Assert.That(result.Success, Is.False);
            Assert.That(result.Message, Does.Contain("Unknown entity type"));
        }

        [Test]
        public async Task ExecuteAsync_ShouldSpawnItemSuccessfully()
        {
            var context = new WorldBuildingToolContext(_world, _serviceProvider);
            var args = new Dictionary<string, object>
            {
                ["x"] = 7,
                ["y"] = 8,
                ["z"] = 2,
                ["entityType"] = "Item"
            };

            var result = await _tool.ExecuteAsync(context, args);

            Assert.That(result.Success, Is.True);
            Assert.That(result.Data, Is.Not.Null);
            var entityId = result.Data!["entityId"].ToString();
            Assert.That(_world.Entities.ContainsKey(entityId!), Is.True);

            var spawned = _world.Entities[entityId!];
            Assert.That(spawned, Is.InstanceOf<Item>());
            var loc = spawned.Get<WorldLocation>();
            Assert.That(loc.X, Is.EqualTo(7));
            Assert.That(loc.Y, Is.EqualTo(8));
            Assert.That(loc.Z, Is.EqualTo(2));
        }

        [Test]
        public async Task ExecuteAsync_ShouldBeCaseInsensitiveForEntityType()
        {
            var context = new WorldBuildingToolContext(_world, _serviceProvider);
            var args = new Dictionary<string, object>
            {
                ["x"] = 0,
                ["y"] = 0,
                ["entityType"] = "dOoR"
            };

            var result = await _tool.ExecuteAsync(context, args);

            Assert.That(result.Success, Is.True);
            var entityId = result.Data!["entityId"].ToString();
            Assert.That(_world.Entities[entityId!], Is.InstanceOf<Door>());
        }

        [Test]
        public async Task ExecuteAsync_ShouldDefaultZToZero()
        {
            var context = new WorldBuildingToolContext(_world, _serviceProvider);
            var args = new Dictionary<string, object>
            {
                ["x"] = 3,
                ["y"] = 4,
                ["entityType"] = "Item"
            };

            var result = await _tool.ExecuteAsync(context, args);

            Assert.That(result.Success, Is.True);
            var entityId = result.Data!["entityId"].ToString();
            Assert.That(_world.Entities[entityId!].Get<WorldLocation>().Z, Is.EqualTo(0));
        }
    }
}
