using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Aetherium.Server.Agents;

namespace Aetherium.Test
{
    [TestFixture]
    public class MicrosoftAgentAdapterE2ETests
    {
        private const string DefaultApiBase = "http://localhost:1234/v1";
        private const string DefaultModel = "phi-4";

        [SetUp]
        public void SetUp()
        {
            // Save original environment variables
            TestEnvironment.SaveEnvironmentVariable("AGENT_LLM_ENABLED");
            TestEnvironment.SaveEnvironmentVariable("OPENAI_API_BASE");
            TestEnvironment.SaveEnvironmentVariable("AGENT_MODEL");
            TestEnvironment.SaveEnvironmentVariable("OPENAI_API_KEY");
        }

        [TearDown]
        public void TearDown()
        {
            // Restore original environment variables
            TestEnvironment.RestoreEnvironmentVariable("AGENT_LLM_ENABLED");
            TestEnvironment.RestoreEnvironmentVariable("OPENAI_API_BASE");
            TestEnvironment.RestoreEnvironmentVariable("AGENT_MODEL");
            TestEnvironment.RestoreEnvironmentVariable("OPENAI_API_KEY");
        }

        [Test]
        [Category("E2E")]
        public async Task DecideAsync_LMStudioAvailable_ReturnsDecision()
        {
            // Skip if LM Studio not available
            if (!await IsLMStudioAvailableAsync())
            {
                Assert.Inconclusive("LM Studio is not available. Start LM Studio with phi-4 model loaded to run this test.");
                return;
            }

            // Arrange
            Environment.SetEnvironmentVariable("OPENAI_API_BASE", DefaultApiBase);
            Environment.SetEnvironmentVariable("AGENT_MODEL", DefaultModel);
            Environment.SetEnvironmentVariable("OPENAI_API_KEY", "lm-studio");

            var adapter = new MicrosoftAgentAdapter();
            var perceptionJson = @"{
                ""playerLocation"": {""x"": 0, ""y"": 0, ""z"": 0},
                ""visibleItems"": [],
                ""affordances"": []
            }";

            // Act
            var decision = await adapter.DecideAsync(perceptionJson, CancellationToken.None);

            // Assert
            Assert.That(decision, Is.Not.Null, "Decision should not be null");
            Assert.That(decision.Action, Is.Not.Empty, "Action should not be empty");
        }

        [Test]
        [Category("E2E")]
        public async Task DecideAsync_Phi4ModelLoaded_ReturnsValidAction()
        {
            // Skip if LM Studio not available
            if (!await IsLMStudioAvailableAsync())
            {
                Assert.Inconclusive("LM Studio is not available. Start LM Studio with phi-4 model loaded to run this test.");
                return;
            }

            // Verify phi-4 model is available
            if (!await IsModelAvailableAsync(DefaultModel))
            {
                Assert.Inconclusive($"Model '{DefaultModel}' is not available. Load '{DefaultModel}' in LM Studio to run this test.");
                return;
            }

            // Arrange
            Environment.SetEnvironmentVariable("OPENAI_API_BASE", DefaultApiBase);
            Environment.SetEnvironmentVariable("AGENT_MODEL", DefaultModel);
            Environment.SetEnvironmentVariable("OPENAI_API_KEY", "lm-studio");

            var adapter = new MicrosoftAgentAdapter();
            var perceptionJson = @"{
                ""playerLocation"": {""x"": 5, ""y"": 5, ""z"": 0},
                ""playerHeading"": ""N"",
                ""visibleItems"": [
                    {""id"": ""item:key1"", ""label"": ""Key""}
                ],
                ""affordances"": [
                    {""action"": ""move"", ""targetId"": ""forward""},
                    {""action"": ""pickup"", ""targetId"": ""item:key1""}
                ]
            }";

            // Act
            var decision = await adapter.DecideAsync(perceptionJson, CancellationToken.None);

