using System;
using System.CommandLine;
using System.Threading.Tasks;
using Aetherctl.Orleans;

namespace Aetherctl
{
    public static class Program
    {
        private static async Task<int> Main(string[] args)
        {
            Commands.Common.ProcessExitCode = 0;
            var parserExitCode = await BuildRootCommand().InvokeAsync(args);
            // Command handlers report failures via Common.WriteError (which records
            // an exit code instead of calling Environment.Exit — that killed the
            // process before `await using` cleanup and made in-process tests
            // impossible). Parse errors take precedence.
            return parserExitCode != 0 ? parserExitCode : Commands.Common.ProcessExitCode;
        }

        /// <summary>
        /// Builds the full command tree. Public so tests can invoke commands
        /// in-process (Aetherctl.Test previously only asserted parse results;
        /// real invocation goes through the same object Main uses).
        /// </summary>
        public static RootCommand BuildRootCommand()
        {
            var rootCommand = new RootCommand("Aetherctl - unified cross-platform CLI for Aetherium");

            // Global options
            var jsonOpt = new Option<bool>("--json", "Output results as JSON");
            var verboseOpt = new Option<bool>("--verbose", "Enable verbose output");
            var quietOpt = new Option<bool>("--quiet", "Suppress non-error output");

            // Orleans connectivity options
            var gatewayOpt = new Option<string?>(
                "--gateway",
                () => Environment.GetEnvironmentVariable("ORLEANS_GATEWAY"),
                "Orleans gateway address (default: localhost)")
            {
                IsHidden = true // For now, localhost only
            };
            var clusterOpt = new Option<string?>(
                "--cluster-id",
                () => Environment.GetEnvironmentVariable("ORLEANS_CLUSTER_ID") ?? "dev",
                "Orleans cluster ID")
            {
                IsHidden = true
            };
            var serviceOpt = new Option<string?>(
                "--service-id",
                () => Environment.GetEnvironmentVariable("ORLEANS_SERVICE_ID") ?? "Aetherium",
                "Orleans service ID")
            {
                IsHidden = true
            };

            rootCommand.AddGlobalOption(jsonOpt);
            rootCommand.AddGlobalOption(verboseOpt);
            rootCommand.AddGlobalOption(quietOpt);
            rootCommand.AddGlobalOption(gatewayOpt);
            rootCommand.AddGlobalOption(clusterOpt);
            rootCommand.AddGlobalOption(serviceOpt);

            // Set global options in Common for access by command handlers
            Commands.Common.JsonOption = jsonOpt;
            Commands.Common.VerboseOption = verboseOpt;
            Commands.Common.QuietOption = quietOpt;

            // Add subcommands
            Commands.ServerCommands.AddToRoot(rootCommand);
            Commands.SessionCommands.AddToRoot(rootCommand);
            Commands.AgentCommands.AddToRoot(rootCommand);
            Commands.ToolsCommands.AddToRoot(rootCommand);
            Commands.VisionCommands.AddToRoot(rootCommand);
            Commands.WorldCommands.AddToRoot(rootCommand);
            Commands.GameCommands.AddToRoot(rootCommand);
            Commands.NarrativeCommands.AddToRoot(rootCommand);
            Commands.QuestCommands.AddToRoot(rootCommand);
            Commands.InstanceCommands.AddToRoot(rootCommand);
            Commands.CombatCommands.AddToRoot(rootCommand);
            Commands.PromptsCommands.AddToRoot(rootCommand);
            Commands.WorldGenCommands.AddToRoot(rootCommand);
            Commands.MonitorCommands.AddToRoot(rootCommand);

            return rootCommand;
        }
    }
}
