using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Aetherium.Server.Agents;
using Aetherium.Server.Agents.Tools;

namespace Aetherium.Test
{
    /// <summary>
    /// P3-5: agent LLM decisions are driven by editable prompt templates via PromptRegistry, with a
    /// safe fallback to the built-in default when no registry/template is available. Verified at the
    /// prompt-composition level (no live LLM needed).
    /// </summary>
    [TestFixture]
    public class AgentPromptTemplateTests
    {
        private string _tempDir = null!;

        [SetUp]
        public void SetUp()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "aeth-prompts-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempDir);
        }

        [TearDown]
        public void TearDown()
        {
            try { Directory.Delete(_tempDir, recursive: true); } catch { /* best effort */ }
        }

        // ---- PromptRegistry rendering --------------------------------------------------------

        [Test]
        public void Substitute_ReplacesPlaceholders_CaseInsensitive_WithWhitespace()
        {
            var vars = new Dictionary<string, string> { ["goal"] = "escape", ["name"] = "Bob" };
            var result = PromptRegistry.Substitute("Hi {{name}}, your goal is {{ GOAL }}.", vars);
            Assert.That(result, Is.EqualTo("Hi Bob, your goal is escape."));
        }

        [Test]
        public void Substitute_LeavesUnknownPlaceholdersIntact()
        {
            var vars = new Dictionary<string, string> { ["goal"] = "escape" };
            var result = PromptRegistry.Substitute("{{goal}} then {{unknown}}", vars);
            Assert.That(result, Is.EqualTo("escape then {{unknown}}"));
        }

        [Test]
        public void Render_ReturnsNull_WhenTemplateMissing()
        {
            var registry = new PromptRegistry(_tempDir);
            registry.LoadTemplates();
            Assert.That(registry.Render("does_not_exist", new Dictionary<string, string>()), Is.Null);
        }

        [Test]
        public void Render_SubstitutesGoal_FromTemplateFile()
        {
            var registry = new PromptRegistry(_tempDir);
            registry.SetTemplate("agent_decision", "SYSTEM: pursue {{goal}} now.");
            var rendered = registry.Render("agent_decision", new Dictionary<string, string> { ["goal"] = "find the key" });
            Assert.That(rendered, Is.EqualTo("SYSTEM: pursue find the key now."));
        }

        // ---- Adapter prompt composition ------------------------------------------------------

        [Test]
        public void BuildSystemPrompt_UsesRenderedTemplate_WhenRegistryProvided()
        {
            var registry = new PromptRegistry(_tempDir);
            registry.SetTemplate("agent_decision", "MARKER persona. Goal: {{goal}}");
            var adapter = new MicrosoftAgentAdapter(promptRegistry: registry, systemTemplateName: "agent_decision", goal: "open the vault");

            var system = adapter.BuildSystemPrompt();

            Assert.That(system, Does.Contain("MARKER persona"));
            Assert.That(system, Does.Contain("open the vault"));
        }

        [Test]
        public void BuildSystemPrompt_FallsBackToDefault_WhenNoRegistry()
        {
            var adapter = new MicrosoftAgentAdapter();
            var system = adapter.BuildSystemPrompt();
            Assert.That(system, Does.Contain("NPC navigator"));
        }

        [Test]
        public void BuildSystemPrompt_FallsBackToDefault_WhenTemplateMissing()
        {
            var registry = new PromptRegistry(_tempDir);
            registry.LoadTemplates(); // empty dir → no templates
            var adapter = new MicrosoftAgentAdapter(promptRegistry: registry, systemTemplateName: "agent_decision");
            var system = adapter.BuildSystemPrompt();
            Assert.That(system, Does.Contain("NPC navigator"));
        }

        [Test]
        public void BuildUserPrompt_IncludesPerception_Goal_AndActions()
        {
            var adapter = new MicrosoftAgentAdapter(goal: "reach the exit");
            var user = adapter.BuildUserPrompt("{\"visible\":[]}", new List<IAgentTool>());

            Assert.That(user, Does.Contain("Perception:"));
            Assert.That(user, Does.Contain("{\"visible\":[]}"));
            Assert.That(user, Does.Contain("Goal: reach the exit"));
            Assert.That(user, Does.Contain("Respond with strict JSON only."));
        }
    }
}
