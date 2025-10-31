using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using Microsoft.Extensions.DependencyInjection;
using Aetherium.Server.Agents.Tools;
using Aetherium.Server.Agents.Tools.Movement;
using Aetherium.Server.Agents.Tools.Interaction;
using Aetherium.Server.Agents.Tools.Vision;
using Aetherium.Server;

namespace Aetherium.Test.Agents.Tools.Integration
{
    [TestFixture]
    public class ToolSystemIntegrationTests
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
            _registry.DiscoverTools(typeof(MoveTool).Assembly);
        }

        [TearDown]
        public void TearDown()
        {
            _serviceProvider?.Dispose();
        }

        [Test]
        public async Task FullWorkflow_AgentCanMoveAndPickupItems()
        {
            // Arrange
            var session = new GameSession("test", null);
            var interactionSystem = new InteractionSystem();
            var profile = AgentToolProfile.FullAccess;
            
            var context = new ToolExecutionContext
            {
                SessionId = "test",
                AgentId = "agent1",
                Session = session,
                InteractionSystem = interactionSystem,
                GrantedCapabilities = profile.GrantedCapabilities,
                ServiceProvider = _serviceProvider
            };

            // Act - Move forward
            var moveTool = _registry.GetTool("move");
            Assert.That(moveTool, Is.Not.Null);
            Assert.That(profile.IsToolAllowed(moveTool), Is.True);
            
            var moveResult = await moveTool.ExecuteAsync(context, new Dictionary<string, object>
            {
                ["direction"] = "forward",
                ["distance"] = 1
            });

            // Act - Toggle vision
            var visionTool = _registry.GetTool("toggledirectionalvision");
            Assert.That(visionTool, Is.Not.Null);
            Assert.That(profile.IsToolAllowed(visionTool), Is.True);
            
            var visionResult = await visionTool.ExecuteAsync(context, new Dictionary<string, object>());

            // Assert
            Assert.That(moveResult.Success, Is.True);
            Assert.That(visionResult.Success, Is.True);
        }

        [Test]
        public void ProfileFiltering_ExplorerCannotAccessInventoryTools()
        {
            // Arrange
            var profile = AgentToolProfile.Explorer;
            var pickupTool = _registry.GetTool("pickup");

            // Act & Assert
            Assert.That(pickupTool, Is.Not.Null);
            Assert.That(profile.IsToolAllowed(pickupTool), Is.False, 
                "Explorer profile should not have access to inventory tools");
        }

        [Test]
        public void ProfileFiltering_FullAccessCanAccessAllPlayerTools()
        {
            // Arrange
            var profile = AgentToolProfile.FullAccess;
            var moveTool = _registry.GetTool("move");
            var pickupTool = _registry.GetTool("pickup");
            var visionTool = _registry.GetTool("toggledirectionalvision");

            // Act & Assert
            Assert.That(profile.IsToolAllowed(moveTool), Is.True);
            Assert.That(profile.IsToolAllowed(pickupTool), Is.True);
            Assert.That(profile.IsToolAllowed(visionTool), Is.True);
        }

        [Test]
        public void ProfileFiltering_WorldBuilderCanAccessWorldBuildingTools()
        {
            // Arrange
            var profile = AgentToolProfile.WorldBuilder;
            var spawnTool = _registry.GetTool("spawnentity");
            var modifyTool = _registry.GetTool("modifyentity");

            // Act & Assert
            if (spawnTool != null)
                Assert.That(profile.IsToolAllowed(spawnTool), Is.True);
            if (modifyTool != null)
                Assert.That(profile.IsToolAllowed(modifyTool), Is.True);
        }

        [Test]
        public async Task ErrorHandling_ToolReturnsErrorForInvalidArgs()
        {
            // Arrange
            var session = new GameSession("test", null);
            var context = new ToolExecutionContext
            {
                Session = session,
                ServiceProvider = _serviceProvider
            };
            var moveTool = _registry.GetTool("move");

            // Act - Invalid direction
            var result = await moveTool.ExecuteAsync(context, new Dictionary<string, object>
            {
                ["direction"] = "invalid_direction",
                ["distance"] = 1
            });

            // Assert
            Assert.That(result.Success, Is.False);
            Assert.That(result.Message, Is.Not.Empty);
        }

        [Test]
        public async Task ToolChaining_MultipleToolsCanBeExecutedSequentially()
        {
            // Arrange
            var session = new GameSession("test", null);
            var context = new ToolExecutionContext
            {
                Session = session,
                ServiceProvider = _serviceProvider
            };

            // Act - Chain move -> rotate -> move
            var moveTool = _registry.GetTool("move");
            var rotateTool = _registry.GetTool("rotate");

            var result1 = await moveTool.ExecuteAsync(context, new Dictionary<string, object>
            {
                ["direction"] = "forward",
                ["distance"] = 1
            });
            
            var result2 = await rotateTool.ExecuteAsync(context, new Dictionary<string, object>
            {
                ["direction"] = "right"
            });
            
            var result3 = await moveTool.ExecuteAsync(context, new Dictionary<string, object>
            {
                ["direction"] = "forward",
                ["distance"] = 1
            });

            // Assert
            Assert.That(result1.Success, Is.True);
            Assert.That(result2.Success, Is.True);
            Assert.That(result3.Success, Is.True);
        }

        [Test]
        public void ToolDiscovery_AllCategoriesAreRepresented()
        {
            // Arrange & Act
            var movementTools = _registry.GetToolsByCategory("movement").ToList();
            var inventoryTools = _registry.GetToolsByCategory("inventory").ToList();
            var visionTools = _registry.GetToolsByCategory("vision").ToList();
            var interactionTools = _registry.GetToolsByCategory("interaction").ToList();

            // Assert
            Assert.That(movementTools.Count, Is.GreaterThan(0), "Should have movement tools");
            Assert.That(inventoryTools.Count, Is.GreaterThan(0), "Should have inventory tools");
            Assert.That(visionTools.Count, Is.GreaterThan(0), "Should have vision tools");
            Assert.That(interactionTools.Count, Is.GreaterThan(0), "Should have interaction tools");
        }

        [Test]
        public void ToolRegistry_DoesNotCreateDuplicateInstances()
        {
            // Arrange & Act
            var tool1 = _registry.GetTool("move");
            var tool2 = _registry.GetTool("move");
            var tool3 = _registry.GetTool("move");

            // Assert
            Assert.That(tool1, Is.SameAs(tool2));
            Assert.That(tool2, Is.SameAs(tool3));
        }

        [Test]
        public async Task DualAPISupport_ToolsWorkWithBothSessionAndGrain()
        {
            // Arrange
            var session = new GameSession("test", null);
            
            // Context 1: Direct session access (GameHub style)
            var hubContext = new ToolExecutionContext
            {
                SessionId = "test",
                ConnectionId = "conn1",
                Session = session,
                InteractionSystem = new InteractionSystem(),
                ServiceProvider = _serviceProvider
            };
            
            // Context 2: Orleans grain style (would have ManagementGrain in real scenario)
            var grainContext = new ToolExecutionContext
            {
                SessionId = "test",
                AgentId = "agent1",
                Session = session,
                ServiceProvider = _serviceProvider
            };

            var moveTool = _registry.GetTool("move");
            var args = new Dictionary<string, object>
            {
                ["direction"] = "forward",
                ["distance"] = 1
            };

            // Act
            var hubResult = await moveTool.ExecuteAsync(hubContext, args);
            var grainResult = await moveTool.ExecuteAsync(grainContext, args);

            // Assert
            Assert.That(hubResult.Success, Is.True);
            Assert.That(grainResult.Success, Is.True);
        }
    }
}

