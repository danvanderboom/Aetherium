using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Microsoft.Extensions.DependencyInjection;
using Aetherium.Server.Agents.Tools;
using Aetherium.Server.Agents.Tools.Movement;
using Aetherium.Server.Agents.Tools.Interaction;

namespace Aetherium.Test.Agents.Tools
{
    [TestFixture]
    public class AgentToolRegistryTests
    {
        private ServiceCollection _services;
        private ServiceProvider _serviceProvider;
        private AgentToolRegistry _registry;

        [SetUp]
        public void SetUp()
        {
            _services = new ServiceCollection();
            // Add any services that tools might need
            _serviceProvider = _services.BuildServiceProvider();
            _registry = new AgentToolRegistry(_serviceProvider);
        }

        [TearDown]
        public void TearDown()
        {
            _serviceProvider?.Dispose();
        }

        [Test]
        public void DiscoverTools_ShouldFindMovementTools()
        {
            // Arrange
            var assembly = typeof(MoveTool).Assembly;

            // Act
            _registry.DiscoverTools(assembly);
            var tools = _registry.ListTools().ToList();

            // Assert
            Assert.That(tools, Does.Contain("move"));
            Assert.That(tools, Does.Contain("rotate"));
            Assert.That(tools, Does.Contain("changelevel"));
            Assert.That(tools, Does.Contain("jumptolocation"));
        }

        [Test]
        public void DiscoverTools_ShouldFindInteractionTools()
        {
            // Arrange
            var assembly = typeof(PickupTool).Assembly;

            // Act
            _registry.DiscoverTools(assembly);
            var tools = _registry.ListTools().ToList();

            // Assert
            Assert.That(tools, Does.Contain("pickup"));
            Assert.That(tools, Does.Contain("drop"));
            Assert.That(tools, Does.Contain("use"));
            Assert.That(tools, Does.Contain("open"));
            Assert.That(tools, Does.Contain("close"));
        }

        [Test]
        public void GetTool_ShouldReturnNullForNonexistentTool()
        {
            // Arrange
            _registry.DiscoverTools(typeof(MoveTool).Assembly);

            // Act
            var tool = _registry.GetTool("nonexistent");

            // Assert
            Assert.That(tool, Is.Null);
        }

        [Test]
        public void GetTool_ShouldReturnMoveTool()
        {
            // Arrange
            _registry.DiscoverTools(typeof(MoveTool).Assembly);

            // Act
            var tool = _registry.GetTool("move");

            // Assert
            Assert.That(tool, Is.Not.Null);
            Assert.That(tool, Is.InstanceOf<MoveTool>());
            Assert.That(tool.ToolId, Is.EqualTo("move"));
        }

        [Test]
        public void GetTool_ShouldCacheInstances()
        {
            // Arrange
            _registry.DiscoverTools(typeof(MoveTool).Assembly);

            // Act
            var tool1 = _registry.GetTool("move");
            var tool2 = _registry.GetTool("move");

            // Assert
            Assert.That(tool1, Is.SameAs(tool2), "Should return the same cached instance");
        }

        [Test]
        public void GetToolsByCategory_ShouldFilterByMovement()
        {
            // Arrange
            _registry.DiscoverTools(typeof(MoveTool).Assembly);

            // Act
            var movementTools = _registry.GetToolsByCategory("movement").ToList();

            // Assert
            Assert.That(movementTools.Count, Is.GreaterThan(0));
            Assert.That(movementTools.Any(t => t.ToolId == "move"), Is.True);
            Assert.That(movementTools.All(t => t.Categories.Contains("movement")), Is.True);
        }

        [Test]
        public void GetToolsByCategory_ShouldFilterByInventory()
        {
            // Arrange
            _registry.DiscoverTools(typeof(PickupTool).Assembly);

            // Act
            var inventoryTools = _registry.GetToolsByCategory("inventory").ToList();

            // Assert
            Assert.That(inventoryTools.Count, Is.GreaterThanOrEqualTo(2));
            Assert.That(inventoryTools.Any(t => t.ToolId == "pickup"), Is.True);
            Assert.That(inventoryTools.Any(t => t.ToolId == "drop"), Is.True);
        }

        [Test]
        public void GetToolsByCapability_ShouldFilterCorrectly()
        {
            // Arrange
            _registry.DiscoverTools(typeof(MoveTool).Assembly);

            // Act
            var basicMovementTools = _registry.GetToolsByCapability("basic_movement").ToList();

            // Assert
            Assert.That(basicMovementTools.Count, Is.GreaterThan(0));
            Assert.That(basicMovementTools.All(t => t.RequiredCapabilities.Contains("basic_movement")), Is.True);
        }

        [Test]
        public void GetToolsForProfile_ShouldRespectExplorerProfile()
        {
            // Arrange
            _registry.DiscoverTools(typeof(MoveTool).Assembly);
            var profile = AgentToolProfile.Explorer;

            // Act
            var tools = _registry.GetToolsForProfile(profile).ToList();

            // Assert
            Assert.That(tools.Count, Is.GreaterThan(0));
            // Explorer should have movement tools
            Assert.That(tools.Any(t => t.ToolId == "move"), Is.True);
            // Explorer should NOT have admin tools
            Assert.That(tools.Any(t => t.RequiredCapabilities.Contains("admin")), Is.False);
        }

        [Test]
        public void GetToolsForProfile_ShouldRespectAdminProfile()
        {
            // Arrange
            _registry.DiscoverTools(typeof(MoveTool).Assembly);
            var profile = AgentToolProfile.Admin;

            // Act
            var tools = _registry.GetToolsForProfile(profile).ToList();

            // Assert
            // Admin should have access to all tools
            Assert.That(tools.Count, Is.GreaterThan(5));
        }

        [Test]
        public void HasTool_ShouldReturnTrueForRegisteredTool()
        {
            // Arrange
            _registry.DiscoverTools(typeof(MoveTool).Assembly);

            // Act & Assert
            Assert.That(_registry.HasTool("move"), Is.True);
            Assert.That(_registry.HasTool("pickup"), Is.True);
        }

        [Test]
        public void HasTool_ShouldReturnFalseForUnregisteredTool()
        {
            // Arrange
            _registry.DiscoverTools(typeof(MoveTool).Assembly);

            // Act & Assert
            Assert.That(_registry.HasTool("nonexistent"), Is.False);
        }
    }
}

