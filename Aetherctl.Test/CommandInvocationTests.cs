using System;
using System.CommandLine;
using System.CommandLine.Parsing;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Aetherctl;
using Aetherctl.Commands;
using Xunit;

namespace Aetherctl.Test
{
    /// <summary>
    /// Real command-invocation tests (P2-5): unlike the parser-structure tests,
    /// these run the actual handlers in-process via Program.BuildRootCommand and
    /// assert stdout/stderr content and exit codes. Only offline-capable commands
    /// are exercised (worldgen generation, config-file-backed server management,
    /// help/parse errors) — nothing here needs a running server.
    ///
    /// Config-touching tests isolate themselves with AETHERCTL_CONFIG_DIR so they
    /// never read or write the developer's real config.
    /// </summary>
    [Collection("cli-invocation")] // serialize: tests redirect global Console streams
    public class CommandInvocationTests : IDisposable
    {
        private readonly string _tempConfigDir;

        public CommandInvocationTests()
        {
            _tempConfigDir = Path.Combine(Path.GetTempPath(), $"aetherctl-test-{Guid.NewGuid():N}");
            Directory.CreateDirectory(_tempConfigDir);
            Environment.SetEnvironmentVariable("AETHERCTL_CONFIG_DIR", _tempConfigDir);
        }

        public void Dispose()
        {
            Environment.SetEnvironmentVariable("AETHERCTL_CONFIG_DIR", null);
            try { Directory.Delete(_tempConfigDir, recursive: true); } catch { /* best effort */ }
        }

        private static async Task<(int ExitCode, string StdOut, string StdErr)> InvokeAsync(params string[] args)
        {
            var originalOut = System.Console.Out;
            var originalErr = System.Console.Error;
            using var stdout = new StringWriter();
            using var stderr = new StringWriter();

            try
            {
                System.Console.SetOut(stdout);
                System.Console.SetError(stderr);

                Common.ProcessExitCode = 0;
                var root = Program.BuildRootCommand();
                var parserExit = await root.InvokeAsync(args);
                var exit = parserExit != 0 ? parserExit : Common.ProcessExitCode;

                return (exit, stdout.ToString(), stderr.ToString());
            }
            finally
            {
                System.Console.SetOut(originalOut);
                System.Console.SetError(originalErr);
            }
        }

        // ---------- worldgen (fully offline, deterministic) ----------

        [Fact]
        public async Task WorldgenGenerate_WithSeed_Succeeds_And_Reports_Seed()
        {
            var (exit, stdout, _) = await InvokeAsync(
                "worldgen", "generate", "--seed", "1337", "--width", "32", "--height", "32");

            Assert.Equal(0, exit);
            Assert.Contains("1337", stdout);
        }

        [Fact]
        public async Task WorldgenGenerate_Json_Emits_Parseable_Payload_With_Seed()
        {
            var (exit, stdout, _) = await InvokeAsync(
                "worldgen", "generate", "--seed", "4242", "--width", "32", "--height", "32", "--json");

            Assert.Equal(0, exit);
            using var json = JsonDocument.Parse(stdout);
            Assert.Equal(JsonValueKind.Object, json.RootElement.ValueKind);
            Assert.Equal(4242, json.RootElement.GetProperty("seed").GetInt32());
        }

        // ---------- server config (isolated via AETHERCTL_CONFIG_DIR) ----------

        [Fact]
        public async Task ServerList_WithNoConfig_Says_No_Servers()
        {
            var (exit, stdout, _) = await InvokeAsync("server", "list");

            Assert.Equal(0, exit);
            Assert.Contains("No servers configured", stdout);
        }

        [Fact]
        public async Task ServerAdd_Then_List_Then_Remove_Round_Trips_Through_Config_File()
        {
            var (addExit, addOut, _) = await InvokeAsync(
                "server", "add", "test-server",
                "--url", "http://localhost:5000",
                "--tenant", "test.onmicrosoft.com",
                "--policy", "B2C_1_susi",
                "--client-id", "client-123",
                "--scope", "api://client-123/.default");
            Assert.Equal(0, addExit);
            Assert.Contains("test-server", addOut);
            Assert.True(File.Exists(Path.Combine(_tempConfigDir, "config.json")),
                "server add must persist to the overridden config dir");

            var (listExit, listOut, _) = await InvokeAsync("server", "list");
            Assert.Equal(0, listExit);
            Assert.Contains("test-server", listOut);

            var (connectExit, connectOut, _) = await InvokeAsync("server", "connect", "test-server");
            Assert.Equal(0, connectExit);
            Assert.Contains("test-server", connectOut);

            var (removeExit, _, _) = await InvokeAsync("server", "remove", "test-server");
            Assert.Equal(0, removeExit);

            var (finalExit, finalOut, _) = await InvokeAsync("server", "list");
            Assert.Equal(0, finalExit);
            Assert.Contains("No servers configured", finalOut);
        }

        [Fact]
        public async Task ServerConnect_UnknownServer_Fails_With_NonZero_Exit_And_Error_Text()
        {
            var (exit, _, stderr) = await InvokeAsync("server", "connect", "does-not-exist");

            // This path used to Environment.Exit(1) — untestable and skipping
            // disposal. It must now report and return.
            Assert.NotEqual(0, exit);
            Assert.Contains("not found", stderr);
        }

        // ---------- parser-level behavior ----------

        [Fact]
        public async Task UnknownCommand_Returns_NonZero_Exit()
        {
            var (exit, _, _) = await InvokeAsync("frobnicate");
            Assert.NotEqual(0, exit);
        }

        [Fact]
        public async Task Help_Succeeds_And_Lists_TopLevel_Commands()
        {
            var (exit, stdout, _) = await InvokeAsync("--help");

            Assert.Equal(0, exit);
            Assert.Contains("worldgen", stdout);
            Assert.Contains("server", stdout);
        }
    }
}
