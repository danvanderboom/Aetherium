using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Linq;
using System.Threading.Tasks;
using Aetherctl.Orleans;

namespace Aetherctl.Commands
{
    /// <summary>
    /// Operator-facing commands for YAML-defined games (add-game-definition-loader): list the
    /// game definitions the server loaded from Data/Games bundles, create running instances of
    /// them, and list a game's instances. Talks to the management grain directly via Orleans.
    /// </summary>
    public static class GameCommands
    {
        public static void AddToRoot(RootCommand root)
        {
            var gameCmd = new Command("game", "List YAML-defined games and manage their running instances");

            // game list
            var listCmd = new Command("list", "List the game definitions loaded on the server");
            listCmd.SetHandler(async (InvocationContext ctx) =>
            {
                var parseResult = ctx.ParseResult;
                try
                {
                    await using var factory = new OrleansClientFactory();
                    await factory.ConnectAsync();
                    var definitions = await factory.GetGameManagement().ListGameDefinitionsAsync();
                    Common.WriteOutput(parseResult, definitions.Select(d => new
                    {
                        d.Id,
                        d.Name,
                        d.Version,
                        d.Description,
                        Tags = string.Join(",", d.Tags),
                    }).ToList());
                }
                catch (Exception ex)
                {
                    Common.WriteError(parseResult, $"Failed to list game definitions: {ex.Message}");
                }
            });
            gameCmd.AddCommand(listCmd);

            // game instances <gameId>
            var instancesCmd = new Command("instances", "List the running instances of a game definition");
            var instancesGameArg = new Argument<string>("gameId", "Game definition id");
            instancesCmd.AddArgument(instancesGameArg);
            instancesCmd.SetHandler(async (InvocationContext ctx) =>
            {
                var parseResult = ctx.ParseResult;
                var gameId = parseResult.GetValueForArgument(instancesGameArg);
                try
                {
                    await using var factory = new OrleansClientFactory();
                    await factory.ConnectAsync();
                    var instances = await factory.GetGameManagement().ListGameInstancesAsync(gameId);
                    Common.WriteOutput(parseResult, instances.Select(w => new
                    {
                        w.WorldId,
                        w.Name,
                        State = w.State.ToString(),
                        w.PlayerCount,
                        w.MaxPlayers,
                        w.GameDefinitionVersion,
                        w.CreatedAt,
                    }).ToList());
                }
                catch (Exception ex)
                {
                    Common.WriteError(parseResult, $"Failed to list game instances: {ex.Message}");
                }
            });
            gameCmd.AddCommand(instancesCmd);

            // game create <gameId> [--name <instanceName>]
            var createCmd = new Command("create", "Create a new running instance of a game definition");
            var createGameArg = new Argument<string>("gameId", "Game definition id");
            var nameOpt = new Option<string?>("--name", () => null, "Instance name (defaults to the game's name)");
            createCmd.AddArgument(createGameArg);
            createCmd.AddOption(nameOpt);
            createCmd.SetHandler(async (InvocationContext ctx) =>
            {
                var parseResult = ctx.ParseResult;
                var gameId = parseResult.GetValueForArgument(createGameArg);
                var name = parseResult.GetValueForOption(nameOpt);
                try
                {
                    await using var factory = new OrleansClientFactory();
                    await factory.ConnectAsync();
                    var result = await factory.GetGameManagement().CreateGameInstanceAsync(gameId, name);
                    if (!result.Success)
                    {
                        Common.WriteError(parseResult, result.Error ?? "Instance creation failed");
                        return;
                    }
                    Common.WriteOutput(parseResult, new { result.WorldId, GameDefinitionId = gameId });
                }
                catch (Exception ex)
                {
                    Common.WriteError(parseResult, $"Failed to create game instance: {ex.Message}");
                }
            });
            gameCmd.AddCommand(createCmd);

            root.AddCommand(gameCmd);
        }
    }
}
