using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using System.Threading.Tasks;
using Aetherctl.Orleans;

namespace Aetherctl.Commands
{
    public static class VisionCommands
    {
        public static void AddToRoot(RootCommand root)
        {
            var visionCmd = new Command("vision", "Control vision modes for game sessions");

            var directionalCmd = new Command("directional", "Enable directional vision mode for a session");
            var sessionIdArg = new Argument<string>("sessionId", "Game session ID");
            directionalCmd.AddArgument(sessionIdArg);
            directionalCmd.SetHandler(async (InvocationContext ctx) =>
            {
                try
                {
                    var parseResult = ctx.ParseResult;
                    var sessionId = parseResult.GetValueForArgument(sessionIdArg);
                    await using var factory = new OrleansClientFactory();
                    await factory.ConnectAsync();
                    var mgmt = factory.GetGameManagement();
                    var result = await mgmt.SetDirectionalVisionAsync(sessionId, true);

                    if (result.Success)
                    {
                        if (Common.IsJsonOutput(parseResult))
                        {
                            Common.WriteOutput(parseResult, new { success = true, sessionId, directional = true });
                        }
                        else
                        {
                            Common.WriteSuccess(parseResult, $"Directional vision enabled for session {sessionId}");
                        }
                    }
                    else
                    {
                        Common.WriteError(parseResult, $"Error: {result.Message}");
                    }
                }
                catch (Exception ex)
                {
                    Common.WriteError(ctx.ParseResult, $"Failed: {ex.Message}");
                }
            });

            var omniCmd = new Command("omnidirectional", "Disable directional vision (use omnidirectional) for a session");
            var sessionIdArg2 = new Argument<string>("sessionId", "Game session ID");
            omniCmd.AddArgument(sessionIdArg2);
            omniCmd.SetHandler(async (InvocationContext ctx) =>
            {
                try
                {
                    var parseResult = ctx.ParseResult;
                    var sessionId = parseResult.GetValueForArgument(sessionIdArg2);
                    await using var factory = new OrleansClientFactory();
                    await factory.ConnectAsync();
                    var mgmt = factory.GetGameManagement();
                    var result = await mgmt.SetDirectionalVisionAsync(sessionId, false);

                    if (result.Success)
                    {
                        if (Common.IsJsonOutput(parseResult))
                        {
                            Common.WriteOutput(parseResult, new { success = true, sessionId, directional = false });
                        }
                        else
                        {
                            Common.WriteSuccess(parseResult, $"Omnidirectional vision enabled for session {sessionId}");
                        }
                    }
                    else
                    {
                        Common.WriteError(parseResult, $"Error: {result.Message}");
                    }
                }
                catch (Exception ex)
                {
                    Common.WriteError(ctx.ParseResult, $"Failed: {ex.Message}");
                }
            });

            var fovCmd = new Command("fov", "Set field of view degrees for a session");
            var fovSessionIdArg = new Argument<string>("sessionId", "Game session ID");
            var degreesArg = new Argument<int>("degrees", "FOV in degrees (1-360)");
            fovCmd.AddArgument(fovSessionIdArg);
            fovCmd.AddArgument(degreesArg);
            fovCmd.SetHandler(async (InvocationContext ctx) =>
            {
                try
                {
                    var parseResult = ctx.ParseResult;
                    var sessionId = parseResult.GetValueForArgument(fovSessionIdArg);
                    var degrees = parseResult.GetValueForArgument(degreesArg);

                    if (degrees < 1 || degrees > 360)
                    {
                        Common.WriteError(parseResult, "Error: FOV must be between 1 and 360 degrees");
                        return;
                    }

                    await using var factory = new OrleansClientFactory();
                    await factory.ConnectAsync();
                    var mgmt = factory.GetGameManagement();
                    var result = await mgmt.SetFieldOfViewAsync(sessionId, degrees);

                    if (result.Success)
                    {
                        if (Common.IsJsonOutput(parseResult))
                        {
                            Common.WriteOutput(parseResult, new { success = true, sessionId, fov = degrees });
                        }
                        else
                        {
                            Common.WriteSuccess(parseResult, $"FOV set to {degrees}° for session {sessionId}");
                        }
                    }
                    else
                    {
                        Common.WriteError(parseResult, $"Error: {result.Message}");
                    }
                }
                catch (Exception ex)
                {
                    Common.WriteError(ctx.ParseResult, $"Failed: {ex.Message}");
                }
            });

            var statusCmd = new Command("status", "Show vision mode status for a session");
            var sessionIdArg3 = new Argument<string>("sessionId", "Game session ID");
            statusCmd.AddArgument(sessionIdArg3);
            statusCmd.SetHandler(async (InvocationContext ctx) =>
            {
                try
                {
                    var parseResult = ctx.ParseResult;
                    var sessionId = parseResult.GetValueForArgument(sessionIdArg3);
                    await using var factory = new OrleansClientFactory();
                    await factory.ConnectAsync();
                    var mgmt = factory.GetGameManagement();
                    var status = await mgmt.GetVisionStatusAsync(sessionId);

                    if (status != null)
                    {
                        if (Common.IsJsonOutput(parseResult))
                        {
                            Common.WriteOutput(parseResult, new
                            {
                                success = true,
                                sessionId,
                                directionalVision = status.DirectionalVisionMode,
                                heading = status.HeadingDegrees,
                                fieldOfView = status.FieldOfViewDegrees,
                                lightingMode = status.LightingMode.ToString(),
                                visionMode = status.VisionMode.ToString()
                            });
                        }
                        else
                        {
                            Console.WriteLine($"Vision Status for session {sessionId}:");
                            Console.WriteLine($"  Directional Vision: {(status.DirectionalVisionMode ? "ON" : "OFF")}");
                            Console.WriteLine($"  Heading: {status.HeadingDegrees}°");
                            Console.WriteLine($"  Field of View: {status.FieldOfViewDegrees}°");
                            Console.WriteLine($"  Lighting Mode: {status.LightingMode}");
                            Console.WriteLine($"  Vision Mode: {status.VisionMode}");
                        }
                    }
                    else
                    {
                        Common.WriteError(parseResult, $"Session {sessionId} not found");
                    }
                }
                catch (Exception ex)
                {
                    Common.WriteError(ctx.ParseResult, $"Failed: {ex.Message}");
                }
            });

            visionCmd.AddCommand(directionalCmd);
            visionCmd.AddCommand(omniCmd);
            visionCmd.AddCommand(fovCmd);
            visionCmd.AddCommand(statusCmd);
            root.AddCommand(visionCmd);
        }
    }
}

