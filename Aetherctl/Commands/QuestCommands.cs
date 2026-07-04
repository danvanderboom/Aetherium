using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Linq;
using System.Threading.Tasks;
using Aetherctl.Orleans;
using Aetherium.Server.Narrative;

namespace Aetherctl.Commands
{
    /// <summary>
    /// Player/ops-facing quest commands: list startable quests, accept a quest, and view the
    /// quest log for a world. Resolves the world's narrative-state grain via the shared
    /// <see cref="NarrativeStateResolver"/> (worldId → narrativeId → scope → grain).
    /// </summary>
    public static class QuestCommands
    {
        public static void AddToRoot(RootCommand root)
        {
            var questCmd = new Command("quest", "Inspect and activate quests for a world");

            var worldArg = new Argument<string>("worldId", "World ID whose narrative to act on");

            // quest available <worldId>
            var availableCmd = new Command("available", "List quests that can currently be started");
            availableCmd.AddArgument(worldArg);
            availableCmd.SetHandler(async (InvocationContext ctx) =>
            {
                var parseResult = ctx.ParseResult;
                var worldId = parseResult.GetValueForArgument(worldArg);
                try
                {
                    await using var factory = new OrleansClientFactory();
                    var client = await factory.ConnectAsync();
                    var grain = await NarrativeStateResolver.ResolveForWorldAsync(client, worldId);
                    if (grain == null)
                    {
                        Common.WriteError(parseResult, $"No narrative associated with world '{worldId}' (or world not found)");
                        return;
                    }

                    var quests = await grain.GetAvailableQuestsAsync();
                    if (Common.IsJsonOutput(parseResult))
                    {
                        Common.WriteOutput(parseResult, new
                        {
                            success = true,
                            worldId,
                            count = quests.Count,
                            quests = quests.Select(q => new { q.QuestId, q.Title, objectives = q.Objectives.Count })
                        });
                    }
                    else
                    {
                        Console.WriteLine($"Available quests in '{worldId}': {quests.Count}");
                        foreach (var q in quests)
                            Console.WriteLine($"  - {q.QuestId}: {q.Title} ({q.Objectives.Count} objective(s))");
                    }
                }
                catch (Exception ex)
                {
                    Common.WriteError(parseResult, $"Error listing available quests: {ex.Message}");
                }
            });

            // quest accept <worldId> <questId>
            var acceptCmd = new Command("accept", "Accept (start) a quest");
            var acceptWorldArg = new Argument<string>("worldId", "World ID whose narrative to act on");
            var questIdArg = new Argument<string>("questId", "Quest ID to accept");
            acceptCmd.AddArgument(acceptWorldArg);
            acceptCmd.AddArgument(questIdArg);
            acceptCmd.SetHandler(async (InvocationContext ctx) =>
            {
                var parseResult = ctx.ParseResult;
                var worldId = parseResult.GetValueForArgument(acceptWorldArg);
                var questId = parseResult.GetValueForArgument(questIdArg);
                try
                {
                    await using var factory = new OrleansClientFactory();
                    var client = await factory.ConnectAsync();
                    var grain = await NarrativeStateResolver.ResolveForWorldAsync(client, worldId);
                    if (grain == null)
                    {
                        Common.WriteError(parseResult, $"No narrative associated with world '{worldId}' (or world not found)");
                        return;
                    }

                    var started = await grain.StartQuestAsync(questId);
                    if (started)
                        Common.WriteSuccess(parseResult, $"Accepted quest '{questId}' in world '{worldId}'");
                    else
                        Common.WriteError(parseResult, $"Could not accept quest '{questId}' (unknown, already active/completed, or prerequisites unmet)");
                }
                catch (Exception ex)
                {
                    Common.WriteError(parseResult, $"Error accepting quest: {ex.Message}");
                }
            });

            // quest log <worldId>
            var logCmd = new Command("log", "Show active and completed quests");
            var logWorldArg = new Argument<string>("worldId", "World ID whose narrative to act on");
            logCmd.AddArgument(logWorldArg);
            logCmd.SetHandler(async (InvocationContext ctx) =>
            {
                var parseResult = ctx.ParseResult;
                var worldId = parseResult.GetValueForArgument(logWorldArg);
                try
                {
                    await using var factory = new OrleansClientFactory();
                    var client = await factory.ConnectAsync();
                    var grain = await NarrativeStateResolver.ResolveForWorldAsync(client, worldId);
                    if (grain == null)
                    {
                        Common.WriteError(parseResult, $"No narrative associated with world '{worldId}' (or world not found)");
                        return;
                    }

                    var state = await grain.GetStateAsync();
                    var active = await grain.GetActiveQuestsAsync();
                    var completed = state?.CompletedQuestIds.ToList() ?? new System.Collections.Generic.List<string>();

                    if (Common.IsJsonOutput(parseResult))
                    {
                        Common.WriteOutput(parseResult, new
                        {
                            success = true,
                            worldId,
                            active = active.Select(q => new
                            {
                                q.QuestId,
                                q.Title,
                                objectives = q.Objectives.Select(o => new
                                {
                                    o.ObjectiveId,
                                    o.Type,
                                    completed = state != null
                                        && state.CompletedObjectives.TryGetValue(q.QuestId, out var cs)
                                        && cs.Contains(o.ObjectiveId)
                                })
                            }),
                            completed
                        });
                    }
                    else
                    {
                        Console.WriteLine($"Quest log for '{worldId}':");
                        Console.WriteLine($"  Active ({active.Count}):");
                        foreach (var q in active)
                            Console.WriteLine($"    - {q.QuestId}: {q.Title}");
                        Console.WriteLine($"  Completed ({completed.Count}):");
                        foreach (var id in completed)
                            Console.WriteLine($"    - {id}");
                    }
                }
                catch (Exception ex)
                {
                    Common.WriteError(parseResult, $"Error reading quest log: {ex.Message}");
                }
            });

            questCmd.AddCommand(availableCmd);
            questCmd.AddCommand(acceptCmd);
            questCmd.AddCommand(logCmd);
            root.AddCommand(questCmd);
        }
    }
}
