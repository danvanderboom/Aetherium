using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;
using Aetherium.Server.Agents.Tools;
using Aetherium.Server.Agents.Tools.Interaction;
using Aetherium.Server;

namespace Aetherium.Test.Agents.Tools
{
    [TestFixture]
    public class PickupToolTests
    {
        private PickupTool _tool;

        [SetUp]
        public void SetUp()
        {
            _tool = new PickupTool();
        }

        [Test]
        public void ToolId_ShouldBePickup()
        {
            Assert.That(_tool.ToolId, Is.EqualTo("pickup"));
        }

        [Test]
        public void Description_ShouldNotBeEmpty()
        {
            Assert.That(_tool.Description, Is.Not.Empty);
        }

        [Test]
        public void Categories_ShouldIncludeInventory()
        {
            Assert.That(_tool.Categories, Does.Contain("inventory"));
            Assert.That(_tool.Categories, Does.Contain("interaction"));
        }

        [Test]
        public void RequiredCapabilities_ShouldIncludeInventoryAccess()
        {
            Assert.That(_tool.RequiredCapabilities, Does.Contain("inventory_access"));
        }

        [Test]
        public void GetParameterSchema_ShouldHaveTargetParameter()
        {
            var schema = _tool.GetParameterSchema();
            Assert.That(schema.Properties, Contains.Key("target"));
            Assert.That(schema.Required, Does.Contain("target"));
        }

        [Test]
        public async Task ExecuteAsync_ShouldFailWithoutSession()
        {
            var context = new ToolExecutionContext
            {
                Session = null
            };
            var args = new Dictionary<string, object>
            {
                ["target"] = "item1"
            };

            var result = await _tool.ExecuteAsync(context, args);

            Assert.That(result.Success, Is.False);
            Assert.That(result.Message, Does.Contain("No session"));
        }

        [Test]
        public async Task ExecuteAsync_ShouldFailWithoutInteractionSystem()
        {
            var session = new GameSession("test", null);
            var context = new ToolExecutionContext
            {
                Session = session,
                InteractionSystem = null
            };
            var args = new Dictionary<string, object>
            {
                ["target"] = "item1"
            };

            var result = await _tool.ExecuteAsync(context, args);

            Assert.That(result.Success, Is.False);
            Assert.That(result.Message, Does.Contain("Interaction system"));
        }

        [Test]
        public async Task ExecuteAsync_ShouldFailWithMissingTarget()
        {
            var session = new GameSession("test", null);
            var interactionSystem = new InteractionSystem();
            
            var context = new ToolExecutionContext
            {
                Session = session,
                InteractionSystem = interactionSystem
            };
            var args = new Dictionary<string, object>();

            var result = await _tool.ExecuteAsync(context, args);

            Assert.That(result.Success, Is.False);
            Assert.That(result.Message, Does.Contain("target"));
        }
    }
}