            // Assert
            Assert.That(decision, Is.Not.Null);
            Assert.That(decision.Action, Is.Not.Empty);
            
            // Valid actions: move, pickup, drop, open, close, use
            var validActions = new[] { "move", "pickup", "drop", "open", "close", "use" };
            Assert.That(validActions, Contains.Item(decision.Action.ToLowerInvariant()), 
                $"Action '{decision.Action}' should be one of: {string.Join(", ", validActions)}");
        }

        [Test]
        [Category("E2E")]
        public async Task DecideAsync_MoveAction_ReturnsDirection()
        {
            // Skip if LM Studio not available
            if (!await IsLMStudioAvailableAsync())
            {
                Assert.Inconclusive("LM Studio is not available. Start LM Studio with phi-4 model loaded to run this test.");
                return;
            }

            // Arrange
            Environment.SetEnvironmentVariable("OPENAI_API_BASE", DefaultApiBase);
            Environment.SetEnvironmentVariable("AGENT_MODEL", DefaultModel);
            Environment.SetEnvironmentVariable("OPENAI_API_KEY", "lm-studio");

            var adapter = new MicrosoftAgentAdapter();
            var perceptionJson = @"{
                ""playerLocation"": {""x"": 0, ""y"": 0, ""z"": 0},
                ""playerHeading"": ""N"",
                ""visibleItems"": [],
                ""affordances"": [
                    {""action"": ""move"", ""targetId"": ""forward""}
                ]
            }";

            // Act
            var decision = await adapter.DecideAsync(perceptionJson, CancellationToken.None);

            // Assert
            Assert.That(decision, Is.Not.Null);
            
            if (decision.Action.ToLowerInvariant() == "move")
            {
                Assert.That(decision.Args, Is.Not.Null, "Move action should have args");
                Assert.That(decision.Args!.ContainsKey("direction"), Is.True, "Move action should have direction");
                
                var validDirections = new[] { "F", "L", "R", "B", "N", "E", "S", "W" };
                var direction = decision.Args["direction"];
                Assert.That(validDirections, Contains.Item(direction), 
                    $"Direction '{direction}' should be one of: {string.Join(", ", validDirections)}");
            }
        }

        private async Task<bool> IsLMStudioAvailableAsync()
        {
            try
            {
                using var client = new HttpClient();
                client.Timeout = TimeSpan.FromSeconds(2);
                var response = await client.GetAsync($"{DefaultApiBase}/models");
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        private async Task<bool> IsModelAvailableAsync(string modelName)
        {
            try
            {
                using var client = new HttpClient();
                client.Timeout = TimeSpan.FromSeconds(2);
                var response = await client.GetAsync($"{DefaultApiBase}/models");
                if (!response.IsSuccessStatusCode)
                    return false;

                var content = await response.Content.ReadAsStringAsync();
                var models = JsonSerializer.Deserialize<ModelsResponse>(content);
                
                if (models?.data == null)
                    return false;

                foreach (var model in models.data)
                {
                    if (model.id != null && model.id.Contains(modelName, StringComparison.OrdinalIgnoreCase))
                        return true;
                }
                
                return false;
            }
            catch
            {
                return false;
            }
        }

        private class ModelsResponse
        {
            public ModelInfo[]? data { get; set; }
        }

        private class ModelInfo
        {
            public string? id { get; set; }
        }

        private static class TestEnvironment
        {
            private static readonly System.Collections.Generic.Dictionary<string, string?> _saved = new();

            public static void SaveEnvironmentVariable(string name)
            {
                _saved[name] = Environment.GetEnvironmentVariable(name);
            }

            public static void RestoreEnvironmentVariable(string name)
            {
                if (_saved.TryGetValue(name, out var value))
                {
                    Environment.SetEnvironmentVariable(name, value);
                }
                else
                {
                    Environment.SetEnvironmentVariable(name, null);
                }
            }
        }
    }
}


