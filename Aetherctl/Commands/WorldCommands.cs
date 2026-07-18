using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using System.Linq;
using System.Threading.Tasks;
using Aetherctl.Orleans;
using Aetherctl.Auth;
using Aetherctl.Config;
using Aetherctl.SignalR;
using Aetherium.Server.MultiWorld;
using Aetherium.Server.Management;
using Aetherium.Model.Worlds;

namespace Aetherctl.Commands
{
    public static class WorldCommands
    {
        /// <summary>
        /// Tries to use SignalR if B2C is configured, otherwise falls back to Orleans.
        /// </summary>
        private static async Task<(bool useSignalR, ServerConfig? server)> TryGetSignalRConfigAsync()
        {
            var server = ConfigManager.GetCurrentServer();
            if (server != null && server.B2C != null && 
                !string.IsNullOrEmpty(server.B2C.Tenant) && 
                !string.IsNullOrEmpty(server.B2C.ClientId))
            {
                return (true, server);
            }
            return (false, null);
        }

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

                    string worldId;
                    var (useSignalR, server) = await TryGetSignalRConfigAsync();
                    if (useSignalR && server != null)
                    {
                        try
                        {
                            await using var authService = new AuthService(
                                server.B2C.Tenant,
                                server.B2C.Policy,
                                server.B2C.ClientId,
                                server.B2C.Scopes);
                            var token = await authService.AcquireTokenDeviceCodeAsync();
                            await using var client = new ManagementClient(server.BaseUrl, async () => token);
                            await client.ConnectAsync();
                            worldId = await client.CreateWorldAsync(request);
                        }
                        catch (Exception ex)
                        {
                            // Fallback to Orleans if SignalR fails
                            await using var factory = new OrleansClientFactory();
                            await factory.ConnectAsync();
                            var mgmt = factory.GetGameManagement();
                            worldId = await mgmt.CreateWorldAsync(request);
                        }
                    }
                    else
                    {
                        // Use Orleans directly
                        await using var factory = new OrleansClientFactory();
                        await factory.ConnectAsync();
                        var mgmt = factory.GetGameManagement();
                        worldId = await mgmt.CreateWorldAsync(request);
                    }

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
                    List<WorldInfo> worlds;
                    var (useSignalR, server) = await TryGetSignalRConfigAsync();
                    if (useSignalR && server != null)
                    {
                        try
                        {
                            await using var authService = new AuthService(
                                server.B2C.Tenant,
                                server.B2C.Policy,
                                server.B2C.ClientId,
                                server.B2C.Scopes);
                            var token = await authService.AcquireTokenDeviceCodeAsync();
                            await using var client = new ManagementClient(server.BaseUrl, async () => token);
                            await client.ConnectAsync();
                            worlds = await client.ListWorldsAsync();
                        }
                        catch (Exception ex)
                        {
                            // Fallback to Orleans if SignalR fails
                            await using var factory = new OrleansClientFactory();
                            await factory.ConnectAsync();
                            var mgmt = factory.GetGameManagement();
                            worlds = await mgmt.ListWorldsAsync();
                        }
                    }
                    else
                    {
                        // Use Orleans directly
                        await using var factory = new OrleansClientFactory();
                        await factory.ConnectAsync();
                        var mgmt = factory.GetGameManagement();
                        worlds = await mgmt.ListWorldsAsync();
                    }

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
                    WorldInfo? world;
                    var (useSignalR, server) = await TryGetSignalRConfigAsync();
                    if (useSignalR && server != null)
                    {
                        try
                        {
                            await using var authService = new AuthService(
                                server.B2C.Tenant,
                                server.B2C.Policy,
                                server.B2C.ClientId,
                                server.B2C.Scopes);
                            var token = await authService.AcquireTokenDeviceCodeAsync();
                            await using var client = new ManagementClient(server.BaseUrl, async () => token);
                            await client.ConnectAsync();
                            world = await client.GetWorldInfoAsync(worldId);
                        }
                        catch (Exception ex)
                        {
                            // Fallback to Orleans if SignalR fails
                            await using var factory = new OrleansClientFactory();
                            await factory.ConnectAsync();
                            var mgmt = factory.GetGameManagement();
                            world = await mgmt.GetWorldInfoAsync(worldId);
                        }
                    }
                    else
                    {
                        // Use Orleans directly
                        await using var factory = new OrleansClientFactory();
                        await factory.ConnectAsync();
                        var mgmt = factory.GetGameManagement();
                        world = await mgmt.GetWorldInfoAsync(worldId);
                    }

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
                    OperationResult result;
                    var (useSignalR, server) = await TryGetSignalRConfigAsync();
                    if (useSignalR && server != null)
                    {
                        try
                        {
                            await using var authService = new AuthService(
                                server.B2C.Tenant,
                                server.B2C.Policy,
                                server.B2C.ClientId,
                                server.B2C.Scopes);
                            var token = await authService.AcquireTokenDeviceCodeAsync();
                            await using var client = new ManagementClient(server.BaseUrl, async () => token);
                            await client.ConnectAsync();
                            result = await client.PauseWorldAsync(worldId);
                        }
                        catch (Exception ex)
                        {
                            // Fallback to Orleans if SignalR fails
                            await using var factory = new OrleansClientFactory();
                            await factory.ConnectAsync();
                            var mgmt = factory.GetGameManagement();
                            result = await mgmt.PauseWorldAsync(worldId);
                        }
                    }
                    else
                    {
                        // Use Orleans directly
                        await using var factory = new OrleansClientFactory();
                        await factory.ConnectAsync();
                        var mgmt = factory.GetGameManagement();
                        result = await mgmt.PauseWorldAsync(worldId);
                    }

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
                    OperationResult result;
                    var (useSignalR, server) = await TryGetSignalRConfigAsync();
                    if (useSignalR && server != null)
                    {
                        try
                        {
                            await using var authService = new AuthService(
                                server.B2C.Tenant,
                                server.B2C.Policy,
                                server.B2C.ClientId,
                                server.B2C.Scopes);
                            var token = await authService.AcquireTokenDeviceCodeAsync();
                            await using var client = new ManagementClient(server.BaseUrl, async () => token);
                            await client.ConnectAsync();
                            result = await client.ResumeWorldAsync(worldId);
                        }
                        catch (Exception ex)
                        {
                            // Fallback to Orleans if SignalR fails
                            await using var factory = new OrleansClientFactory();
                            await factory.ConnectAsync();
                            var mgmt = factory.GetGameManagement();
                            result = await mgmt.ResumeWorldAsync(worldId);
                        }
                    }
                    else
                    {
                        // Use Orleans directly
                        await using var factory = new OrleansClientFactory();
                        await factory.ConnectAsync();
                        var mgmt = factory.GetGameManagement();
                        result = await mgmt.ResumeWorldAsync(worldId);
                    }

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
                    OperationResult result;
                    var (useSignalR, server) = await TryGetSignalRConfigAsync();
                    if (useSignalR && server != null)
                    {
                        try
                        {
                            await using var authService = new AuthService(
                                server.B2C.Tenant,
                                server.B2C.Policy,
                                server.B2C.ClientId,
                                server.B2C.Scopes);
                            var token = await authService.AcquireTokenDeviceCodeAsync();
                            await using var client = new ManagementClient(server.BaseUrl, async () => token);
                            await client.ConnectAsync();
                            result = await client.ShutdownAsync(worldId);
                        }
                        catch (Exception ex)
                        {
                            // Fallback to Orleans if SignalR fails
                            await using var factory = new OrleansClientFactory();
                            await factory.ConnectAsync();
                            var mgmt = factory.GetGameManagement();
                            result = await mgmt.ShutdownWorldAsync(worldId);
                        }
                    }
                    else
                    {
                        // Use Orleans directly
                        await using var factory = new OrleansClientFactory();
                        await factory.ConnectAsync();
                        var mgmt = factory.GetGameManagement();
                        result = await mgmt.ShutdownWorldAsync(worldId);
                    }

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

