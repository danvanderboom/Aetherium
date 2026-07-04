using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Aetherctl.Orleans;
using Aetherium.Server.Narrative;
using Aetherium.Model.Narrative;

namespace Aetherctl.Commands
{
    public static class NarrativeCommands
    {
        public static void AddToRoot(RootCommand root)
        {
            var narrativeCmd = new Command("narrative", "Manage game narratives");

            var createCmd = new Command("create", "Create a new narrative");
            var idArg = new Argument<string>("narrativeId", "Narrative ID");
            var nameArg = new Argument<string>("name", "Narrative name");
            var descArg = new Argument<string>("description", "Narrative description");
            createCmd.AddArgument(idArg);
            createCmd.AddArgument(nameArg);
            createCmd.AddArgument(descArg);
            createCmd.SetHandler(async (InvocationContext ctx) =>
            {
                try
                {
                    var parseResult = ctx.ParseResult;
                    var id = parseResult.GetValueForArgument(idArg);
                    var name = parseResult.GetValueForArgument(nameArg);
                    var desc = parseResult.GetValueForArgument(descArg);
                    await using var factory = new OrleansClientFactory();
                    await factory.ConnectAsync();
                    var narrative = factory.GetNarrative(id);
                    var definition = new NarrativeDefinition
                    {
                        NarrativeId = id,
                        Name = name,
                        Description = desc
                    };
                    await narrative.SetNarrativeAsync(definition);
                    Common.WriteSuccess(parseResult, $"Created narrative: {id}");
                }
                catch (Exception ex)
                {
                    Common.WriteError(ctx.ParseResult, $"Error creating narrative: {ex.Message}");
                }
            });

            var loadCmd = new Command("load", "Load narrative from JSON file");
            var fileArg = new Argument<string>("file", "Path to narrative JSON file");
            loadCmd.AddArgument(fileArg);
            loadCmd.SetHandler(async (InvocationContext ctx) =>
            {
                try
                {
                    var parseResult = ctx.ParseResult;
                    var file = parseResult.GetValueForArgument(fileArg);
                    if (!File.Exists(file))
                    {
                        Common.WriteError(parseResult, $"File not found: {file}");
                        return;
                    }
                    var json = await File.ReadAllTextAsync(file);
                    var definition = JsonSerializer.Deserialize<NarrativeDefinition>(json, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
                    if (definition == null || string.IsNullOrEmpty(definition.NarrativeId))
                    {
                        Common.WriteError(parseResult, "Invalid narrative file: missing NarrativeId");
                        return;
                    }
                    await using var factory = new OrleansClientFactory();
                    await factory.ConnectAsync();
                    var narrative = factory.GetNarrative(definition.NarrativeId);
                    await narrative.SetNarrativeAsync(definition);
                    if (Common.IsJsonOutput(parseResult))
                    {
                        Common.WriteOutput(parseResult, new
                        {
                            success = true,
                            narrativeId = definition.NarrativeId,
                            name = definition.Name,
                            quests = definition.Quests.Count,
                            lootTables = definition.LootTables.Count,
                            npcGoals = definition.NPCGoals.Count
                        });
                    }
                    else
                    {
                        Console.WriteLine($"✓ Loaded narrative: {definition.NarrativeId}");
                        Console.WriteLine($"  Name: {definition.Name}");
                        Console.WriteLine($"  Quests: {definition.Quests.Count}");
                        Console.WriteLine($"  Loot Tables: {definition.LootTables.Count}");
                        Console.WriteLine($"  NPC Goals: {definition.NPCGoals.Count}");
                    }
                }
                catch (Exception ex)
                {
                    Common.WriteError(ctx.ParseResult, $"Error loading narrative: {ex.Message}");
                }
            });

            var showCmd = new Command("show", "Show narrative details");
            var showIdArg = new Argument<string>("narrativeId", "Narrative ID");
            showCmd.AddArgument(showIdArg);
            showCmd.SetHandler(async (InvocationContext ctx) =>
            {
                try
                {
                    var parseResult = ctx.ParseResult;
                    var id = parseResult.GetValueForArgument(showIdArg);
                    await using var factory = new OrleansClientFactory();
                    await factory.ConnectAsync();
                    var narrative = factory.GetNarrative(id);
                    var definition = await narrative.GetNarrativeAsync();
                    if (definition == null)
                    {
                        Common.WriteError(parseResult, $"Narrative {id} not found");
                        return;
                    }
                    if (Common.IsJsonOutput(parseResult))
                    {
                        Common.WriteOutput(parseResult, new
                        {
                            success = true,
                            narrativeId = definition.NarrativeId,
                            name = definition.Name,
                            description = definition.Description,
                            quests = definition.Quests.Select(q => new { q.QuestId, q.Title, objectives = q.Objectives.Count }),
                            lootTables = definition.LootTables.Select(kvp => new { name = kvp.Key, entries = kvp.Value.Entries.Count }),
                            npcGoals = definition.NPCGoals.Select(g => new { g.GoalId, g.NPCType, g.GoalType })
                        });
                    }
                    else
                    {
                        Console.WriteLine($"Narrative: {definition.Name}");
                        Console.WriteLine($"  ID: {definition.NarrativeId}");
                        Console.WriteLine($"  Description: {definition.Description}");
                        Console.WriteLine($"  Quests: {definition.Quests.Count}");
                        if (definition.Quests.Count > 0)
                        {
                            Console.WriteLine($"\n  Quest List:");
                            foreach (var quest in definition.Quests)
                            {
                                Console.WriteLine($"    - {quest.Title} ({quest.QuestId})");
                                Console.WriteLine($"      Objectives: {quest.Objectives.Count}");
                            }
                        }
                        Console.WriteLine($"\n  Loot Tables: {definition.LootTables.Count}");
                        foreach (var kvp in definition.LootTables)
                        {
                            Console.WriteLine($"    - {kvp.Key}: {kvp.Value.Entries.Count} entries");
                        }
                        Console.WriteLine($"\n  NPC Goals: {definition.NPCGoals.Count}");
                        foreach (var goal in definition.NPCGoals)
                        {
                            Console.WriteLine($"    - {goal.GoalId} ({goal.NPCType}): {goal.GoalType}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Common.WriteError(ctx.ParseResult, $"Error showing narrative: {ex.Message}");
                }
            });

            var deleteCmd = new Command("delete", "Delete a narrative");
            var deleteIdArg = new Argument<string>("narrativeId", "Narrative ID");
            deleteCmd.AddArgument(deleteIdArg);
            deleteCmd.SetHandler(async (InvocationContext ctx) =>
            {
                try
                {
                    var parseResult = ctx.ParseResult;
                    var id = parseResult.GetValueForArgument(deleteIdArg);
                    await using var factory = new OrleansClientFactory();
                    await factory.ConnectAsync();
                    var narrative = factory.GetNarrative(id);
                    await narrative.DeleteAsync();
                    Common.WriteSuccess(parseResult, $"Deleted narrative: {id}");
                }
                catch (Exception ex)
                {
                    Common.WriteError(ctx.ParseResult, $"Error deleting narrative: {ex.Message}");
                }
            });

            var listCmd = new Command("list", "List all narratives");
            listCmd.SetHandler(async (InvocationContext ctx) =>
            {
                try
                {
                    var parseResult = ctx.ParseResult;
                    await using var factory = new OrleansClientFactory();
                    await factory.ConnectAsync();
                    // Note: Narrative listing not yet implemented - need server-side API
                    if (Common.IsJsonOutput(parseResult))
                    {
                        Common.WriteOutput(parseResult, new { success = true, count = 0, narratives = Array.Empty<string>() });
                    }
                    else
                    {
                        Console.WriteLine("Narrative listing not yet implemented. Use 'narrative show <id>' to check individual narratives.");
                    }
                }
                catch (Exception ex)
                {
                    Common.WriteError(ctx.ParseResult, $"Error listing narratives: {ex.Message}");
                }
            });

            narrativeCmd.AddCommand(createCmd);
            narrativeCmd.AddCommand(loadCmd);
            narrativeCmd.AddCommand(showCmd);
            narrativeCmd.AddCommand(deleteCmd);
            narrativeCmd.AddCommand(listCmd);
            root.AddCommand(narrativeCmd);
        }
    }
}

