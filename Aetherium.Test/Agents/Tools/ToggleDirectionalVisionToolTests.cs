using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;
using Aetherium.Server.Agents.Tools;
using Aetherium.Server.Agents.Tools.Vision;
using Aetherium.Server;
using Aetherium.WorldBuilders;

namespace Aetherium.Test.Agents.Tools
{
    [TestFixture]
    public class ToggleDirectionalVisionToolTests
    {
        private ToggleDirectionalVisionTool _tool;

        [SetUp]
        public void SetUp()
        {
            _tool = new ToggleDirectionalVisionTool();
        }

        [Test]
        public void ToolId_ShouldBeToggleDirectionalVision()
        {
            Assert.That(_tool.ToolId, Is.EqualTo("toggledirectionalvision"));
        }

        [Test]
        public void Description_ShouldNotBeEmpty()
        {
            Assert.That(_tool.Description, Is.Not.Empty);
        }

        [Test]
        public void Categories_ShouldIncludeVision()
        {
            Assert.That(_tool.Categories, Does.Contain("vision"));
            Assert.That(_tool.Categories, Does.Contain("perception"));
        }

        [Test]
        public void RequiredCapabilities_ShouldIncludeVision()
        {
            Assert.That(_tool.RequiredCapabilities, Does.Contain("vision"));
        }

        [Test]
        public async Task ExecuteAsync_ShouldFailWithoutSession()
        {
            var context = new ToolExecutionContext
            {
                Session = null,
                GrantedCapabilities = new HashSet<string> { "vision" } // Has capability but no session
            };
            var args = new Dictionary<string, object>();

            var result = await _tool.ExecuteAsync(context, args);

            Assert.That(result.Success, Is.False);
            Assert.That(result.Message, Does.Contain("No execution context"));
        }

        [Test]
        public async Task ExecuteAsync_ShouldToggleVision()
        {
            var worldBuilder = new TorusWorldBuilder();
            var session = new GameSession("test", worldBuilder);
            var initialState = session.DirectionalVisionMode;
            
            var context = new ToolExecutionContext
            {
                Session = session,
                GrantedCapabilities = new HashSet<string> { "vision" } // Need capability
            };
            var args = new Dictionary<string, object>();

            var result = await _tool.ExecuteAsync(context, args);

            Assert.That(result.Success, Is.True);
            Assert.That(session.DirectionalVisionMode, Is.Not.EqualTo(initialState));
        }

        [Test]
        public async Task ExecuteAsync_ShouldToggleBackAndForth()
        {
            var worldBuilder = new TorusWorldBuilder();
            var session = new GameSession("test", worldBuilder);
            var context = new ToolExecutionContext
            {
                Session = session,
                GrantedCapabilities = new HashSet<string> { "vision" } // Need capability
            };
            var args = new Dictionary<string, object>();

            var initialState = session.DirectionalVisionMode;
            await _tool.ExecuteAsync(context, args);
            var afterFirstToggle = session.DirectionalVisionMode;
            await _tool.ExecuteAsync(context, args);
            var afterSecondToggle = session.DirectionalVisionMode;

            Assert.That(afterFirstToggle, Is.Not.EqualTo(initialState));
            Assert.That(afterSecondToggle, Is.EqualTo(initialState));
        }
    }
}

