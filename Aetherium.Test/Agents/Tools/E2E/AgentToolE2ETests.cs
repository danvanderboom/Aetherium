using System;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using Microsoft.Extensions.DependencyInjection;
using Aetherium.Server.Agents.Tools;
using Aetherium.Server.Agents.Tools.Movement;
using Aetherium.Server;
using Aetherium.Model;

namespace Aetherium.Test.Agents.Tools.E2E
{
    /// <summary>
    /// End-to-end tests for the agent tool system. These tests validate the full integration
    /// including tool registration, agent profiles, execution, and perception updates.
    /// </summary>
    [TestFixture]
    [Category("E2E")]
    public class AgentToolE2ETests
    {
        private ServiceCollection _services;
        private ServiceProvider _serviceProvider;
        private AgentToolRegistry _registry;
        private GameSession _session;
        private InteractionSystem _interactionSystem;

        [SetUp]
        public void SetUp()
        {
            _services = new ServiceCollection();
            _serviceProvider = _services.BuildServiceProvider();
            _registry = new AgentToolRegistry(_serviceProvider);
            _registry.DiscoverTools(typeof(MoveTool).Assembly);
            
            _session = new GameSession("e2e-test", null);
            _interactionSystem = new InteractionSystem();
        }

        [TearDown]
        public void TearDown()
        {
            _serviceProvider?.Dispose();
        }

        [Test]
        public async Task AgentLifecycle_ExplorerAgentNavigatesWorld()
        {
            // Arrange - Create an explorer agent with basic navigation capabilities
            var profile = AgentToolProfile.Explorer;
            var context = new ToolExecutionContext
            {
                SessionId = "e2e-test",
                AgentId = "explorer-1",
                Session = _session,
                InteractionSystem = _interactionSystem,
                GrantedCapabilities = profile.GrantedCapabilities,
                ServiceProvider = _serviceProvider
            };

            var startX = _session.ViewportX;
            var startY = _session.ViewportY;
            var startDirection = _session.Direction;

            // Act - Execute a series of movement commands
            var moveTool = _registry.GetTool("move");
            Assert.That(moveTool, Is.Not.Null);
            Assert.That(profile.IsToolAllowed(moveTool), Is.True, "Explorer should have move tool");

            var rotateTool = _registry.GetTool("rotate");
            Assert.That(rotateTool, Is.Not.Null);
            Assert.That(profile.IsToolAllowed(rotateTool), Is.True, "Explorer should have rotate tool");

            // Move forward
            var moveResult1 = await moveTool.ExecuteAsync(context, new System.Collections.Generic.Dictionary<string, object>
            {
                ["direction"] = "forward",
                ["distance"] = 2
            });

            // Rotate
            var rotateResult = await rotateTool.ExecuteAsync(context, new System.Collections.Generic.Dictionary<string, object>
            {
                ["direction"] = "right"
            });

            // Move forward again
            var moveResult2 = await moveTool.ExecuteAsync(context, new System.Collections.Generic.Dictionary<string, object>
            {
                ["direction"] = "forward",
                ["distance"] = 1
            });

            // Assert - All operations should succeed
            Assert.That(moveResult1.Success, Is.True);
            Assert.That(rotateResult.Success, Is.True);
            Assert.That(moveResult2.Success, Is.True);

            // Agent should have moved from starting position
            var moved = _session.ViewportX != startX || _session.ViewportY != startY;
            Assert.That(moved, Is.True, "Agent should have moved from start position");

            // Direction should have changed
            Assert.That(_session.Direction, Is.Not.EqualTo(startDirection), "Agent should have rotated");
        }

        [Test]
        public async Task AgentLifecycle_PlayerAgentUsesInventory()
        {
            // Arrange - Create a player agent with full capabilities
            var profile = AgentToolProfile.FullAccess;
            var context = new ToolExecutionContext
            {
                SessionId = "e2e-test",
                AgentId = "player-1",
                Session = _session,
                InteractionSystem = _interactionSystem,
                GrantedCapabilities = profile.GrantedCapabilities,
                ServiceProvider = _serviceProvider
            };

            // Act - Try to use inventory tools
            var pickupTool = _registry.GetTool("pickup");
            Assert.That(pickupTool, Is.Not.Null);
            Assert.That(profile.IsToolAllowed(pickupTool), Is.True, "Player should have pickup tool");

            var pickupResult = await pickupTool.ExecuteAsync(context, new System.Collections.Generic.Dictionary<string, object>
            {
                ["target"] = "test-item"
            });

            // Assert - Operation should execute (may fail if item doesn't exist, but tool should work)
            Assert.That(pickupResult, Is.Not.Null);
            Assert.That(pickupResult.Message, Is.Not.Empty);
        }

        [Test]
        public void AgentLifecycle_ExplorerCannotAccessInventory()
        {
            // Arrange
            var profile = AgentToolProfile.Explorer;
            var pickupTool = _registry.GetTool("pickup");

            // Assert - Explorer should not have access to inventory tools
            Assert.That(pickupTool, Is.Not.Null);
            Assert.That(profile.IsToolAllowed(pickupTool), Is.False, 
                "Explorer should NOT have access to inventory tools");
        }

