using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;
using Microsoft.Extensions.DependencyInjection;
using Aetherium.Server.Agents.Tools;
using Aetherium.Server.Agents.Tools.WorldBuilding;
using Aetherium.Core;
using Aetherium.Components;

namespace Aetherium.Test.Agents.Tools.WorldBuilding
{
    [TestFixture]
    public class ModifyEntityToolTests
    {
        private ModifyEntityTool _tool;
        private World _world;
        private IServiceProvider _serviceProvider;

        [SetUp]
        public void SetUp()
        {
            _tool = new ModifyEntityTool();
            _world = new World();
            _serviceProvider = new ServiceCollection().BuildServiceProvider();
        }

        private TestEntity AddEntity()
        {
            var entity = new TestEntity();
            entity.Set(new WorldLocation(0, 0, 0));
            _world.AddEntity(entity);
            return entity;
        }

        [Test]
        public void ToolId_ShouldBeModifyentity()
        {
            Assert.That(_tool.ToolId, Is.EqualTo("modifyentity"));
        }

        [Test]
        public async Task ExecuteAsync_ShouldFailWithoutWorldBuildingToolContext()
        {
            var context = new ToolExecutionContext
            {
                GrantedCapabilities = new HashSet<string> { "world_edit" },
                ServiceProvider = _serviceProvider
            };
            var args = new Dictionary<string, object> { ["entityId"] = "x" };

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
                ["addComponents"] = new List<string> { "Carriable" }
            };

            var result = await _tool.ExecuteAsync(context, args);

            Assert.That(result.Success, Is.False);
            Assert.That(result.Message, Does.Contain("not found"));
        }

        [Test]
        public async Task ExecuteAsync_ShouldFailWithNoModifications()
        {
            var entity = AddEntity();
            var context = new WorldBuildingToolContext(_world, _serviceProvider);
            var args = new Dictionary<string, object> { ["entityId"] = entity.EntityId };

            var result = await _tool.ExecuteAsync(context, args);

            Assert.That(result.Success, Is.False);
            Assert.That(result.Message, Does.Contain("No modifications"));
        }

        [Test]
        public async Task ExecuteAsync_ShouldAddComponent()
        {
            var entity = AddEntity();
            Assert.That(entity.Has<Carriable>(), Is.False);

            var context = new WorldBuildingToolContext(_world, _serviceProvider);
            var args = new Dictionary<string, object>
            {
                ["entityId"] = entity.EntityId,
                ["addComponents"] = new List<string> { "Carriable" }
            };

            var result = await _tool.ExecuteAsync(context, args);

            Assert.That(result.Success, Is.True);
            Assert.That(entity.Has<Carriable>(), Is.True);
        }

        [Test]
        public async Task ExecuteAsync_ShouldRemoveComponent()
        {
            var entity = AddEntity();
            entity.Set(new Carriable());
            Assert.That(entity.Has<Carriable>(), Is.True);

            var context = new WorldBuildingToolContext(_world, _serviceProvider);
            var args = new Dictionary<string, object>
            {
                ["entityId"] = entity.EntityId,
                ["removeComponents"] = new List<string> { "Carriable" }
            };

            var result = await _tool.ExecuteAsync(context, args);

            Assert.That(result.Success, Is.True);
            Assert.That(entity.Has<Carriable>(), Is.False);
        }

        [Test]
        public async Task ExecuteAsync_ShouldRejectRemovingProtectedComponent()
        {
            var entity = AddEntity();
            var context = new WorldBuildingToolContext(_world, _serviceProvider);
            var args = new Dictionary<string, object>
            {
                ["entityId"] = entity.EntityId,
                ["removeComponents"] = new List<string> { "WorldLocation" }
            };

            var result = await _tool.ExecuteAsync(context, args);

            Assert.That(result.Success, Is.False);
            Assert.That(result.Message, Does.Contain("protected"));
            Assert.That(entity.Has<WorldLocation>(), Is.True);
        }

        [Test]
        public async Task ExecuteAsync_ShouldFailWithUnknownComponent()
        {
            var entity = AddEntity();
            var context = new WorldBuildingToolContext(_world, _serviceProvider);
            var args = new Dictionary<string, object>
            {
                ["entityId"] = entity.EntityId,
                ["addComponents"] = new List<string> { "NotARealComponent" }
            };

            var result = await _tool.ExecuteAsync(context, args);

            Assert.That(result.Success, Is.False);
            Assert.That(result.Message, Does.Contain("Unknown component"));
        }

        private class TestEntity : Entity
        {
            public TestEntity()
            {
                Set(new WorldLocation(0, 0, 0));
            }
        }
    }
}
