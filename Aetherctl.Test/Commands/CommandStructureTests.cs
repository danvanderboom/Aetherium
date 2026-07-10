using System.CommandLine;
using System.Linq;
using Aetherctl.Commands;
using Xunit;

namespace Aetherctl.Test.Commands
{
    public class CommandStructureTests
    {
        [Fact]
        public void RootCommand_CanBeCreated()
        {
            var root = new RootCommand("Aetherctl - unified cross-platform CLI for Aetherium");
            Assert.NotNull(root);
            Assert.Equal("Aetherctl - unified cross-platform CLI for Aetherium", root.Description);
        }

        [Fact]
        public void SessionCommand_CanBeAdded()
        {
            var root = new RootCommand();
            SessionCommands.AddToRoot(root);
            var sessionCmd = root.Subcommands.FirstOrDefault(c => c.Name == "session");
            Assert.NotNull(sessionCmd);
        }

        [Fact]
        public void AgentCommand_CanBeAdded()
        {
            var root = new RootCommand();
            AgentCommands.AddToRoot(root);
            var agentCmd = root.Subcommands.FirstOrDefault(c => c.Name == "agent");
            Assert.NotNull(agentCmd);
        }

        [Fact]
        public void ToolsCommand_CanBeAdded()
        {
            var root = new RootCommand();
            ToolsCommands.AddToRoot(root);
            var toolsCmd = root.Subcommands.FirstOrDefault(c => c.Name == "tools");
            Assert.NotNull(toolsCmd);
        }

        [Fact]
        public void VisionCommand_CanBeAdded()
        {
            var root = new RootCommand();
            VisionCommands.AddToRoot(root);
            var visionCmd = root.Subcommands.FirstOrDefault(c => c.Name == "vision");
            Assert.NotNull(visionCmd);
        }

        [Fact]
        public void GameCommand_CanBeAdded()
        {
            var root = new RootCommand();
            GameCommands.AddToRoot(root);
            var gameCmd = root.Subcommands.FirstOrDefault(c => c.Name == "game");
            Assert.NotNull(gameCmd);
            Assert.Equal(new[] { "create", "instances", "list" },
                gameCmd!.Subcommands.Select(c => c.Name).OrderBy(n => n).ToArray());
        }

        [Fact]
        public void WorldCommand_CanBeAdded()
        {
            var root = new RootCommand();
            WorldCommands.AddToRoot(root);
            var worldCmd = root.Subcommands.FirstOrDefault(c => c.Name == "world");
            Assert.NotNull(worldCmd);
        }

        [Fact]
        public void NarrativeCommand_CanBeAdded()
        {
            var root = new RootCommand();
            NarrativeCommands.AddToRoot(root);
            var narrativeCmd = root.Subcommands.FirstOrDefault(c => c.Name == "narrative");
            Assert.NotNull(narrativeCmd);
        }

        [Fact]
        public void PromptsCommand_CanBeAdded()
        {
            var root = new RootCommand();
            PromptsCommands.AddToRoot(root);
            var promptsCmd = root.Subcommands.FirstOrDefault(c => c.Name == "prompts");
            Assert.NotNull(promptsCmd);
        }

        [Fact]
        public void WorldGenCommand_CanBeAdded()
        {
            var root = new RootCommand();
            WorldGenCommands.AddToRoot(root);
            var worldgenCmd = root.Subcommands.FirstOrDefault(c => c.Name == "worldgen");
            Assert.NotNull(worldgenCmd);
        }

        [Fact]
        public void MonitorCommand_CanBeAdded()
        {
            var root = new RootCommand();
            MonitorCommands.AddToRoot(root);
            var monitorCmd = root.Subcommands.FirstOrDefault(c => c.Name == "monitor");
            Assert.NotNull(monitorCmd);
        }

        [Fact]
        public void ServerCommand_CanBeAdded()
        {
            var root = new RootCommand();
            ServerCommands.AddToRoot(root);
            var serverCmd = root.Subcommands.FirstOrDefault(c => c.Name == "server");
            Assert.NotNull(serverCmd);
        }

        [Fact]
        public void AllCommands_HaveSubcommands()
        {
            var root = new RootCommand();
            SessionCommands.AddToRoot(root);
            AgentCommands.AddToRoot(root);
            ToolsCommands.AddToRoot(root);
            VisionCommands.AddToRoot(root);
            WorldCommands.AddToRoot(root);
            NarrativeCommands.AddToRoot(root);
            PromptsCommands.AddToRoot(root);
            WorldGenCommands.AddToRoot(root);
            MonitorCommands.AddToRoot(root);
            ServerCommands.AddToRoot(root);

            var sessionCmd = root.Subcommands.FirstOrDefault(c => c.Name == "session");
            Assert.True(sessionCmd?.Subcommands.Count > 0, "Session command should have subcommands");

            var agentCmd = root.Subcommands.FirstOrDefault(c => c.Name == "agent");
            Assert.True(agentCmd?.Subcommands.Count > 0, "Agent command should have subcommands");

            var toolsCmd = root.Subcommands.FirstOrDefault(c => c.Name == "tools");
            Assert.True(toolsCmd?.Subcommands.Count > 0, "Tools command should have subcommands");

            var visionCmd = root.Subcommands.FirstOrDefault(c => c.Name == "vision");
            Assert.True(visionCmd?.Subcommands.Count > 0, "Vision command should have subcommands");

            var worldCmd = root.Subcommands.FirstOrDefault(c => c.Name == "world");
            Assert.True(worldCmd?.Subcommands.Count > 0, "World command should have subcommands");

            var narrativeCmd = root.Subcommands.FirstOrDefault(c => c.Name == "narrative");
            Assert.True(narrativeCmd?.Subcommands.Count > 0, "Narrative command should have subcommands");

            var promptsCmd = root.Subcommands.FirstOrDefault(c => c.Name == "prompts");
            Assert.True(promptsCmd?.Subcommands.Count > 0, "Prompts command should have subcommands");

            var worldgenCmd = root.Subcommands.FirstOrDefault(c => c.Name == "worldgen");
            Assert.True(worldgenCmd?.Subcommands.Count > 0, "WorldGen command should have subcommands");

            var monitorCmd = root.Subcommands.FirstOrDefault(c => c.Name == "monitor");
            Assert.NotNull(monitorCmd);

            var serverCmd = root.Subcommands.FirstOrDefault(c => c.Name == "server");
            Assert.True(serverCmd?.Subcommands.Count > 0, "Server command should have subcommands");
        }
    }
}

