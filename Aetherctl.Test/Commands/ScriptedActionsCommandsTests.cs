using System.CommandLine;
using System.Linq;
using System.Text.Json;
using Aetherctl.Commands;
using Xunit;

namespace Aetherctl.Test.Commands
{
    /// <summary>
    /// Tests for the "aetherctl scripted actions" CLI surface. Each test maps to an OpenSpec
    /// requirement under changes/add-aetherctl-scripted-actions/specs/aetherctl.
    /// </summary>
    public class ScriptedActionsCommandsTests
    {
        // Spec: aetherctl / Scripted Action Command
        [Fact]
        public void AgentScript_HasSessionFileAndStopOnError()
        {
            var root = new RootCommand();
            AgentCommands.AddToRoot(root);

            var agentCmd = root.Subcommands.First(c => c.Name == "agent");
            var scriptCmd = agentCmd.Subcommands.FirstOrDefault(c => c.Name == "script");

            Assert.NotNull(scriptCmd);
            Assert.Contains(scriptCmd!.Arguments, a => a.Name == "sessionId");
            Assert.Contains(scriptCmd.Options, o => o.Name == "file");
            Assert.Contains(scriptCmd.Options, o => o.Name == "stop-on-error");
            Assert.True(scriptCmd.Options.First(o => o.Name == "file").IsRequired, "--file should be required");
        }

        // Spec: aetherctl / Multi-Character Scenario Command
        [Fact]
        public void ScenarioRun_HasFileAndFanOutOptions()
        {
            var root = new RootCommand();
            ScenarioCommands.AddToRoot(root);

            var scenarioCmd = root.Subcommands.FirstOrDefault(c => c.Name == "scenario");
            Assert.NotNull(scenarioCmd);

            var runCmd = scenarioCmd!.Subcommands.FirstOrDefault(c => c.Name == "run");
            Assert.NotNull(runCmd);
            Assert.Contains(runCmd!.Arguments, a => a.Name == "file");
            Assert.Contains(runCmd.Options, o => o.Name == "concurrent");
            Assert.Contains(runCmd.Options, o => o.Name == "stop-on-error");
            Assert.Contains(runCmd.Options, o => o.Name == "delay-ms");
        }

        // Spec: aetherctl / Scripted Action Command — action-file parsing + arg normalization
        [Fact]
        public void ParseActions_NormalizesArgsToPrimitives()
        {
            const string json = "[{\"tool\":\"move\",\"args\":{\"direction\":\"forward\",\"distance\":2,\"flag\":true}}]";
            using var doc = JsonDocument.Parse(json);

            var actions = ActionScript.ParseActions(doc.RootElement);

            Assert.Single(actions);
            Assert.Equal("move", actions[0].Tool);
            Assert.Equal("forward", actions[0].Args["direction"]);
            Assert.Equal(2L, actions[0].Args["distance"]);
            Assert.Equal(true, actions[0].Args["flag"]);
        }
    }
}
