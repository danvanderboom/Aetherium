using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Threading.Tasks;
using Aetherctl.Orleans;

namespace Aetherctl.Commands
{
    /// <summary>
    /// Operator-facing combat observability (P3-7 slice 2): read a map's rolling combat
    /// analytics (monsters defeated + total damage dealt). Talks to the map grain directly.
    /// </summary>
    public static class CombatCommands
    {
        public static void AddToRoot(RootCommand root)
        {
            var combatCmd = new Command("combat", "Inspect combat state");

            // combat stats <mapId>
            var statsCmd = new Command("stats", "Show a map's combat analytics (kills + damage)");
            var mapArg = new Argument<string>("mapId", "Map ID to inspect");
            statsCmd.AddArgument(mapArg);
            statsCmd.SetHandler(async (InvocationContext ctx) =>
            {
                var parseResult = ctx.ParseResult;
                var mapId = parseResult.GetValueForArgument(mapArg);
                try
                {
                    await using var factory = new OrleansClientFactory();
                    await factory.ConnectAsync();
                    var stats = await factory.GetGameMap(mapId).GetCombatStatsAsync();
                    if (Common.IsJsonOutput(parseResult))
                        Common.WriteOutput(parseResult, new { success = true, mapId, stats.MonstersDefeated, stats.TotalDamageDealt });
                    else
                        Console.WriteLine($"Combat on '{mapId}': {stats.MonstersDefeated} monster(s) defeated, {stats.TotalDamageDealt} total damage dealt");
                }
                catch (Exception ex)
                {
                    Common.WriteError(parseResult, $"Error reading combat stats: {ex.Message}");
                }
            });

            combatCmd.AddCommand(statsCmd);
            root.AddCommand(combatCmd);
        }
    }
}
