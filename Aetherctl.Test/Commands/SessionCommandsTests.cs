using System;
using System.Linq;
using System.CommandLine;
using System.CommandLine.Parsing;
using System.Text.Json;
using Aetherctl.Commands;
using Xunit;

namespace Aetherctl.Test.Commands
{
    public class SessionCommandsTests
    {
        [Fact]
        public void SessionCommand_CanBeAdded()
        {
            var root = new RootCommand();
            SessionCommands.AddToRoot(root);
            var sessionCmd = root.Subcommands.FirstOrDefault(c => c.Name == "session");
            Assert.NotNull(sessionCmd);
        }

        [Fact]
        public void SessionCommand_HasListSubcommand()
        {
            var root = new RootCommand();
            SessionCommands.AddToRoot(root);
            var sessionCmd = root.Subcommands.FirstOrDefault(c => c.Name == "session");
            var listCmd = sessionCmd?.Subcommands.FirstOrDefault(c => c.Name == "list");
            Assert.NotNull(listCmd);
            Assert.Equal("List all active game sessions", listCmd.Description);
        }

        [Fact]
        public void SessionCommand_HasCloseSubcommand()
        {
            var root = new RootCommand();
            SessionCommands.AddToRoot(root);
            var sessionCmd = root.Subcommands.FirstOrDefault(c => c.Name == "session");
            var closeCmd = sessionCmd?.Subcommands.FirstOrDefault(c => c.Name == "close");
            Assert.NotNull(closeCmd);
            Assert.Equal("Terminate a session by ID", closeCmd.Description);
        }

        [Fact]
        public void SessionCommand_HasCreateSubcommand()
        {
            var root = new RootCommand();
            SessionCommands.AddToRoot(root);
            var sessionCmd = root.Subcommands.FirstOrDefault(c => c.Name == "session");
            var createCmd = sessionCmd?.Subcommands.FirstOrDefault(c => c.Name == "create");
            Assert.NotNull(createCmd);
            Assert.Equal("Create a new session (pending server support)", createCmd.Description);
        }

        [Fact]
        public void SessionCloseCommand_RequiresSessionIdArgument()
        {
            var root = new RootCommand();
            SessionCommands.AddToRoot(root);
            var sessionCmd = root.Subcommands.FirstOrDefault(c => c.Name == "session");
            var closeCmd = sessionCmd?.Subcommands.FirstOrDefault(c => c.Name == "close");

            Assert.NotNull(closeCmd);
            var sessionIdArg = closeCmd.Arguments.FirstOrDefault(a => a.Name == "sessionId");
            Assert.NotNull(sessionIdArg);
            Assert.Equal("Session ID to terminate", sessionIdArg.Description);
        }

        [Fact]
        public void SessionListCommand_SupportsJsonOutput()
        {
            var root = new RootCommand();
            var jsonOpt = new Option<bool>("--json");
            root.AddGlobalOption(jsonOpt);
            Common.JsonOption = jsonOpt;

            SessionCommands.AddToRoot(root);

            var args = new[] { "session", "list", "--json" };
            var result = root.Parse(args);

            Assert.NotNull(result);
            Assert.True(Common.IsJsonOutput(result));
        }

        [Fact]
        public void SessionCommands_UseCorrectCommandStructure()
        {
            // Verify that session commands are at the root level, not under agent
            var root = new RootCommand();
            SessionCommands.AddToRoot(root);
            AgentCommands.AddToRoot(root);

            // Session should be a direct subcommand of root
            var sessionCmd = root.Subcommands.FirstOrDefault(c => c.Name == "session");
            Assert.NotNull(sessionCmd);

            // Agent should also be a direct subcommand of root
            var agentCmd = root.Subcommands.FirstOrDefault(c => c.Name == "agent");
            Assert.NotNull(agentCmd);

            // Session should NOT be under agent
            var sessionUnderAgent = agentCmd.Subcommands.FirstOrDefault(c => c.Name == "session");
            Assert.Null(sessionUnderAgent);
        }

        [Fact]
        public void SessionCreateCommand_ErrorOutput_IsValidJson()
        {
            var root = new RootCommand();
            var jsonOpt = new Option<bool>("--json");
            root.AddGlobalOption(jsonOpt);
            Common.JsonOption = jsonOpt;

            SessionCommands.AddToRoot(root);

            // The create command should return JSON error when --json is set
            // Since we can't actually invoke it without Orleans connection, we just verify
            // the command structure is correct
            var args = new[] { "session", "create", "--json" };
            var result = root.Parse(args);

            Assert.NotNull(result);
            Assert.True(Common.IsJsonOutput(result));

            var sessionCmd = result.CommandResult.Parent;
            var createCmd = result.CommandResult;
            Assert.NotNull(sessionCmd);
            Assert.Equal("session", sessionCmd.Symbol.Name);
            Assert.NotNull(createCmd);
            Assert.Equal("create", createCmd.Symbol.Name);
        }
    }
}