            // ACL commands
            var setAclCmd = new Command("set-acl", "Set access control list for a world");
            var aclWorldIdArg = new Argument<string>("worldId", "World ID");
            var accessLevelOpt = new Option<string>("--access-level", () => "public", "Access level: public or private");
            var allowedPlayersOpt = new Option<string[]>("--allowed-players", () => Array.Empty<string>(), "Comma-separated list of allowed player IDs");
            setAclCmd.AddArgument(aclWorldIdArg);
            setAclCmd.AddOption(accessLevelOpt);
            setAclCmd.AddOption(allowedPlayersOpt);
            setAclCmd.SetHandler(async (InvocationContext ctx) =>
            {
                try
                {
                    var parseResult = ctx.ParseResult;
                    var worldId = parseResult.GetValueForArgument(aclWorldIdArg);
                    var accessLevelStr = parseResult.GetValueForOption(accessLevelOpt);
                    var allowedPlayers = parseResult.GetValueForOption(allowedPlayersOpt) ?? Array.Empty<string>();

                    var accessLevel = accessLevelStr?.ToLower() == "private" 
                        ? Aetherium.Model.Worlds.WorldAccessLevel.Private 
                        : Aetherium.Model.Worlds.WorldAccessLevel.Public;

                    var acl = new Aetherium.Model.Worlds.WorldAcl
                    {
                        AccessLevel = accessLevel,
                        AllowedPlayers = new HashSet<Aetherium.Model.Worlds.PlayerId>(
                            allowedPlayers.Select(p => new Aetherium.Model.Worlds.PlayerId(p))
                        ),
                        OwnerPlayers = new HashSet<Aetherium.Model.Worlds.PlayerId>()
                    };

                    await using var factory = new OrleansClientFactory();
                    await factory.ConnectAsync();
                    var mgmt = factory.GetGameManagement();
                    var result = await mgmt.SetWorldAclAsync(worldId, acl);

                    if (result.Success)
                    {
                        if (Common.IsJsonOutput(parseResult))
                            Common.WriteOutput(parseResult, new { success = true, worldId, accessLevel = accessLevelStr });
                        else
                            Common.WriteSuccess(parseResult, $"Set ACL for world {worldId} to {accessLevelStr}");
                    }
                    else
                    {
                        Common.WriteError(parseResult, $"Error: {result.Message}");
                    }
                }
                catch (Exception ex)
                {
                    Common.WriteError(ctx.ParseResult, $"Error setting ACL: {ex.Message}");
                }
            });

