using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Aetherctl.Orleans;
using Aetherium.Model.Instances;
using Aetherium.Model.Groups;
using Aetherium.Model.Worlds;

namespace Aetherctl.Commands
{
    /// <summary>
    /// Operator-facing commands for the dungeon-instance / party stack: enter or sweep instances in
    /// a world, and create/inspect/populate parties. Talks to the grains directly via Orleans.
    /// </summary>
    public static class InstanceCommands
    {
        public static void AddToRoot(RootCommand root)
        {
            AddInstanceCommand(root);
            AddPartyCommand(root);
        }

        private static void AddInstanceCommand(RootCommand root)
        {
            var instanceCmd = new Command("instance", "Enter and manage dungeon instances");

            // instance enter <worldId> <dungeonId> [--player <id>] [--party <id>]
            var enterCmd = new Command("enter", "Enter (allocate/reuse) a dungeon instance in a world");
            var worldArg = new Argument<string>("worldId", "World that hosts the instance allocator");
            var dungeonArg = new Argument<string>("dungeonId", "Dungeon to enter");
            var playerOpt = new Option<string?>("--player", () => null, "Player ID entering (solo). Defaults to 'cli-player'.");
            var partyOpt = new Option<string?>("--party", () => null, "Party ID entering (its members enter together)");
            enterCmd.AddArgument(worldArg);
            enterCmd.AddArgument(dungeonArg);
            enterCmd.AddOption(playerOpt);
            enterCmd.AddOption(partyOpt);
            enterCmd.SetHandler(async (InvocationContext ctx) =>
            {
                var parseResult = ctx.ParseResult;
                var worldId = parseResult.GetValueForArgument(worldArg);
                var dungeonId = parseResult.GetValueForArgument(dungeonArg);
                var partyId = parseResult.GetValueForOption(partyOpt);
                var player = parseResult.GetValueForOption(playerOpt);
                try
                {
                    await using var factory = new OrleansClientFactory();
                    await factory.ConnectAsync();

                    List<PlayerId> playerIds;
                    PartyId? partyIdValue = null;
                    if (!string.IsNullOrWhiteSpace(partyId))
                    {
                        partyIdValue = new PartyId(partyId!);
                        playerIds = await factory.GetParty(partyId!).GetMemberIdsAsync();
                        if (playerIds.Count == 0)
                        {
                            Common.WriteError(parseResult, $"Party '{partyId}' has no members");
                            return;
                        }
                    }
                    else
                    {
                        playerIds = new List<PlayerId> { new PlayerId(string.IsNullOrWhiteSpace(player) ? "cli-player" : player!) };
                    }

                    var allocator = factory.GetInstanceAllocator(worldId);
                    var result = await allocator.EnterAsync(new EnterInstanceRequest
                    {
                        WorldId = new WorldId(worldId),
                        DungeonId = new DungeonId(dungeonId),
                        PartyId = partyIdValue,
                        PlayerIds = playerIds
                    });

                    if (!result.Success || !result.InstanceId.HasValue)
                    {
                        Common.WriteError(parseResult, result.ErrorMessage ?? "Enter failed");
                        return;
                    }

                    var mapId = await factory.GetDungeonInstance(result.InstanceId.Value.Value).GetMapIdAsync();
                    if (Common.IsJsonOutput(parseResult))
                        Common.WriteOutput(parseResult, new { success = true, instanceId = result.InstanceId.Value.Value, mapId });
                    else
                        Console.WriteLine($"✓ Entered '{dungeonId}' → instance {result.InstanceId.Value.Value} (map {mapId})");
                }
                catch (Exception ex)
                {
                    Common.WriteError(parseResult, $"Error entering instance: {ex.Message}");
                }
            });

            // instance sweep <worldId>
            var sweepCmd = new Command("sweep", "Reap abandoned/idle instances in a world");
            var sweepWorldArg = new Argument<string>("worldId", "World whose allocator to sweep");
            sweepCmd.AddArgument(sweepWorldArg);
            sweepCmd.SetHandler(async (InvocationContext ctx) =>
            {
                var parseResult = ctx.ParseResult;
                var worldId = parseResult.GetValueForArgument(sweepWorldArg);
                try
                {
                    await using var factory = new OrleansClientFactory();
                    await factory.ConnectAsync();
                    var reaped = await factory.GetInstanceAllocator(worldId).SweepAbandonedInstancesAsync();
                    if (Common.IsJsonOutput(parseResult))
                        Common.WriteOutput(parseResult, new { success = true, reaped });
                    else
                        Console.WriteLine($"✓ Reaped {reaped} instance(s) in '{worldId}'");
                }
                catch (Exception ex)
                {
                    Common.WriteError(parseResult, $"Error sweeping instances: {ex.Message}");
                }
            });

            instanceCmd.AddCommand(enterCmd);
            instanceCmd.AddCommand(sweepCmd);
            root.AddCommand(instanceCmd);
        }

