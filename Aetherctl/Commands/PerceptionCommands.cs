using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Aetherctl.Orleans;

namespace Aetherctl.Commands
{
    public static class PerceptionCommands
    {
        public static void AddToRoot(RootCommand root)
        {
            var perceptionCmd = new Command("perception", "Inspect a session's perception");

            var getCmd = new Command("get", "Get the current perception for a session");
            var sessionIdArg = new Argument<string>("sessionId", "Game session ID");
            var absoluteOpt = new Option<bool>("--absolute", "Return true world coordinates instead of relative (0,0,0)");
            getCmd.AddArgument(sessionIdArg);
            getCmd.AddOption(absoluteOpt);
            getCmd.SetHandler(async (InvocationContext ctx) =>
            {
                try
                {
                    var parseResult = ctx.ParseResult;
                    var sessionId = parseResult.GetValueForArgument(sessionIdArg);
                    var absolute = parseResult.GetValueForOption(absoluteOpt);

                    await using var factory = new OrleansClientFactory();
                    await factory.ConnectAsync();
                    var mgmt = factory.GetGameManagement();
                    var json = await mgmt.GetPerceptionAsync(sessionId, absolute);

                    if (string.IsNullOrEmpty(json))
                    {
                        Common.WriteError(parseResult, $"No perception for session '{sessionId}' (unknown session, or operator access disabled for --absolute).");
                        return;
                    }

                    if (Common.IsJsonOutput(parseResult))
                    {
                        // Emit the raw perception object (already valid JSON).
                        Console.WriteLine(json);
                    }
                    else
                    {
                        using var doc = JsonDocument.Parse(json);
                        var r = doc.RootElement;
                        Console.WriteLine($"Perception for session {sessionId}:");
                        if (r.TryGetProperty("PlayerLocation", out var loc))
                        {
                            int gx = loc.TryGetProperty("X", out var xe) ? xe.GetInt32() : 0;
                            int gy = loc.TryGetProperty("Y", out var ye) ? ye.GetInt32() : 0;
                            int gz = loc.TryGetProperty("Z", out var ze) ? ze.GetInt32() : 0;
                            Console.WriteLine($"  Player Location ({(absolute ? "absolute" : "relative")}): ({gx}, {gy}, {gz})");
                        }
                        if (r.TryGetProperty("HeadingDegrees", out var hd))
                            Console.WriteLine($"  Heading: {hd.GetInt32()}°");
                        if (r.TryGetProperty("IsDirectionalVision", out var dv))
                            Console.WriteLine($"  Directional Vision: {(dv.GetBoolean() ? "ON" : "OFF")}");
                        if (r.TryGetProperty("FieldOfViewDegrees", out var fov))
                            Console.WriteLine($"  Field of View: {fov.GetInt32()}°");
                        if (r.TryGetProperty("Visuals", out var visuals) && visuals.ValueKind == JsonValueKind.Object)
                            Console.WriteLine($"  Visible tiles: {visuals.EnumerateObject().Count()}");
                        if (r.TryGetProperty("VisibleItems", out var items) && items.ValueKind == JsonValueKind.Array)
                            Console.WriteLine($"  Visible items: {items.GetArrayLength()}");
                    }
                }
                catch (Exception ex)
                {
                    Common.WriteError(ctx.ParseResult, $"Failed to get perception: {ex.Message}");
                }
            });

            perceptionCmd.AddCommand(getCmd);
            root.AddCommand(perceptionCmd);
        }
    }
}