            var getAclCmd = new Command("get-acl", "Get access control list for a world");
            var getAclWorldIdArg = new Argument<string>("worldId", "World ID");
            getAclCmd.AddArgument(getAclWorldIdArg);
            getAclCmd.SetHandler(async (InvocationContext ctx) =>
            {
                try
                {
                    var parseResult = ctx.ParseResult;
                    var worldId = parseResult.GetValueForArgument(getAclWorldIdArg);

                    await using var factory = new OrleansClientFactory();
                    await factory.ConnectAsync();
                    var mgmt = factory.GetGameManagement();
                    var acl = await mgmt.GetWorldAclAsync(worldId);

                    if (acl == null)
                    {
                        Common.WriteError(parseResult, $"ACL not found for world {worldId}");
                        return;
                    }

                    if (Common.IsJsonOutput(parseResult))
                    {
                        Common.WriteOutput(parseResult, new
                        {
                            success = true,
                            worldId,
                            accessLevel = acl.AccessLevel.ToString(),
                            allowedPlayers = acl.AllowedPlayers.Select(p => p.Value).ToArray(),
                            ownerPlayers = acl.OwnerPlayers.Select(p => p.Value).ToArray()
                        });
                    }
                    else
                    {
                        Console.WriteLine($"World: {worldId}");
                        Console.WriteLine($"  Access Level: {acl.AccessLevel}");
                        Console.WriteLine($"  Allowed Players: {string.Join(", ", acl.AllowedPlayers.Select(p => p.Value))}");
                        Console.WriteLine($"  Owner Players: {string.Join(", ", acl.OwnerPlayers.Select(p => p.Value))}");
                    }
                }
                catch (Exception ex)
                {
                    Common.WriteError(ctx.ParseResult, $"Error getting ACL: {ex.Message}");
                }
            });

            // Invite commands
            var inviteCmd = new Command("invite", "Invite a player to a private world");
            var inviteWorldIdArg = new Argument<string>("worldId", "World ID");
            var invitePlayerIdArg = new Argument<string>("playerId", "Player ID");
            inviteCmd.AddArgument(inviteWorldIdArg);
            inviteCmd.AddArgument(invitePlayerIdArg);
            inviteCmd.SetHandler(async (InvocationContext ctx) =>
            {
                try
                {
                    var parseResult = ctx.ParseResult;
                    var worldId = parseResult.GetValueForArgument(inviteWorldIdArg);
                    var playerId = parseResult.GetValueForArgument(invitePlayerIdArg);

                    await using var factory = new OrleansClientFactory();
                    await factory.ConnectAsync();
                    var mgmt = factory.GetGameManagement();
                    var inviteId = await mgmt.InvitePlayerAsync(worldId, playerId);

                    if (Common.IsJsonOutput(parseResult))
                    {
                        Common.WriteOutput(parseResult, new { success = true, worldId, playerId, inviteId });
                    }
                    else
                    {
                        Console.WriteLine($"✓ Invited player {playerId} to world {worldId}");
                        Console.WriteLine($"  Invite ID: {inviteId}");
                    }
                }
                catch (Exception ex)
                {
                    Common.WriteError(ctx.ParseResult, $"Error inviting player: {ex.Message}");
                }
            });