        private static void AddPartyCommand(RootCommand root)
        {
            var partyCmd = new Command("party", "Create and manage parties");

            // party create <partyId> <leaderId> [--name <name>]
            var createCmd = new Command("create", "Create a party with a leader");
            var idArg = new Argument<string>("partyId", "Party ID to create");
            var leaderArg = new Argument<string>("leaderId", "Leader player ID");
            var nameOpt = new Option<string?>("--name", () => null, "Leader display name");
            createCmd.AddArgument(idArg);
            createCmd.AddArgument(leaderArg);
            createCmd.AddOption(nameOpt);
            createCmd.SetHandler(async (InvocationContext ctx) =>
            {
                var parseResult = ctx.ParseResult;
                var partyId = parseResult.GetValueForArgument(idArg);
                var leaderId = parseResult.GetValueForArgument(leaderArg);
                var name = parseResult.GetValueForOption(nameOpt) ?? leaderId;
                try
                {
                    await using var factory = new OrleansClientFactory();
                    await factory.ConnectAsync();
                    await factory.GetParty(partyId).CreateAsync(new PlayerId(leaderId), name);
                    Common.WriteSuccess(parseResult, $"Created party '{partyId}' led by {leaderId}");
                }
                catch (Exception ex)
                {
                    Common.WriteError(parseResult, $"Error creating party: {ex.Message}");
                }
            });

            // party add <partyId> <playerId> [--name <name>]
            var addCmd = new Command("add", "Add a member to a party");
            var addIdArg = new Argument<string>("partyId", "Party ID");
            var playerArg = new Argument<string>("playerId", "Player ID to add");
            var addNameOpt = new Option<string?>("--name", () => null, "Member display name");
            addCmd.AddArgument(addIdArg);
            addCmd.AddArgument(playerArg);
            addCmd.AddOption(addNameOpt);
            addCmd.SetHandler(async (InvocationContext ctx) =>
            {
                var parseResult = ctx.ParseResult;
                var partyId = parseResult.GetValueForArgument(addIdArg);
                var playerId = parseResult.GetValueForArgument(playerArg);
                var name = parseResult.GetValueForOption(addNameOpt) ?? playerId;
                try
                {
                    await using var factory = new OrleansClientFactory();
                    await factory.ConnectAsync();
                    var added = await factory.GetParty(partyId).AddMemberAsync(new PlayerId(playerId), name);
                    if (added)
                        Common.WriteSuccess(parseResult, $"Added {playerId} to party '{partyId}'");
                    else
                        Common.WriteError(parseResult, $"Could not add {playerId} (party full or already a member)");
                }
                catch (Exception ex)
                {
                    Common.WriteError(parseResult, $"Error adding member: {ex.Message}");
                }
            });

            // party show <partyId>
            var showCmd = new Command("show", "Show a party's members");
            var showIdArg = new Argument<string>("partyId", "Party ID");
            showCmd.AddArgument(showIdArg);
            showCmd.SetHandler(async (InvocationContext ctx) =>
            {
                var parseResult = ctx.ParseResult;
                var partyId = parseResult.GetValueForArgument(showIdArg);
                try
                {
                    await using var factory = new OrleansClientFactory();
                    await factory.ConnectAsync();
                    var info = await factory.GetParty(partyId).GetInfoAsync();
                    if (info == null)
                    {
                        Common.WriteError(parseResult, $"Party '{partyId}' not found");
                        return;
                    }
                    if (Common.IsJsonOutput(parseResult))
                    {
                        Common.WriteOutput(parseResult, new
                        {
                            success = true,
                            partyId = info.PartyId.Value,
                            maxMembers = info.MaxMembers,
                            members = info.Members.Select(m => new { playerId = m.PlayerId.Value, m.Name, role = m.Role.ToString(), m.IsOnline })
                        });
                    }
                    else
                    {
                        Console.WriteLine($"Party '{info.PartyId.Value}' ({info.Members.Count}/{info.MaxMembers}):");
                        foreach (var m in info.Members)
                            Console.WriteLine($"  - {m.PlayerId.Value} ({m.Role}){(m.IsOnline ? "" : " [offline]")}");
                    }
                }
                catch (Exception ex)
                {
                    Common.WriteError(parseResult, $"Error showing party: {ex.Message}");
                }
            });

            partyCmd.AddCommand(createCmd);
            partyCmd.AddCommand(addCmd);
            partyCmd.AddCommand(showCmd);
            root.AddCommand(partyCmd);
        }
    }
}
