using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;
using Aetherium.Server.Agents.Tools;
using Aetherium.Server.Agents.Tools.Movement;
using Aetherium.Server;
using Aetherium.Model;
using Aetherium.WorldBuilders;

namespace Aetherium.Test.Agents.Tools
{
    [TestFixture]
    public class MoveToolTests
    {
        private MoveTool _tool;

        [SetUp]
        public void SetUp()
        {
            _tool = new MoveTool();
        }

        [Test]
        public void ToolId_ShouldBeMove()
        {
            Assert.That(_tool.ToolId, Is.EqualTo("move"));
        }

        [Test]
        public void Description_ShouldNotBeEmpty()
        {
            Assert.That(_tool.Description, Is.Not.Empty);
        }

        [Test]
        public void Categories_ShouldIncludeMovement()
        {
            Assert.That(_tool.Categories, Does.Contain("movement"));
            Assert.That(_tool.Categories, Does.Contain("navigation"));
        }

        [Test]
        public void RequiredCapabilities_ShouldIncludeBasicMovement()
        {
            Assert.That(_tool.RequiredCapabilities, Does.Contain("basic_movement"));
        }

        [Test]
        public void GetParameterSchema_ShouldHaveDirectionParameter()
        {
            var schema = _tool.GetParameterSchema();
            Assert.That(schema.Properties, Contains.Key("direction"));
            Assert.That(schema.Required, Does.Contain("direction"));
        }

        [Test]
        public void GetParameterSchema_ShouldHaveDistanceParameter()
        {
            var schema = _tool.GetParameterSchema();
            Assert.That(schema.Properties, Contains.Key("distance"));
        }

        [Test]
        public async Task ExecuteAsync_ShouldFailWithoutSession()
        {
            var context = new ToolExecutionContext
            {
                Session = null,
                GrantedCapabilities = new HashSet<string> { "basic_movement" } // Has capability but no session
            };
            var args = new Dictionary<string, object>
            {
                ["direction"] = "forward",
                ["distance"] = 1
            };

            var result = await _tool.ExecuteAsync(context, args);

            Assert.That(result.Success, Is.False);
            Assert.That(result.Message, Does.Contain("No execution context"));
        }

        [Test]
        public async Task ExecuteAsync_ShouldMoveForward()
        {
            var worldBuilder = new TorusWorldBuilder();
            var session = new GameSession("test", worldBuilder);
            // Movement is validated (P0-1); carve open ground so the forward
            // step doesn't depend on maze geometry at the spawn point.
            TestWorldMovement.CarveOpenArea(session);
            var startX = session.ViewLocation?.X ?? 0;
            var startY = session.ViewLocation?.Y ?? 0;
            
            var context = new ToolExecutionContext
            {
                Session = session,
                GrantedCapabilities = new HashSet<string> { "basic_movement" } // Need capability
            };
            var args = new Dictionary<string, object>
            {
                ["direction"] = "forward",
                ["distance"] = 1
            };

            var result = await _tool.ExecuteAsync(context, args);

            Assert.That(result.Success, Is.True);
            // Position should have changed based on direction
            Assert.That(session.ViewLocation?.X != startX || session.ViewLocation?.Y != startY, Is.True);
        }

        [Test]
        public async Task ExecuteAsync_PrefersGateway_OverManagementGrain()
        {
            // THE canonical-body regression (live: "continuous damage with no monster in
            // sight, then death"). The hub sets BOTH a grain-routed MutationGateway and a
            // ManagementGrain on the context; checking the management grain first routed
            // every player move through session.MoveView — session-local — so the player's
            // canonical body never left spawn while monsters converged on and killed it.
            // The gateway must win whenever it is present.
            var gateway = new Moq.Mock<Aetherium.Server.MultiWorld.IMapMutationGateway>();
            gateway.Setup(g => g.MoveAsync(Aetherium.Model.RelativeDirection.Forward, 1))
                .Returns(Task.FromResult(Aetherium.Server.MultiWorld.MoveResult.Ok()));
            var managementGrain = new Moq.Mock<Aetherium.Server.Management.IGameManagementGrain>(Moq.MockBehavior.Strict);

            var context = new ToolExecutionContext
            {
                SessionId = "s1",
                MutationGateway = gateway.Object,
                ManagementGrain = managementGrain.Object,
                GrantedCapabilities = new HashSet<string> { "basic_movement" },
            };

            var result = await _tool.ExecuteAsync(context, new Dictionary<string, object>
            {
                ["direction"] = "forward",
                ["distance"] = 1,
            });

            Assert.That(result.Success, Is.True, result.Message);
            gateway.Verify(g => g.MoveAsync(Aetherium.Model.RelativeDirection.Forward, 1), Moq.Times.Once);
            managementGrain.VerifyNoOtherCalls(); // strict: any management-grain call fails the test
        }

        [Test]
        public async Task ExecuteAsync_ShouldHandleInvalidDirection()
        {
            var worldBuilder = new TorusWorldBuilder();
            var session = new GameSession("test", worldBuilder);
            var context = new ToolExecutionContext
            {
                Session = session,
                GrantedCapabilities = new HashSet<string> { "basic_movement" } // Need capability
            };
            var args = new Dictionary<string, object>
            {
                ["direction"] = "invalid",
                ["distance"] = 1
            };

            var result = await _tool.ExecuteAsync(context, args);

            Assert.That(result.Success, Is.False);
        }
    }
}