            var acceptInviteCmd = new Command("accept-invite", "Accept a world invite");
            var inviteIdArg = new Argument<string>("inviteId", "Invite ID");
            acceptInviteCmd.AddArgument(inviteIdArg);
            acceptInviteCmd.SetHandler(async (InvocationContext ctx) =>
            {
                try
                {
                    var parseResult = ctx.ParseResult;
                    var inviteId = parseResult.GetValueForArgument(inviteIdArg);

                    await using var factory = new OrleansClientFactory();
                    await factory.ConnectAsync();
                    var mgmt = factory.GetGameManagement();
                    var result = await mgmt.AcceptInviteAsync(inviteId);

                    if (result.Success)
                    {
                        if (Common.IsJsonOutput(parseResult))
                            Common.WriteOutput(parseResult, new { success = true, inviteId });
                        else
                            Common.WriteSuccess(parseResult, $"Accepted invite {inviteId}");
                    }
                    else
                    {
                        Common.WriteError(parseResult, $"Error: {result.Message}");
                    }
                }
                catch (Exception ex)
                {
                    Common.WriteError(ctx.ParseResult, $"Error accepting invite: {ex.Message}");
                }
            });

            // dump: omniscient, FOV-independent snapshot of a world's tiles and entities
            var dumpCmd = new Command("dump", "Dump a world's tiles and entities (omniscient snapshot)");
            var dumpWorldIdArg = new Argument<string>("worldId", "World ID");
            dumpCmd.AddArgument(dumpWorldIdArg);
            dumpCmd.SetHandler(async (InvocationContext ctx) =>
            {
                try
                {
                    var parseResult = ctx.ParseResult;
                    var worldId = parseResult.GetValueForArgument(dumpWorldIdArg);

                    await using var factory = new OrleansClientFactory();
                    await factory.ConnectAsync();
                    var mgmt = factory.GetGameManagement();
                    var json = await mgmt.GetWorldSnapshotAsync(worldId);

                    if (string.IsNullOrEmpty(json))
                    {
                        Common.WriteError(parseResult, $"No snapshot for world '{worldId}' (unknown world, not initialized in this process, or operator access disabled).");
                        Environment.Exit(1);
                        return;
                    }

                    if (Common.IsJsonOutput(parseResult))
                    {
                        Console.WriteLine(json);
                    }
                    else
                    {
                        var snapshot = System.Text.Json.JsonSerializer.Deserialize<Aetherium.Model.WorldSnapshotDto>(json);
                        if (snapshot == null)
                        {
                            Common.WriteError(parseResult, "Failed to parse world snapshot.");
                            Environment.Exit(1);
                            return;
                        }
                        Console.WriteLine($"World: {snapshot.WorldId}");
                        Console.WriteLine($"  Map: {snapshot.MapId}");
                        Console.WriteLine($"  Bounds: {snapshot.Width} x {snapshot.Height} x {snapshot.Depth}");
                        Console.WriteLine($"  Entities: {snapshot.EntityCount}{(snapshot.Truncated ? " (truncated)" : "")}");
                        Console.WriteLine($"  Occupied tiles: {snapshot.TileCount}");
                        foreach (var g in snapshot.Entities.GroupBy(e => e.Type).OrderByDescending(g => g.Count()))
                            Console.WriteLine($"    {g.Key}: {g.Count()}");
                    }
                }
                catch (Exception ex)
                {
                    Common.WriteError(ctx.ParseResult, $"Error dumping world: {ex.Message}");
                    Environment.Exit(1);
                }
            });

