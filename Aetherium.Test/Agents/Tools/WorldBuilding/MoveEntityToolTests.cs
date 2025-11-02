using System;
using System.Collections.Generic;
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
    public class MoveEntityToolTests
    {
        private MoveEntityTool _tool;
        private World _world;
        private IServiceProvider _serviceProvider;

        [SetUp]
        public void SetUp()
        {
            _tool = new MoveEntityTool();
            _world = new World();
            
            var services = new ServiceCollection();
            _serviceProvider = services.BuildServiceProvider();
        }

        [Test]
        public void ToolId_ShouldBeMoveentity()
        {
            Assert.That(_tool.ToolId, Is.EqualTo("moveentity"));
        }

        [Test]
        public void RequiredCapabilities_ShouldIncludeWorldEdit()
        {
            Assert.That(_tool.RequiredCapabilities, Does.Contain("world_edit"));
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
                ["entityId"] = "test-entity",
                ["x"] = 10,
                ["y"] = 20
            };

            var result = await _tool.ExecuteAsync(context, args);

            Assert.That(result.Success, Is.False);
            Assert.That(result.Message, Does.Contain("WorldBuildingToolContext"));
        }

        [Test]
        public async Task ExecuteAsync_ShouldFailWithMissingEntity()
        {
            var context = new WorldBuildingToolContext(_world, _serviceProvider);
            var args = new Dictionary<string, object>
            {
                ["entityId"] = "non-existent",
                ["x"] = 10,
                ["y"] = 20
            };

            var result = await _tool.ExecuteAsync(context, args);

            Assert.That(result.Success, Is.False);
            Assert.That(result.Message, Does.Contain("not found"));
        }

        [Test]
        public async Task ExecuteAsync_ShouldMoveEntitySuccessfully()
        {
            var context = new WorldBuildingToolContext(_world, _serviceProvider);
            
            // Create a test entity
            var entity = new TestEntity();
            entity.Set(new WorldLocation(5, 5, 0));
            _world.AddEntity(entity);
            
            var args = new Dictionary<string, object>
            {
                ["entityId"] = entity.EntityId,
                ["x"] = 10,
                ["y"] = 20,
                ["z"] = 1
            };

            var result = await _tool.ExecuteAsync(context, args);

            Assert.That(result.Success, Is.True);
            Assert.That(result.Message, Does.Contain("Moved"));
            
            var newLocation = entity.Get<WorldLocation>();
            Assert.That(newLocation.X, Is.EqualTo(10));
            Assert.That(newLocation.Y, Is.EqualTo(20));
            Assert.That(newLocation.Z, Is.EqualTo(1));
        }

        [Test]
        public async Task ExecuteAsync_ShouldUseDefaultZ()
        {
            var context = new WorldBuildingToolContext(_world, _serviceProvider);
            
            var entity = new TestEntity();
            entity.Set(new WorldLocation(0, 0, 0));
            _world.AddEntity(entity);
            
            var args = new Dictionary<string, object>
            {
                ["entityId"] = entity.EntityId,
                ["x"] = 15,
                ["y"] = 25
                // z not specified, should default to 0
            };

            var result = await _tool.ExecuteAsync(context, args);

            Assert.That(result.Success, Is.True);
            var newLocation = entity.Get<WorldLocation>();
            Assert.That(newLocation.Z, Is.EqualTo(0));
        }

        // Helper class for testing
        private class TestEntity : Entity
        {
            public TestEntity()
            {
                Set(new WorldLocation(0, 0, 0));
            }
        }
    }
}

