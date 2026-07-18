using System.CommandLine;
using System.Linq;
using Aetherctl.Commands;
using Xunit;

namespace Aetherctl.Test.Commands
{
    /// <summary>
    /// Structural tests for the "aetherctl headless driving" CLI surface. Each test maps to an
    /// OpenSpec requirement under changes/add-aetherctl-headless-driving/specs/aetherctl.
    /// </summary>
    public class HeadlessDrivingCommandsTests
    {
        // Spec: aetherctl / Headless Session Creation Command
        [Fact]
        public void SessionCreate_HasWorldAtAndProfileOptions()
        {
            var root = new RootCommand();
            SessionCommands.AddToRoot(root);

            var sessionCmd = root.Subcommands.First(c => c.Name == "session");
            var createCmd = sessionCmd.Subcommands.First(c => c.Name == "create");

            Assert.Equal("Create a headless session in a world", createCmd.Description);
            Assert.Contains(createCmd.Options, o => o.Name == "world");
            Assert.Contains(createCmd.Options, o => o.Name == "at");
            Assert.Contains(createCmd.Options, o => o.Name == "profile");

            var worldOpt = createCmd.Options.First(o => o.Name == "world");
            Assert.True(worldOpt.IsRequired, "--world should be required");
        }

        // Spec: aetherctl / Perception Inspection Command
        [Fact]
        public void PerceptionGet_ExistsWithSessionIdAndAbsolute()
        {
            var root = new RootCommand();
            PerceptionCommands.AddToRoot(root);

            var perceptionCmd = root.Subcommands.FirstOrDefault(c => c.Name == "perception");
            Assert.NotNull(perceptionCmd);

            var getCmd = perceptionCmd!.Subcommands.FirstOrDefault(c => c.Name == "get");
            Assert.NotNull(getCmd);
            Assert.Contains(getCmd!.Arguments, a => a.Name == "sessionId");
            Assert.Contains(getCmd.Options, o => o.Name == "absolute");
        }

        // Spec: aetherctl / World Inspection Command
        [Fact]
        public void WorldDump_ExistsWithWorldIdArgument()
        {
            var root = new RootCommand();
            WorldCommands.AddToRoot(root);

            var worldCmd = root.Subcommands.First(c => c.Name == "world");
            var dumpCmd = worldCmd.Subcommands.FirstOrDefault(c => c.Name == "dump");

            Assert.NotNull(dumpCmd);
            Assert.Contains(dumpCmd!.Arguments, a => a.Name == "worldId");
        }

        // Spec: aetherctl / World Edit Commands (change: add-aetherctl-runtime-worldbuilding)
        [Fact]
        public void WorldEdit_ExistsWithWorldIdToolIdAndArgs()
        {
            var root = new RootCommand();
            WorldCommands.AddToRoot(root);

            var worldCmd = root.Subcommands.First(c => c.Name == "world");
            var editCmd = worldCmd.Subcommands.FirstOrDefault(c => c.Name == "edit");

            Assert.NotNull(editCmd);
            Assert.Contains(editCmd!.Arguments, a => a.Name == "worldId");
            Assert.Contains(editCmd.Arguments, a => a.Name == "toolId");
            Assert.Contains(editCmd.Options, o => o.Name == "args");
        }

        // Spec: aetherctl / Memory Inspection Command (change: add-character-memory)
        [Fact]
        public void MemoryGet_ExistsWithSessionIdArgument()
        {
            var root = new RootCommand();
            MemoryCommands.AddToRoot(root);

            var memoryCmd = root.Subcommands.FirstOrDefault(c => c.Name == "memory");
            Assert.NotNull(memoryCmd);

            var getCmd = memoryCmd!.Subcommands.FirstOrDefault(c => c.Name == "get");
            Assert.NotNull(getCmd);
            Assert.Contains(getCmd!.Arguments, a => a.Name == "sessionId");
        }

        // Spec: character-memory / Memory Permanence + Stability display (change: add-memory-dynamics)
        [Fact]
        public void MemoryGet_FormatsDurabilitySuffix()
        {
            // Permanent wins over stability; grown stability shows the memory's own half-life in hours;
            // a never-reinforced (stability 0) memory has no suffix.
            Assert.Equal(" [permanent]", MemoryCommands.FormatDurability(permanent: true, stabilitySeconds: 7200));
            Assert.Equal(" stab=2.0h", MemoryCommands.FormatDurability(permanent: false, stabilitySeconds: 7200));
            Assert.Equal(string.Empty, MemoryCommands.FormatDurability(permanent: false, stabilitySeconds: 0));
        }

        // Spec: aetherctl / World Edit Commands — spawn convenience (change: add-aetherctl-runtime-worldbuilding)
        [Fact]
        public void WorldSpawn_ExistsWithRequiredTypeAndAt()
        {
            var root = new RootCommand();
            WorldCommands.AddToRoot(root);

            var worldCmd = root.Subcommands.First(c => c.Name == "world");
            var spawnCmd = worldCmd.Subcommands.FirstOrDefault(c => c.Name == "spawn");

            Assert.NotNull(spawnCmd);
            Assert.Contains(spawnCmd!.Arguments, a => a.Name == "worldId");
            Assert.True(spawnCmd.Options.First(o => o.Name == "type").IsRequired);
            Assert.True(spawnCmd.Options.First(o => o.Name == "at").IsRequired);
        }
    }
}
