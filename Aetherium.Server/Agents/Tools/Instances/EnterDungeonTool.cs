using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Aetherium.Server.Instances;
using Aetherium.Server.Groups;
using Aetherium.Model.Instances;
using Aetherium.Model.Groups;
using Aetherium.Model.Worlds;
using Orleans;

namespace Aetherium.Server.Agents.Tools.Instances
{
    /// <summary>
    /// Enters a dungeon instance for the character's current world, solo or with a party.
    /// </summary>
    [AgentTool("enter_dungeon", "Enter a dungeon instance in this world (solo or with a party)",
        Categories = new[] { "instance" })]
    public class EnterDungeonTool : IAgentTool
    {
        public string ToolId => "enter_dungeon";
        public string Description => "Enter a dungeon instance by dungeon id; optionally with a party id so the whole party enters together";
        public IEnumerable<string> Categories => new[] { "instance" };
        public IEnumerable<string> RequiredCapabilities => Array.Empty<string>();

        public ToolParameterSchema GetParameterSchema() => new()
        {
            Properties = new Dictionary<string, ParameterDefinition>
            {
                ["dungeonId"] = new() { Type = "string", Description = "ID of the dungeon to enter" },
                ["partyId"] = new() { Type = "string", Description = "Optional party ID; when set, the whole party enters" }
            },
            Required = new() { "dungeonId" }
        };

        public async Task<ToolExecutionResult> ExecuteAsync(ToolExecutionContext context, Dictionary<string, object> args)
        {
            if (!args.TryGetValue("dungeonId", out var dungeonObj) || string.IsNullOrWhiteSpace(dungeonObj?.ToString()))
                return ToolExecutionResult.Error("Missing required parameter: dungeonId");

            var dungeonId = dungeonObj.ToString()!;
            var worldId = context.Session?.WorldId;
            if (string.IsNullOrEmpty(worldId))
                return ToolExecutionResult.Error("No world context for the current session");

            if (context.ServiceProvider.GetService(typeof(IGrainFactory)) is not IGrainFactory grainFactory)
                return ToolExecutionResult.Error("Unable to access grain factory");

            // Resolve the entering players: the whole party when a party id is given, else the caller.
            List<PlayerId> playerIds;
            PartyId? partyIdValue = null;
            if (args.TryGetValue("partyId", out var partyObj) && !string.IsNullOrWhiteSpace(partyObj?.ToString()))
            {
                var partyId = partyObj.ToString()!;
                partyIdValue = new PartyId(partyId);
                var party = grainFactory.GetGrain<IPartyGrain>(partyId);
                playerIds = await party.GetMemberIdsAsync();
                if (playerIds.Count == 0)
                    return ToolExecutionResult.Error($"Party '{partyId}' has no members");
            }
            else
            {
                playerIds = new List<PlayerId> { new PlayerId(context.SessionId) };
            }

            var allocator = grainFactory.GetGrain<IInstanceAllocatorGrain>(worldId);
            var result = await allocator.EnterAsync(new EnterInstanceRequest
            {
                WorldId = new WorldId(worldId),
                DungeonId = new DungeonId(dungeonId),
                PartyId = partyIdValue,
                PlayerIds = playerIds
            });

            if (!result.Success || !result.InstanceId.HasValue)
                return ToolExecutionResult.Error(result.ErrorMessage ?? "Enter failed");

            var instanceGrain = grainFactory.GetGrain<IDungeonInstanceGrain>(result.InstanceId.Value.Value);
            var mapId = await instanceGrain.GetMapIdAsync();

            return ToolExecutionResult.Ok(
                $"Entered dungeon '{dungeonId}' (instance {result.InstanceId.Value.Value})",
                new Dictionary<string, object>
                {
                    ["instanceId"] = result.InstanceId.Value.Value,
                    ["mapId"] = mapId ?? string.Empty
                });
        }
    }
}
