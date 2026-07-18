using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using System.Linq;
using System.Threading.Tasks;
using Aetherctl.Orleans;

namespace Aetherctl.Commands
{
    public static class SessionCommands
    {
        public static void AddToRoot(RootCommand root)
        {
            var sessionCmd = new Command("session", "Session management");

            var listCmd = new Command("list", "List all active game sessions");
            listCmd.SetHandler(async (InvocationContext ctx) =>
            {
                try
                {
                    var parseResult = ctx.ParseResult;
                    await using var factory = new OrleansClientFactory();
                    var client = await factory.ConnectAsync();
                    var mgmt = factory.GetGameManagement();
                    var sessions = await mgmt.ListSessionsAsync();

                    if (Common.IsJsonOutput(parseResult))
                    {
                        var output = new
                        {
                            success = true,
                            count = sessions?.Count ?? 0,
                            sessions = sessions?.Select(s => new
                            {
                                sessionId = s.SessionId,
                                connectionId = s.ConnectionId,
                                directionalVision = s.DirectionalVisionMode,
                                fieldOfView = s.FieldOfViewDegrees,
                                connectedAt = s.ConnectedAt
                            }).ToArray() ?? Array.Empty<object>()
                        };
                        Common.WriteOutput(parseResult, output);
                    }
                    else
                    {
                        if (sessions == null || sessions.Count == 0)
                        {
                            Console.WriteLine("No active sessions");
                            return;
                        }

                        Console.WriteLine($"Active sessions ({sessions.Count}):");
                        foreach (var session in sessions)
                        {
                            Console.WriteLine($"  Session ID: {session.SessionId}");
                            Console.WriteLine($"    Connection ID: {session.ConnectionId}");
                            Console.WriteLine($"    Directional Vision: {(session.DirectionalVisionMode ? "ON" : "OFF")}");
                            Console.WriteLine($"    FOV: {session.FieldOfViewDegrees}°");
                            Console.WriteLine($"    Connected at: {session.ConnectedAt:yyyy-MM-dd HH:mm:ss}");
                            Console.WriteLine();
                        }
                    }
                }
                catch (Exception ex)
                {
                    Common.WriteError(ctx.ParseResult, $"Failed to list sessions: {ex.Message}");
                }
            });

            var closeCmd = new Command("close", "Terminate a session by ID");
            var sessionIdArg = new Argument<string>(name: "sessionId", description: "Session ID to terminate");
            closeCmd.AddArgument(sessionIdArg);
            closeCmd.SetHandler(async (InvocationContext ctx) =>
            {
                try
                {
                    var parseResult = ctx.ParseResult;
                    var sessionId = parseResult.GetValueForArgument(sessionIdArg);
                    await using var factory = new OrleansClientFactory();
                    var client = await factory.ConnectAsync();
                    var mgmt = factory.GetGameManagement();
                    var result = await mgmt.TerminateSessionAsync(sessionId);

                    if (Common.IsJsonOutput(parseResult))
                    {
                        Common.WriteOutput(parseResult, new { success = result.Success, message = result.Message });
                    }
                    else
                    {
                        Console.WriteLine(result.Success ? "Session terminated" : $"Failed to terminate: {result.Message}");
                    }

                    if (!result.Success)
                    {
                        Common.ProcessExitCode = 1;
                    }
                }
                catch (Exception ex)
                {
                    Common.WriteError(ctx.ParseResult, $"Failed to terminate session: {ex.Message}");
                }
            });

            // Create a headless session (no interactive client) in an existing world.
            var createCmd = new Command("create", "Create a headless session in a world");
            var createWorldOpt = new Option<string>("--world", "World ID to create the headless session in") { IsRequired = true };
            var createAtOpt = new Option<string?>("--at", () => null, "Optional start location as x,y or x,y,z");
            var createProfileOpt = new Option<string?>("--profile", () => null, "Optional tool profile (reserved for future use)");
            createCmd.AddOption(createWorldOpt);
            createCmd.AddOption(createAtOpt);
            createCmd.AddOption(createProfileOpt);
            createCmd.SetHandler(async (InvocationContext ctx) =>
            {
                try
                {
                    var parseResult = ctx.ParseResult;
                    var worldId = parseResult.GetValueForOption(createWorldOpt);
                    var at = parseResult.GetValueForOption(createAtOpt);
                    var profile = parseResult.GetValueForOption(createProfileOpt);

                    int? x = null, y = null, z = null;
                    if (!string.IsNullOrWhiteSpace(at))
                    {
                        var parts = at.Split(',');
                        if (parts.Length < 2
                            || !int.TryParse(parts[0].Trim(), out var px)
                            || !int.TryParse(parts[1].Trim(), out var py))
                        {
                            Common.WriteError(parseResult, "Invalid --at value; expected 'x,y' or 'x,y,z'");
                            return;
                        }
                        x = px;
                        y = py;
                        if (parts.Length >= 3 && int.TryParse(parts[2].Trim(), out var pz))
                            z = pz;
                    }

                    await using var factory = new OrleansClientFactory();
                    await factory.ConnectAsync();
                    var mgmt = factory.GetGameManagement();
                    var result = await mgmt.CreateHeadlessSessionAsync(worldId!, x, y, z, profile);

                    if (result.Success)
                    {
                        if (Common.IsJsonOutput(parseResult))
                        {
                            Common.WriteOutput(parseResult, new
                            {
                                success = true,
                                sessionId = result.SessionId,
                                worldId = result.WorldId,
                                connectionId = result.ConnectionId
                            });
                        }
                        else
                        {
                            Console.WriteLine($"✓ Created headless session: {result.SessionId}");
                            Console.WriteLine($"  World: {result.WorldId}");
                        }
                    }
                    else
                    {
                        if (Common.IsJsonOutput(parseResult))
                            Common.WriteOutput(parseResult, new { success = false, error = result.Message });
                        else
                            Console.Error.WriteLine($"Failed to create session: {result.Message}");
                        Common.ProcessExitCode = 1;
                    }
                }
                catch (Exception ex)
                {
                    Common.WriteError(ctx.ParseResult, $"Failed to create session: {ex.Message}");
                }
            });

            sessionCmd.AddCommand(listCmd);
            sessionCmd.AddCommand(closeCmd);
            sessionCmd.AddCommand(createCmd);
            root.AddCommand(sessionCmd);
        }
    }
}
