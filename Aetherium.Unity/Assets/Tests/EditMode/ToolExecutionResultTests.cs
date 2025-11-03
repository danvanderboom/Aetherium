using System.Collections.Generic;
using Aetherium.Unity.Model;
using NUnit.Framework;

namespace Aetherium.Unity.Tests
{
    public class ToolExecutionResultTests
    {
        [Test]
        public void ToolExecutionResultDto_SuccessResult_InitializesCorrectly()
        {
            // Arrange & Act
            var result = new ToolExecutionResultDto
            {
                Success = true,
                Message = "Tool executed successfully",
                Data = null
            };

            // Assert
            Assert.IsTrue(result.Success);
            Assert.AreEqual("Tool executed successfully", result.Message);
            Assert.IsNull(result.Data);
        }

        [Test]
        public void ToolExecutionResultDto_ErrorResult_InitializesCorrectly()
        {
            // Arrange & Act
            var result = new ToolExecutionResultDto
            {
                Success = false,
                Message = "Tool execution failed",
                Data = null
            };

            // Assert
            Assert.IsFalse(result.Success);
            Assert.AreEqual("Tool execution failed", result.Message);
        }

        [Test]
        public void ToolExecutionResultDto_WithOptionsData_StoresCorrectly()
        {
            // Arrange & Act
            var options = new List<object>
            {
                new Dictionary<string, object>
                {
                    { "usageId", "use1" },
                    { "label", "Use" },
                    { "description", "Use the item" }
                },
                new Dictionary<string, object>
                {
                    { "usageId", "inspect1" },
                    { "label", "Inspect" },
                    { "description", "Inspect the item" }
                }
            };

            var result = new ToolExecutionResultDto
            {
                Success = true,
                Message = "Multiple options available",
                Data = new Dictionary<string, object> { { "options", options } }
            };

            // Assert
            Assert.IsTrue(result.Success);
            Assert.IsNotNull(result.Data);
            Assert.IsTrue(result.Data.ContainsKey("options"));
            
            var resultOptions = result.Data["options"] as List<object>;
            Assert.IsNotNull(resultOptions);
            Assert.AreEqual(2, resultOptions.Count);
        }

        [Test]
        public void UsageOptionDto_AllProperties_SetCorrectly()
        {
            // Arrange & Act
            var option = new UsageOptionDto
            {
                UsageId = "test-usage-id",
                Label = "Test Option",
                Description = "This is a test option for multi-use tools"
            };

            // Assert
            Assert.AreEqual("test-usage-id", option.UsageId);
            Assert.AreEqual("Test Option", option.Label);
            Assert.AreEqual("This is a test option for multi-use tools", option.Description);
        }
    }
}

