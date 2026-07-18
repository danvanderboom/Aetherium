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
    /// <summary>
    /// Character memory inspection (see OpenSpec change add-character-memory).
    /// </summary>
    public static class MemoryCommands
    {
        public static void AddToRoot(RootCommand root)
        {
            var memoryCmd = new Command("memory", "Inspect a character's accumulated memories");

            var getCmd = new Command("get", "Get the memories recorded for a session's character");
            var sessionIdArg = new Argument<string>("sessionId", "Game session ID");
            getCmd.AddArgument(sessionIdArg);
            getCmd.SetHandler(async (InvocationContext ctx) =>
            {
                try
                {
                    var parseResult = ctx.ParseResult;
                    var sessionId = parseResult.GetValueForArgument(sessionIdArg);

                    await using var factory = new OrleansClientFactory();
                    await factory.ConnectAsync();
                    var mgmt = factory.GetGameManagement();
                    var json = await mgmt.GetMemoryAsync(sessionId);

                    if (string.IsNullOrEmpty(json))
                    {
                        Common.WriteError(parseResult, $"No memory for session '{sessionId}' (unknown session, or operator access disabled).");
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
                        Console.WriteLine($"Memory for session {sessionId}:");
                        Console.WriteLine($"  Locations tracked: {r.GetProperty("LocationsTracked").GetInt32()}");
                        Console.WriteLine($"  Memories: {r.GetProperty("TotalMemories").GetInt32()}");
                        Console.WriteLine($"  Impressions: {r.GetProperty("TotalImpressions").GetInt32()}");

                        var memories = r.GetProperty("Memories").EnumerateArray()
                            .OrderByDescending(m => m.GetProperty("EffectiveStrength").GetDouble())
                            .Take(25)
                            .ToList();
                        if (memories.Count > 0)
                        {
                            Console.WriteLine($"  Strongest (top {memories.Count}):");
                            foreach (var m in memories)
                            {
                                var loc = m.GetProperty("Location");
                                var permanent = m.TryGetProperty("Permanent", out var permProp) && permProp.GetBoolean();
                                var stability = m.TryGetProperty("StabilitySeconds", out var stabProp) ? stabProp.GetDouble() : 0;
                                Console.WriteLine(
                                    $"    ({loc.GetProperty("X").GetInt32()},{loc.GetProperty("Y").GetInt32()},{loc.GetProperty("Z").GetInt32()}) " +
                                    $"{m.GetProperty("ContentType").GetString()}={m.GetProperty("Content").GetString()} " +
                                    $"eff={m.GetProperty("EffectiveStrength").GetDouble():F2} x{m.GetProperty("Impressions").GetInt32()}{FormatDurability(permanent, stability)}");
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Common.WriteError(ctx.ParseResult, $"Failed to get memory: {ex.Message}");
                }
            });

            memoryCmd.AddCommand(getCmd);
            root.AddCommand(memoryCmd);
        }

        /// <summary>
        /// Formats a memory's durability suffix for the table view (add-memory-dynamics): a permanent
        /// memory never fades; otherwise, once reinforcement has grown its stability past the world
        /// default, its own half-life is shown in hours. A stability-0 memory has no suffix.
        /// </summary>
        public static string FormatDurability(bool permanent, double stabilitySeconds)
        {
            if (permanent)
                return " [permanent]";
            if (stabilitySeconds > 0)
                return $" stab={stabilitySeconds / 3600.0:F1}h";
            return string.Empty;
        }
    }
}
