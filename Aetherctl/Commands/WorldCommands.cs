using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using System.Linq;
using System.Threading.Tasks;
using Aetherctl.Orleans;
using Aetherium.Server.MultiWorld;

namespace Aetherctl.Commands
{
    public static class WorldCommands
    {
        public static void AddToRoot(RootCommand root)
        {
            var worldCmd = new Command("world", "Manage game worlds");

            var createCmd = new Command("create", "Create a new game world");
            var nameArg = new Argument<string>("name", "World name");
            var descArg = new Argument<string>("description", "World description");
            var genOpt = new Option<string>("--generator", () => "rooms-and-corridors", "Generator type");
            var widthOpt = new Option<int>("--width", () => 100, "World width");
            var heightOpt = new Option<int>("--height", () => 100, "World height");
            var narrativeOpt = new Option<string?>("--narrative", () => null, "Narrative ID");
            createCmd.AddArgument(nameArg);
            createCmd.AddArgument(descArg);
            createCmd.AddOption(genOpt);
            createCmd.AddOption(widthOpt);
            createCmd.AddOption(heightOpt);
            createCmd.AddOption(narrativeOpt);
            createCmd.SetHandler(async (InvocationContext ctx) =>
            {
                try
                {
                    var parseResult = ctx.ParseResult;
                    var name = parseResult.GetValueForArgument(nameArg);
                    var desc = parseResult.GetValueForArgument(descArg);
                    var gen = parseResult.GetValueForOption(genOpt);
                    var width = parseResult.GetValueForOption(widthOpt);
                    var height = parseResult.GetValueForOption(heightOpt);
                    var narrativeId = parseResult.GetValueForOption(narrativeOpt);

                    await using var factory = new OrleansClientFactory();
                    await factory.ConnectAsync();
                    var mgmt = factory.GetGameManagement();
                    var request = new CreateWorldRequest
                    {
                        Name = name,
                        Description = desc,
                        GeneratorType = gen,
                        GeneratorParameters = new Dictionary<string, object>
                        {
                            ["Width"] = width,
                            ["Height"] = height
                        },
                        NarrativeId = narrativeId,
                        Size = new WorldSize { Width = width, Height = height, Depth = 1 }
                    };

                    var worldId = await mgmt.CreateWorldAsync(request);

                    if (Common.IsJsonOutput(parseResult))
                    {
                        Common.WriteOutput(parseResult, new
                        {
                            success = true,
                            worldId,
                            name,
                            size = $"{width}x{height}",
                            generator = gen,
                            narrativeId
                        });
                    }
                    else
                    {
                        Console.WriteLine($"✓ Created world: {worldId}");
                        Console.WriteLine($"  Name: {name}");
                        Console.WriteLine($"  Size: {width}x{height}");
                        Console.WriteLine($"  Generator: {gen}");
                        if (narrativeId != null)
                            Console.WriteLine($"  Narrative: {narrativeId}");
                    }
                }
                catch (Exception ex)
                {
                    Common.WriteError(ctx.ParseResult, $"Error creating world: {ex.Message}");
                }
            });

            var listCmd = new Command("list", "List all game worlds");
            listCmd.SetHandler(async (InvocationContext ctx) =>
            {
                try
                {
                    var parseResult = ctx.ParseResult;
                    await using var factory = new OrleansClientFactory();
                    await factory.ConnectAsync();
                    var mgmt = factory.GetGameManagement();
                    var worlds = await mgmt.ListWorldsAsync();

                    if (Common.IsJsonOutput(parseResult))
                    {
                        Common.WriteOutput(parseResult, new
                        {
                            success = true,
                            count = worlds.Count,
                            worlds = worlds.Select(w => new
                            {
                                worldId = w.WorldId,
                                name = w.Name,
                                state = w.State.ToString(),
                                players = $"{w.PlayerCount}/{w.MaxPlayers}",
                                maps = w.MapIds.Count,
                                narrativeId = w.NarrativeId,
                                createdAt = w.CreatedAt
                            })
                        });
                    }
                    else
                    {
                        if (worlds.Count == 0)
                        {
                            Console.WriteLine("No worlds found.");
                            return;
                        }

                        Console.WriteLine($"Found {worlds.Count} world(s):\n");
                        foreach (var world in worlds)
                        {
                            Console.WriteLine($"[{world.WorldId}]");
                            Console.WriteLine($"  Name: {world.Name}");
                            Console.WriteLine($"  State: {world.State}");
                            Console.WriteLine($"  Players: {world.PlayerCount}/{world.MaxPlayers}");
                            Console.WriteLine($"  Maps: {world.MapIds.Count}");
                            if (world.NarrativeId != null)
                                Console.WriteLine($"  Narrative: {world.NarrativeId}");
                            Console.WriteLine($"  Created: {world.CreatedAt:yyyy-MM-dd HH:mm:ss}");
                            Console.WriteLine();
                        }
                    }
                }
                catch (Exception ex)
                {
                    Common.WriteError(ctx.ParseResult, $"Error listing worlds: {ex.Message}");
                }
            });

            var infoCmd = new Command("info", "Get detailed world information");
            var worldIdArg = new Argument<string>("worldId", "World ID");
            infoCmd.AddArgument(worldIdArg);
            infoCmd.SetHandler(async (InvocationContext ctx) =>
            {
                try
                {
                    var parseResult = ctx.ParseResult;
                    var worldId = parseResult.GetValueForArgument(worldIdArg);
                    await using var factory = new OrleansClientFactory();
                    await factory.ConnectAsync();
                    var mgmt = factory.GetGameManagement();
                    var world = await mgmt.GetWorldInfoAsync(worldId);

                    if (world == null)
                    {
                        Common.WriteError(parseResult, $"World {worldId} not found");
                        return;
                    }

                    if (Common.IsJsonOutput(parseResult))
                    {
                        Common.WriteOutput(parseResult, new
                        {
                            success = true,
                            worldId = world.WorldId,
                            name = world.Name,
                            state = world.State.ToString(),
                            description = world.Description,
                            players = $"{world.PlayerCount}/{world.MaxPlayers}",
                            maps = world.MapIds,
                            narrativeId = world.NarrativeId,
                            createdAt = world.CreatedAt,
                            lastActivityAt = world.LastActivityAt
                        });
                    }
                    else
                    {
                        Console.WriteLine($"World: {world.Name}");
                        Console.WriteLine($"  ID: {world.WorldId}");
                        Console.WriteLine($"  State: {world.State}");
                        Console.WriteLine($"  Description: {world.Description}");
                        Console.WriteLine($"  Players: {world.PlayerCount}/{world.MaxPlayers}");
                        Console.WriteLine($"  Maps: {string.Join(", ", world.MapIds)}");
                        if (world.NarrativeId != null)
                            Console.WriteLine($"  Narrative: {world.NarrativeId}");
                        Console.WriteLine($"  Created: {world.CreatedAt:yyyy-MM-dd HH:mm:ss}");
                        if (world.LastActivityAt.HasValue)
                            Console.WriteLine($"  Last Activity: {world.LastActivityAt.Value:yyyy-MM-dd HH:mm:ss}");
                    }
                }
                catch (Exception ex)
                {
                    Common.WriteError(ctx.ParseResult, $"Error getting world info: {ex.Message}");
                }
            });

            var pauseCmd = new Command("pause", "Pause a running world");
            var pauseWorldIdArg = new Argument<string>("worldId", "World ID");
            pauseCmd.AddArgument(pauseWorldIdArg);
            pauseCmd.SetHandler(async (InvocationContext ctx) =>
            {
                try
                {
                    var parseResult = ctx.ParseResult;
                    var worldId = parseResult.GetValueForArgument(pauseWorldIdArg);
                    await using var factory = new OrleansClientFactory();
                    await factory.ConnectAsync();
                    var mgmt = factory.GetGameManagement();
                    var result = await mgmt.PauseWorldAsync(worldId);

                    if (result.Success)
                    {
                        if (Common.IsJsonOutput(parseResult))
                            Common.WriteOutput(parseResult, new { success = true, worldId });
                        else
                            Common.WriteSuccess(parseResult, $"World {worldId} paused");
                    }
                    else
                    {
                        Common.WriteError(parseResult, $"Error: {result.Message}");
                    }
                }
                catch (Exception ex)
                {
                    Common.WriteError(ctx.ParseResult, $"Error pausing world: {ex.Message}");
                }
            });

            var resumeCmd = new Command("resume", "Resume a paused world");
            var resumeWorldIdArg = new Argument<string>("worldId", "World ID");
            resumeCmd.AddArgument(resumeWorldIdArg);
            resumeCmd.SetHandler(async (InvocationContext ctx) =>
            {
                try
                {
                    var parseResult = ctx.ParseResult;
                    var worldId = parseResult.GetValueForArgument(resumeWorldIdArg);
                    await using var factory = new OrleansClientFactory();
                    await factory.ConnectAsync();
                    var mgmt = factory.GetGameManagement();
                    var result = await mgmt.ResumeWorldAsync(worldId);

                    if (result.Success)
                    {
                        if (Common.IsJsonOutput(parseResult))
                            Common.WriteOutput(parseResult, new { success = true, worldId });
                        else
                            Common.WriteSuccess(parseResult, $"World {worldId} resumed");
                    }
                    else
                    {
                        Common.WriteError(parseResult, $"Error: {result.Message}");
                    }
                }
                catch (Exception ex)
                {
                    Common.WriteError(ctx.ParseResult, $"Error resuming world: {ex.Message}");
                }
            });

            var shutdownCmd = new Command("shutdown", "Shut down and remove a world");
            var shutdownWorldIdArg = new Argument<string>("worldId", "World ID");
            shutdownCmd.AddArgument(shutdownWorldIdArg);
            shutdownCmd.SetHandler(async (InvocationContext ctx) =>
            {
                try
                {
                    var parseResult = ctx.ParseResult;
                    var worldId = parseResult.GetValueForArgument(shutdownWorldIdArg);
                    await using var factory = new OrleansClientFactory();
                    await factory.ConnectAsync();
                    var mgmt = factory.GetGameManagement();
                    var result = await mgmt.ShutdownWorldAsync(worldId);

                    if (result.Success)
                    {
                        if (Common.IsJsonOutput(parseResult))
                            Common.WriteOutput(parseResult, new { success = true, worldId });
                        else
                            Common.WriteSuccess(parseResult, $"World {worldId} shut down");
                    }
                    else
                    {
                        Common.WriteError(parseResult, $"Error: {result.Message}");
                    }
                }
                catch (Exception ex)
                {
                    Common.WriteError(ctx.ParseResult, $"Error shutting down world: {ex.Message}");
                }
            });

            worldCmd.AddCommand(createCmd);
            worldCmd.AddCommand(listCmd);
            worldCmd.AddCommand(infoCmd);
            worldCmd.AddCommand(pauseCmd);
            worldCmd.AddCommand(resumeCmd);
            worldCmd.AddCommand(shutdownCmd);
            root.AddCommand(worldCmd);
        }
    }
}

