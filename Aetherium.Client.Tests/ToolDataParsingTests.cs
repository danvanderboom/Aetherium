using System.Collections.Generic;
using System.Text.Json;
using NUnit.Framework;
using Aetherium.Client;

namespace Aetherium.Client.Tests
{
    /// <summary>
    /// ExecuteTool's Data dictionary is Dictionary&lt;string, object&gt;, so over the wire its
    /// values arrive as JsonElement — these tests pin the readers ToolClient uses to type
    /// attack feedback (damage/remainingHealth/defeated) and use-disambiguation options.
    /// </summary>
    [TestFixture]
    public class ToolDataParsingTests
    {
        /// <summary>Simulates the wire: a server-built Data dictionary after JSON round-trip
        /// (every value becomes a JsonElement, exactly as the SignalR client yields them).</summary>
        private static Dictionary<string, object> OverTheWire(Dictionary<string, object> data)
        {
            var json = JsonSerializer.Serialize(data);
            return JsonSerializer.Deserialize<Dictionary<string, object>>(json)!;
        }

        [Test]
        public void AttackFeedback_ParsesFromJsonElements()
        {
            var data = OverTheWire(new Dictionary<string, object>
            {
                ["targetId"] = "npc-1",
                ["damage"] = 7,
                ["remainingHealth"] = 13,
                ["defeated"] = false,
            });

            Assert.That(ToolClient.GetString(data, "targetId"), Is.EqualTo("npc-1"));
            Assert.That(ToolClient.GetInt(data, "damage"), Is.EqualTo(7));
            Assert.That(ToolClient.GetInt(data, "remainingHealth"), Is.EqualTo(13));
            Assert.That(ToolClient.GetBool(data, "defeated"), Is.False);
        }

        [Test]
        public void MissingKeys_AndNullData_ReadAsNull()
        {
            Assert.That(ToolClient.GetInt(null, "damage"), Is.Null);
            Assert.That(ToolClient.GetBool(new Dictionary<string, object>(), "defeated"), Is.Null);
            Assert.That(ToolClient.GetString(null, "targetId"), Is.Null);
        }

        [Test]
        public void PlainCliValues_ParseToo()
        {
            // Values that never crossed JSON (in-proc callers, tests) stay primitives.
            var data = new Dictionary<string, object> { ["damage"] = 9, ["defeated"] = true, ["targetId"] = "x" };
            Assert.That(ToolClient.GetInt(data, "damage"), Is.EqualTo(9));
            Assert.That(ToolClient.GetBool(data, "defeated"), Is.True);
            Assert.That(ToolClient.GetString(data, "targetId"), Is.EqualTo("x"));
        }

        [Test]
        public void UsageOptions_ParseFromTheServersAnonymousShape()
        {
            var data = OverTheWire(new Dictionary<string, object>
            {
                ["options"] = new[]
                {
                    new Dictionary<string, object> { ["usageId"] = "cut", ["label"] = "Cut open", ["description"] = "Force the door" },
                    new Dictionary<string, object> { ["usageId"] = "pry", ["label"] = "Pry", ["description"] = "Lever it" },
                },
            });

            var options = ToolClient.ParseUsageOptions(data);
            Assert.That(options, Has.Count.EqualTo(2));
            Assert.That(options[0].UsageId, Is.EqualTo("cut"));
            Assert.That(options[0].Label, Is.EqualTo("Cut open"));
            Assert.That(options[1].Description, Is.EqualTo("Lever it"));
        }

        [Test]
        public void UsageOptions_EmptyWhenAbsentOrMalformed()
        {
            Assert.That(ToolClient.ParseUsageOptions(null), Is.Empty);
            Assert.That(ToolClient.ParseUsageOptions(new Dictionary<string, object>()), Is.Empty);
            Assert.That(ToolClient.ParseUsageOptions(OverTheWire(new Dictionary<string, object>
            {
                ["options"] = "not-an-array",
            })), Is.Empty);
        }
    }
}
