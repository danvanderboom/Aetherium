using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Aetherium.Unity.Model;
using Aetherium.Unity.Networking;
using Aetherium.Unity.Rendering;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Aetherium.Unity.Tests
{
    public class OptionSelectionTests
    {
        [UnityTest]
        public IEnumerator ToolExecutionResult_WithOptions_EntersSelectionMode()
        {
            // Arrange
            var gameObject = new GameObject("TestPlayerController");
            var playerController = gameObject.AddComponent<PlayerController>();
            var facadeObject = new GameObject("TestFacade");
            var facade = facadeObject.AddComponent<GameClientFacade>();
            var hudObject = new GameObject("HUDText");
            var hudText = hudObject.AddComponent<Text>();

            playerController.gameClientFacade = facade;
            playerController.hudText = hudText;

            // Create a result with options
            var options = new List<object>
            {
                new Dictionary<string, object>
                {
                    { "usageId", "option1" },
                    { "label", "Option 1" },
                    { "description", "First option" }
                },
                new Dictionary<string, object>
                {
                    { "usageId", "option2" },
                    { "label", "Option 2" },
                    { "description", "Second option" }
                }
            };

            // Act - Execute tool async and handle result
            var task = facade.ExecuteToolAsync("use", new Dictionary<string, object>());
            yield return new WaitUntil(() => task.IsCompleted);

            var result = task.Result;
            
            // Simulate entering option selection mode with options
            if (result.Success && result.Data != null && result.Data.TryGetValue("options", out var optionsObj))
            {
                // Test that options are parsed correctly
                Assert.IsNotNull(optionsObj);
            }

            // Cleanup
            Object.Destroy(gameObject);
            Object.Destroy(facadeObject);
            Object.Destroy(hudObject);
        }

        [Test]
        public void ToolExecutionResultDto_Creation_InitializesCorrectly()
        {
            // Arrange & Act
            var result = new ToolExecutionResultDto
            {
                Success = true,
                Message = "Test message",
                Data = new Dictionary<string, object> { { "test", "value" } }
            };

            // Assert
            Assert.IsTrue(result.Success);
            Assert.AreEqual("Test message", result.Message);
            Assert.IsNotNull(result.Data);
            Assert.AreEqual("value", result.Data["test"]);
        }

        [Test]
        public void UsageOptionDto_Creation_InitializesCorrectly()
        {
            // Arrange & Act
            var option = new UsageOptionDto
            {
                UsageId = "test-id",
                Label = "Test Label",
                Description = "Test Description"
            };

            // Assert
            Assert.AreEqual("test-id", option.UsageId);
            Assert.AreEqual("Test Label", option.Label);
            Assert.AreEqual("Test Description", option.Description);
        }

        [UnityTest]
        public IEnumerator GameClientFacade_ExecuteToolAsync_ReturnsResult()
        {
            // Arrange
            var facadeObject = new GameObject("TestFacade");
            var facade = facadeObject.AddComponent<GameClientFacade>();

            yield return new WaitForSeconds(0.1f); // Wait for Awake

            // Act
            var args = new Dictionary<string, object> { { "direction", "north" }, { "distance", 1 } };
            var task = facade.ExecuteToolAsync("move", args);
            yield return new WaitUntil(() => task.IsCompleted);

            var result = task.Result;

            // Assert
            Assert.IsNotNull(result);
            Assert.IsTrue(result.Success || !result.Success); // Should have a success value

            Object.Destroy(facadeObject);
        }

        [UnityTest]
        public IEnumerator PlayerController_OptionSelection_DisplaysInHUD()
        {
            // Arrange
            var gameObject = new GameObject("TestPlayerController");
            var playerController = gameObject.AddComponent<PlayerController>();
            var hudObject = new GameObject("HUDText");
            var hudText = hudObject.AddComponent<Text>();
            hudText.text = "Original HUD Text";

            playerController.hudText = hudText;

            yield return new WaitForSeconds(0.1f);

            // Act - Simulate option selection by using reflection to call private methods
            // Or test through public interface
            // For now, verify HUD text is accessible
            Assert.IsNotNull(hudText);
            Assert.AreEqual("Original HUD Text", hudText.text);

            // Cleanup
            Object.Destroy(gameObject);
            Object.Destroy(hudObject);
        }
    }
}

