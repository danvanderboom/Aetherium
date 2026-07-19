using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Aetherium.Server.MultiWorld;
using Aetherium.Server.Vehicles;
using Orleans;

namespace Aetherium.Server.Agents.Tools.Interaction
{
    /// <summary>
    /// Boards an adjacent boardable vehicle (add-boardable-vehicles Phase 2): the caller targets the
    /// vehicle's exterior entity, and — if they are within reach — their session is re-pointed into the
    /// vehicle interior. Auto-discovered; the "interaction" category makes it available to the Player
    /// profile. The hub validates via the map grain (a pure read) then calls the vehicle grain directly,
    /// so the map grain is never called re-entrantly during the board.
    /// </summary>
    [AgentTool("board", "Board an adjacent vehicle you can see",
        Categories = new[] { "interaction" }, RequiredCapabilities = new[] { "interaction" })]
    public class BoardTool : IAgentTool
    {
        public string ToolId => "board";
        public string Description => "Board an adjacent boardable vehicle by its entity id";
        public IEnumerable<string> Categories => new[] { "interaction" };
        public IEnumerable<string> RequiredCapabilities => new[] { "interaction" };

        public ToolParameterSchema GetParameterSchema() => new()
        {
            Properties = new Dictionary<string, ParameterDefinition>
            {
                ["targetEntityId"] = new() { Type = "string", Description = "Entity id of the vehicle exterior to board" }
            },
            Required = new() { "targetEntityId" }
        };

        public async Task<ToolExecutionResult> ExecuteAsync(ToolExecutionContext context, Dictionary<string, object> args)
        {
            if (!context.HasCapability("interaction"))
                return ToolExecutionResult.Error("Missing capability: interaction");

            if (!args.TryGetValue("targetEntityId", out var targetObj) || string.IsNullOrWhiteSpace(targetObj?.ToString()))
                return ToolExecutionResult.Error("Missing required parameter: targetEntityId");
            var targetEntityId = targetObj.ToString()!;

            var mapId = context.Session?.MapId;
            if (string.IsNullOrEmpty(mapId))
                return ToolExecutionResult.Error("No map context for the current session");

            if (context.ServiceProvider.GetService(typeof(IGrainFactory)) is not IGrainFactory grainFactory)
                return ToolExecutionResult.Error("Unable to access grain factory");

            // Pure read on the map grain: is this a boardable vehicle, and is the caller adjacent?
            var mapGrain = grainFactory.GetGrain<IGameMapGrain>(mapId);
            var info = await mapGrain.GetBoardableInfoAsync(context.SessionId, targetEntityId);
            if (!info.Found || string.IsNullOrEmpty(info.VehicleInstanceId))
                return ToolExecutionResult.Error("That is not a boardable vehicle");
            if (!info.InReach)
                return ToolExecutionResult.Error("You are too far from the vehicle to board");

            // Call the vehicle grain directly — it leaves the caller from this map and re-points their
            // session into the interior (never calling back into this map grain mid-await).
            var vehicle = grainFactory.GetGrain<IVehicleGrain>(info.VehicleInstanceId);
            var result = await vehicle.BoardAsync(new List<string> { context.SessionId });
            if (!result.Success)
                return ToolExecutionResult.Error(result.Error ?? "Could not board the vehicle");
            if (result.Moved == 0)
                return ToolExecutionResult.Error("Could not board (the vehicle may be full)");

            var name = string.IsNullOrEmpty(info.DisplayName) ? "the vehicle" : info.DisplayName;
            return ToolExecutionResult.Ok($"Boarded {name}.",
                new Dictionary<string, object> { ["vehicleId"] = info.VehicleInstanceId });
        }
    }
}
