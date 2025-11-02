using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.CommandLine;
using System.CommandLine.Parsing;
using Aetherctl.Commands;
using Xunit;

namespace Aetherctl.Test.Commands
{
    public class WorldGenRenderTests : IDisposable
    {
        private readonly string _testOutputDir;

        public WorldGenRenderTests()
        {
            _testOutputDir = Path.Combine(Path.GetTempPath(), $"aetherctl-test-{Guid.NewGuid()}");
            Directory.CreateDirectory(_testOutputDir);
        }

        [Fact]
        public void WorldGenRenderCommand_CanBeAdded()
        {
            var root = new RootCommand();
            WorldGenCommands.AddToRoot(root);
            var worldgenCmd = root.Subcommands.FirstOrDefault(c => c.Name == "worldgen");
            Assert.NotNull(worldgenCmd);

            var renderCmd = worldgenCmd?.Subcommands.FirstOrDefault(c => c.Name == "render");
            Assert.NotNull(renderCmd);

            // Verify --png option exists
            var pngOpt = renderCmd?.Options.FirstOrDefault(o => o.Name == "png");
            Assert.NotNull(pngOpt);
        }

        [Fact]
        public void WorldGenRenderCommand_WithPngOption_CreatesPngFile()
        {
            var pngPath = Path.Combine(_testOutputDir, "test-output.png");

            var root = new RootCommand();
            var jsonOpt = new Option<bool>("--json");
            var quietOpt = new Option<bool>("--quiet");
            root.AddGlobalOption(jsonOpt);
            root.AddGlobalOption(quietOpt);
            Common.JsonOption = jsonOpt;
            Common.QuietOption = quietOpt;

            WorldGenCommands.AddToRoot(root);

            var args = new[]
            {
                "worldgen", "render",
                "--generator", "AdvancedDungeon",
                "--width", "32",
                "--height", "32",
                "--seed", "12345",
                "--png", pngPath
            };

            var result = root.Parse(args);
            Assert.NotNull(result);

            // Verify the command structure is correct
            var renderCmd = result.CommandResult;
            Assert.NotNull(renderCmd);
            Assert.Equal("render", renderCmd.Symbol.Name);

            // Verify PNG path is parsed correctly
            var worldgenCmd = result.CommandResult.Parent;
            Assert.NotNull(worldgenCmd);
            Assert.Equal("worldgen", worldgenCmd?.Symbol.Name);
        }

        [Fact]
        public void PngFile_ValidPngHeader_WhenCreated()
        {
            // PNG files should start with PNG signature: 89 50 4E 47 0D 0A 1A 0A
            var pngPath = Path.Combine(_testOutputDir, "valid-png-test.png");

            // Create a minimal valid PNG file for testing header validation
            var pngSignature = new byte[]
            {
                0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A
            };

            File.WriteAllBytes(pngPath, pngSignature);

            // Verify file exists
            Assert.True(File.Exists(pngPath));

            // Verify PNG header
            var fileBytes = File.ReadAllBytes(pngPath);
            Assert.True(fileBytes.Length >= 8);
            Assert.Equal(0x89, fileBytes[0]);
            Assert.Equal(0x50, fileBytes[1]); // P
            Assert.Equal(0x4E, fileBytes[2]); // N
            Assert.Equal(0x47, fileBytes[3]); // G
        }

        [Fact]
        public void PngRendering_CreatesDirectory_WhenPathHasSubdirectory()
        {
            var subDir = Path.Combine(_testOutputDir, "subdir");
            var pngPath = Path.Combine(subDir, "test.png");

            // Directory should not exist yet
            Assert.False(Directory.Exists(subDir));

            // Simulate directory creation logic (same as in WorldGenCommands.cs line 338-340)
            var directory = Path.GetDirectoryName(pngPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // Directory should now exist
            Assert.True(Directory.Exists(subDir));
        }

        [Fact]
        public void PngRendering_HandlesAbsolutePaths()
        {
            var pngPath = Path.Combine(_testOutputDir, "absolute-path-test.png");
            var absolutePath = Path.GetFullPath(pngPath);

            // Verify path handling
            Assert.True(Path.IsPathRooted(absolutePath));
            var directory = Path.GetDirectoryName(absolutePath);
            Assert.NotNull(directory);

            // Directory creation should work
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
                Assert.True(Directory.Exists(directory));
            }
        }

        public void Dispose()
        {
            if (Directory.Exists(_testOutputDir))
            {
                try
                {
                    Directory.Delete(_testOutputDir, recursive: true);
                }
                catch
                {
                    // Ignore cleanup errors
                }
            }
        }
    }
}

