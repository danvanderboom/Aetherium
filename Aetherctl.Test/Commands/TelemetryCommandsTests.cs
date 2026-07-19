using System.CommandLine;
using System.Linq;
using Aetherctl.Commands;
using Xunit;

namespace Aetherctl.Test.Commands
{
    /// <summary>
    /// Structural tests for the telemetry CLI surface. Each test maps to an OpenSpec requirement
    /// under changes/add-aetherctl-telemetry/specs/aetherctl (Telemetry Inspection Commands).
    /// </summary>
    public class TelemetryCommandsTests
    {
        private static Command TelemetryRoot()
        {
            var root = new RootCommand();
            TelemetryCommands.AddToRoot(root);
            var cmd = root.Subcommands.FirstOrDefault(c => c.Name == "telemetry");
            Assert.NotNull(cmd);
            return cmd!;
        }

        // Spec: aetherctl / Telemetry Inspection Commands — Scenario "List recent snapshots"
        [Fact]
        public void Snapshots_HasAgentIdAndLimit()
        {
            var cmd = TelemetryRoot().Subcommands.FirstOrDefault(c => c.Name == "snapshots");
            Assert.NotNull(cmd);
            Assert.Contains(cmd!.Arguments, a => a.Name == "agentId");
            Assert.Contains(cmd.Options, o => o.Name == "limit");
        }

        // Spec: aetherctl / Telemetry Inspection Commands — Scenario "Show aggregated analysis"
        [Fact]
        public void Analysis_HasAgentId()
        {
            var cmd = TelemetryRoot().Subcommands.FirstOrDefault(c => c.Name == "analysis");
            Assert.NotNull(cmd);
            Assert.Contains(cmd!.Arguments, a => a.Name == "agentId");
        }

        // Spec: aetherctl / Telemetry Inspection Commands — Scenario "List and fetch failed-run replays"
        [Fact]
        public void ReplaysAndReplay_Exist()
        {
            var telemetry = TelemetryRoot();

            var replays = telemetry.Subcommands.FirstOrDefault(c => c.Name == "replays");
            Assert.NotNull(replays);
            Assert.Contains(replays!.Arguments, a => a.Name == "agentId");

            var replay = telemetry.Subcommands.FirstOrDefault(c => c.Name == "replay");
            Assert.NotNull(replay);
            Assert.Contains(replay!.Arguments, a => a.Name == "agentId");
            Assert.Contains(replay.Arguments, a => a.Name == "replayId");
        }

        // Spec: aetherctl / Telemetry Inspection Commands — Scenario "Clear telemetry"
        [Fact]
        public void Clear_HasAgentId()
        {
            var cmd = TelemetryRoot().Subcommands.FirstOrDefault(c => c.Name == "clear");
            Assert.NotNull(cmd);
            Assert.Contains(cmd!.Arguments, a => a.Name == "agentId");
        }
    }
}
