using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Aetherctl.Orleans;

namespace Aetherctl.Commands
{
    /// <summary>
    /// Individual-recognition inspection (see OpenSpec change add-identity-recognition). Reads a
    /// character's known-individuals memory over the canonical world (PCs and NPCs alike).
    /// </summary>
    public static class RecognitionCommands
    {
        public static void AddToRoot(RootCommand root)
        {
            var recognitionCmd = new Command("recognition", "Inspect a character's individual-recognition memory");

            var getCmd = new Command("get", "Get the individuals a character recognizes");
            var worldIdArg = new Argument<string>("worldId", "World ID (or map ID)");
            var entityIdArg = new Argument<string>("entityId", "Character entity ID (PC or NPC)");
            getCmd.AddArgument(worldIdArg);
            getCmd.AddArgument(entityIdArg);
            getCmd.SetHandler(async (InvocationContext ctx) =>
            {
                try
                {
                    var parseResult = ctx.ParseResult;
                    var worldId = parseResult.GetValueForArgument(worldIdArg);
                    var entityId = parseResult.GetValueForArgument(entityIdArg);

                    await using var factory = new OrleansClientFactory();
                    await factory.ConnectAsync();
                    var mgmt = factory.GetGameManagement();
                    var json = await mgmt.GetRecognitionAsync(worldId, entityId);

                    if (string.IsNullOrEmpty(json))
                    {
                        Common.WriteError(parseResult, $"No recognition state for '{entityId}' in '{worldId}' (unknown world/entity, no recognition memory, or operator access disabled).");
                        return;
                    }

                    if (Common.IsJsonOutput(parseResult))
                    {
                        Console.WriteLine(json);
                    }
                    else
                    {
                        using var doc = JsonDocument.Parse(json);
                        var r = doc.RootElement;
                        Console.WriteLine($"Recognition for {entityId} ({r.GetProperty("Kind").GetString()}) in {worldId}:");
                        Console.WriteLine($"  Known individuals: {r.GetProperty("KnownCount").GetInt32()}");

                        var individuals = r.GetProperty("Individuals").EnumerateArray()
                            .OrderByDescending(m => m.GetProperty("EffectiveFamiliarity").GetDouble())
                            .Take(25)
                            .ToList();
                        if (individuals.Count > 0)
                        {
                            Console.WriteLine($"  Most familiar (top {individuals.Count}):");
                            foreach (var m in individuals)
                            {
                                var permanent = m.TryGetProperty("Permanent", out var permProp) && permProp.GetBoolean();
                                var suffix = permanent ? " [permanent]" : string.Empty;
                                Console.WriteLine(
                                    $"    {m.GetProperty("EntityId").GetString()} ({m.GetProperty("Kind").GetString()}) " +
                                    $"fam={m.GetProperty("EffectiveFamiliarity").GetDouble():F2} " +
                                    $"x{m.GetProperty("Encounters").GetInt32()}{suffix}");
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Common.WriteError(ctx.ParseResult, $"Failed to get recognition: {ex.Message}");
                }
            });

            recognitionCmd.AddCommand(getCmd);
            root.AddCommand(recognitionCmd);
        }
    }
}