            worldCmd.AddCommand(createCmd);
            worldCmd.AddCommand(listCmd);
            worldCmd.AddCommand(infoCmd);
            worldCmd.AddCommand(pauseCmd);
            worldCmd.AddCommand(resumeCmd);
            worldCmd.AddCommand(shutdownCmd);
            worldCmd.AddCommand(setAclCmd);
            worldCmd.AddCommand(getAclCmd);
            // edit: run any world-building (world_edit) tool against a live world
            var editCmd = new Command("edit", "Execute a world-building tool against a running world");
            var editWorldIdArg = new Argument<string>("worldId", "World ID");
            var editToolIdArg = new Argument<string>("toolId", "World-building tool ID (e.g. spawnentity, setterrain, destroyentity)");
            var editArgsOpt = new Option<string?>("--args", () => null, "Tool arguments as JSON object");
            editCmd.AddArgument(editWorldIdArg);
            editCmd.AddArgument(editToolIdArg);
            editCmd.AddOption(editArgsOpt);
            editCmd.SetHandler(async (InvocationContext ctx) =>
            {
                try
                {
                    var parseResult = ctx.ParseResult;
                    var worldId = parseResult.GetValueForArgument(editWorldIdArg);
                    var toolId = parseResult.GetValueForArgument(editToolIdArg);
                    var argsJson = parseResult.GetValueForOption(editArgsOpt);

                    Dictionary<string, object> args = new();
                    if (!string.IsNullOrWhiteSpace(argsJson))
                    {
                        try
                        {
                            using var doc = System.Text.Json.JsonDocument.Parse(argsJson);
                            foreach (var p in doc.RootElement.EnumerateObject())
                                args[p.Name] = ActionScript.ToObject(p.Value);
                        }
                        catch (System.Text.Json.JsonException ex)
                        {
                            Common.WriteError(parseResult, $"Invalid --args JSON: {ex.Message}");
                            Environment.Exit(1);
                            return;
                        }
                    }

                    await using var factory = new OrleansClientFactory();
                    await factory.ConnectAsync();
                    var mgmt = factory.GetGameManagement();
                    var result = await mgmt.ExecuteWorldToolAsync(worldId, toolId, args);

                    if (Common.IsJsonOutput(parseResult))
                    {
                        Common.WriteOutput(parseResult, new { success = result.Success, message = result.Message, data = result.Data });
                    }
                    else
                    {
                        Console.WriteLine(result.Success
                            ? $"✓ {toolId}: {result.Message}"
                            : $"✗ {toolId} failed: {result.Message}");
                    }

                    if (!result.Success)
                        Environment.Exit(1);
                }
                catch (Exception ex)
                {
                    Common.WriteError(ctx.ParseResult, $"Error executing world tool: {ex.Message}");
                    Environment.Exit(1);
                }
            });

            // spawn: convenience wrapper over the spawnentity tool
            var spawnCmd = new Command("spawn", "Spawn a creature into a running world");
            var spawnWorldIdArg = new Argument<string>("worldId", "World ID");
            var spawnTypeOpt = new Option<string>("--type", "Creature type (monster, wolf, bear, bandit, snake, zombie)") { IsRequired = true };
            var spawnAtOpt = new Option<string>("--at", "Location as x,y or x,y,z") { IsRequired = true };
            spawnCmd.AddArgument(spawnWorldIdArg);
            spawnCmd.AddOption(spawnTypeOpt);
            spawnCmd.AddOption(spawnAtOpt);
            spawnCmd.SetHandler(async (InvocationContext ctx) =>
            {
                try
                {
                    var parseResult = ctx.ParseResult;
                    var worldId = parseResult.GetValueForArgument(spawnWorldIdArg);
                    var type = parseResult.GetValueForOption(spawnTypeOpt);
                    var at = parseResult.GetValueForOption(spawnAtOpt);

                    var parts = (at ?? string.Empty).Split(',');
                    if (parts.Length < 2
                        || !int.TryParse(parts[0].Trim(), out var x)
                        || !int.TryParse(parts[1].Trim(), out var y))
                    {
                        Common.WriteError(parseResult, "Invalid --at value; expected 'x,y' or 'x,y,z'");
                        Environment.Exit(1);
                        return;
                    }
                    int z = 0;
                    if (parts.Length >= 3) int.TryParse(parts[2].Trim(), out z);

                    var args = new Dictionary<string, object>
                    {
                        ["x"] = x,
                        ["y"] = y,
                        ["z"] = z,
                        ["entityType"] = type!
                    };

                    await using var factory = new OrleansClientFactory();
                    await factory.ConnectAsync();
                    var mgmt = factory.GetGameManagement();
                    var result = await mgmt.ExecuteWorldToolAsync(worldId, "spawnentity", args);

                    if (Common.IsJsonOutput(parseResult))
                    {
                        Common.WriteOutput(parseResult, new { success = result.Success, message = result.Message, data = result.Data });
                    }
                    else
                    {
                        Console.WriteLine(result.Success
                            ? $"✓ {result.Message}{(result.Data != null && result.Data.TryGetValue("entityId", out var id) ? $" (entityId: {id})" : "")}"
                            : $"✗ Spawn failed: {result.Message}");
                    }

                    if (!result.Success)
                        Environment.Exit(1);
                }
                catch (Exception ex)
                {
                    Common.WriteError(ctx.ParseResult, $"Error spawning entity: {ex.Message}");
                    Environment.Exit(1);
                }
            });

            worldCmd.AddCommand(inviteCmd);
            worldCmd.AddCommand(acceptInviteCmd);
            worldCmd.AddCommand(dumpCmd);
            worldCmd.AddCommand(editCmd);
            worldCmd.AddCommand(spawnCmd);
            root.AddCommand(worldCmd);
        }
    }
}

