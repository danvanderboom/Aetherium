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

