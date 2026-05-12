using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;
using Aetherium.Server.Agents.Tools;
using Aetherium.Server.Agents.Tools.Interaction;
using Aetherium.Server;
using Aetherium.WorldBuilders;

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
            Assert.That(schema.Properties, Contains.Key("targetEntityId"));
            Assert.That(schema.Required, Does.Contain("targetEntityId"));
        }

        [Test]
        public async Task ExecuteAsync_ShouldFailWithoutSession()
        {
            var context = new ToolExecutionContext
            {
                Session = null,
                GrantedCapabilities = new HashSet<string> { "inventory_access" } // Has capability but no session
            };
            var args = new Dictionary<string, object>
            {
                ["targetEntityId"] = "item1"
            };

            var result = await _tool.ExecuteAsync(context, args);

            Assert.That(result.Success, Is.False);
            Assert.That(result.Message, Does.Contain("No execution context"));
        }

        [Test]
        public async Task ExecuteAsync_ShouldFailWithoutSessionOrGateway()
        {
            // Phase 2a: a ToolExecutionContext with neither a Session nor an explicit
            // MutationGateway nor a ManagementGrain has no way to dispatch the call,
            // and the tool SHALL return a "No execution context" failure.
            // (Previously this test asserted on a missing InteractionSystem; after
            // phase 2a, the gateway auto-falls-back from Session if present, so the
            // gating dependency is Session-OR-explicit-gateway, not InteractionSystem.)
            var context = new ToolExecutionContext
            {
                Session = null,
                MutationGateway = null,
                ManagementGrain = null,
                GrantedCapabilities = new HashSet<string> { "inventory_access" }
            };
            var args = new Dictionary<string, object>
            {
                ["targetEntityId"] = "item1"
            };

            var result = await _tool.ExecuteAsync(context, args);

            Assert.That(result.Success, Is.False);
            Assert.That(result.Message, Does.Contain("execution context"));
        }

        [Test]
        public async Task ExecuteAsync_ShouldFailWithMissingTarget()
        {
            var worldBuilder = new TorusWorldBuilder();
            var session = new GameSession("test", worldBuilder);
            var interactionSystem = new InteractionSystem();
            
            var context = new ToolExecutionContext
            {
                Session = session,
                GrantedCapabilities = new HashSet<string> { "inventory_access" } // Need capability
            };
            var args = new Dictionary<string, object>();

            var result = await _tool.ExecuteAsync(context, args);

            Assert.That(result.Success, Is.False);
            Assert.That(result.Message, Does.Contain("targetEntityId")); // Changed to match actual parameter
        }
    }
}

