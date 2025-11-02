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
    public class DestroyEntityToolTests
    {
        private DestroyEntityTool _tool;
        private World _world;
        private IServiceProvider _serviceProvider;

        [SetUp]
        public void SetUp()
        {
            _tool = new DestroyEntityTool();
            _world = new World();
            
            var services = new ServiceCollection();
            _serviceProvider = services.BuildServiceProvider();
        }

        [Test]
        public void ToolId_ShouldBeDestroyentity()
        {
            Assert.That(_tool.ToolId, Is.EqualTo("destroyentity"));
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
                ["entityId"] = "test-entity"
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
                ["entityId"] = "non-existent"
            };

            var result = await _tool.ExecuteAsync(context, args);

            Assert.That(result.Success, Is.False);
            Assert.That(result.Message, Does.Contain("not found"));
        }

        [Test]
        public async Task ExecuteAsync_ShouldDestroyEntitySuccessfully()
        {
            var context = new WorldBuildingToolContext(_world, _serviceProvider);
            
            // Create a test entity
            var entity = new TestEntity();
            entity.Set(new WorldLocation(10, 10, 0));
            _world.AddEntity(entity);
            
            var entityId = entity.EntityId;
            Assert.That(_world.Entities.ContainsKey(entityId), Is.True);
            
            var args = new Dictionary<string, object>
            {
                ["entityId"] = entityId
            };

            var result = await _tool.ExecuteAsync(context, args);

            Assert.That(result.Success, Is.True);
            Assert.That(result.Message, Does.Contain("Destroyed"));
            
            // Verify entity is removed
            Assert.That(_world.Entities.ContainsKey(entityId), Is.False);
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