        [Test]
        public async Task AgentLifecycle_MultipleAgentsInSameSession()
        {
            // Arrange - Create two different agent profiles
            var explorerProfile = AgentToolProfile.Explorer;
            var playerProfile = AgentToolProfile.FullAccess;

            var explorerContext = new ToolExecutionContext
            {
                SessionId = "e2e-test",
                AgentId = "explorer-1",
                Session = _session,
                GrantedCapabilities = explorerProfile.GrantedCapabilities,
                ServiceProvider = _serviceProvider
            };

            var playerContext = new ToolExecutionContext
            {
                SessionId = "e2e-test",
                AgentId = "player-1",
                Session = _session,
                InteractionSystem = _interactionSystem,
                GrantedCapabilities = playerProfile.GrantedCapabilities,
                ServiceProvider = _serviceProvider
            };

            var moveTool = _registry.GetTool("move");

            // Act - Both agents should be able to move
            var explorerMoveResult = await moveTool.ExecuteAsync(explorerContext, 
                new System.Collections.Generic.Dictionary<string, object>
                {
                    ["direction"] = "forward",
                    ["distance"] = 1
                });

            var playerMoveResult = await moveTool.ExecuteAsync(playerContext, 
                new System.Collections.Generic.Dictionary<string, object>
                {
                    ["direction"] = "backward",
                    ["distance"] = 1
                });

            // Assert
            Assert.That(explorerMoveResult.Success, Is.True);
            Assert.That(playerMoveResult.Success, Is.True);
        }

        [Test]
        public void ToolDiscovery_AllRequiredToolsAreDiscovered()
        {
            // Assert - Check that key tool categories are represented
            var movementTools = _registry.GetToolsByCategory("movement").ToList();
            var interactionTools = _registry.GetToolsByCategory("interaction").ToList();
            var visionTools = _registry.GetToolsByCategory("vision").ToList();

            Assert.That(movementTools.Count, Is.GreaterThanOrEqualTo(3), 
                "Should have at least 3 movement tools");
            Assert.That(interactionTools.Count, Is.GreaterThanOrEqualTo(3), 
                "Should have at least 3 interaction tools");
            Assert.That(visionTools.Count, Is.GreaterThanOrEqualTo(2), 
                "Should have at least 2 vision tools");

            // Check specific tools exist
            Assert.That(_registry.HasTool("move"), Is.True);
            Assert.That(_registry.HasTool("rotate"), Is.True);
            Assert.That(_registry.HasTool("pickup"), Is.True);
            Assert.That(_registry.HasTool("drop"), Is.True);
            Assert.That(_registry.HasTool("toggledirectionalvision"), Is.True);
        }

        [Test]
        public void ProfileSystem_AllPredefinedProfilesAreValid()
        {
            // Arrange - Get all predefined profiles
            var profiles = new[]
            {
                AgentToolProfile.Explorer,
                AgentToolProfile.FullAccess,
                AgentToolProfile.WorldBuilder,
                AgentToolProfile.NarrativeDesigner,
                AgentToolProfile.Player,
                AgentToolProfile.Admin
            };

            // Act & Assert - Each profile should have tools
            foreach (var profile in profiles)
            {
                var tools = _registry.GetToolsForProfile(profile).ToList();
                Assert.That(tools.Count, Is.GreaterThan(0), 
                    $"Profile {profile.ProfileName} should have at least one tool");

                // Check that profile capabilities are consistent
                foreach (var tool in tools)
                {
                    Assert.That(profile.IsToolAllowed(tool), Is.True,
                        $"Tool {tool.ToolId} should be allowed by profile {profile.ProfileName}");
                }
            }
        }

        [Test]
        public async Task PerformanceTest_ToolExecutionIsEfficient()
        {
            // Arrange
            var profile = AgentToolProfile.FullAccess;
            var context = new ToolExecutionContext
            {
                SessionId = "e2e-test",
                AgentId = "perf-test",
                Session = _session,
                GrantedCapabilities = profile.GrantedCapabilities,
                ServiceProvider = _serviceProvider
            };

            var moveTool = _registry.GetTool("move");
            var args = new System.Collections.Generic.Dictionary<string, object>
            {
                ["direction"] = "forward",
                ["distance"] = 1
            };

            // Act - Execute tool multiple times and measure time
            var startTime = DateTime.UtcNow;
            const int iterations = 100;

            for (int i = 0; i < iterations; i++)
            {
                await moveTool.ExecuteAsync(context, args);
            }

            var elapsed = DateTime.UtcNow - startTime;
            var avgMs = elapsed.TotalMilliseconds / iterations;

            // Assert - Average execution should be reasonably fast (< 10ms per call)
            Assert.That(avgMs, Is.LessThan(10.0), 
                $"Tool execution should be efficient. Average: {avgMs:F2}ms");

            Console.WriteLine($"Performance: {iterations} iterations in {elapsed.TotalMilliseconds:F2}ms " +
                            $"(avg {avgMs:F2}ms per call)");
        }
    }
}

