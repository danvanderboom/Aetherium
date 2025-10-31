using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Aetherium.Server.MultiWorld;
using Orleans;

namespace Aetherium.Server.Agents.Tools.MultiWorld
{
    /// <summary>
    /// Tool for scheduling transports along trade routes.
    /// Requires world_generate capability.
    /// </summary>
    [AgentTool("scheduletransport", "Schedule a transport along a trade route to move resources between markets",
        Categories = new[] { "multiworld", "worldbuilding", "economy" },
        RequiredCapabilities = new[] { "world_generate" })]
    public class ScheduleTransportTool : IAgentTool
    {
        public string ToolId => "scheduletransport";
        public string Description => "Schedule a transport along a trade route to move resources between markets";
        public IEnumerable<string> Categories => new[] { "multiworld", "worldbuilding", "economy" };
        public IEnumerable<string> RequiredCapabilities => new[] { "world_generate" };

        public ToolParameterSchema GetParameterSchema()
        {
            return new ToolParameterSchema
            {
                Properties = new Dictionary<string, ParameterDefinition>
                {
                    ["clusterId"] = new()
                    {
                        Type = "string",
                        Description = "ID of the cluster"
                    },
                    ["routeId"] = new()
                    {
                        Type = "string",
                        Description = "ID of the trade route"
                    },
                    ["cargo"] = new()
                    {
                        Type = "object",
                        Description = "Dictionary of resource types to quantities (e.g., {'ore': 50, 'food': 30})"
                    },
                    ["departureTime"] = new()
                    {
                        Type = "string",
                        Description = "ISO 8601 datetime string for departure (optional, defaults to now)"
                    }
                },
                Required = new() { "clusterId", "routeId", "cargo" }
            };
        }

        public async Task<ToolExecutionResult> ExecuteAsync(ToolExecutionContext context, Dictionary<string, object> args)
        {
            if (!context.HasCapability("world_generate"))
                return ToolExecutionResult.Error("Missing required capability: world_generate");

            if (!args.TryGetValue("clusterId", out var clusterIdObj) || string.IsNullOrWhiteSpace(clusterIdObj?.ToString()))
                return ToolExecutionResult.Error("Missing or invalid clusterId parameter");

            if (!args.TryGetValue("routeId", out var routeIdObj) || string.IsNullOrWhiteSpace(routeIdObj?.ToString()))
                return ToolExecutionResult.Error("Missing or invalid routeId parameter");

            if (!args.TryGetValue("cargo", out var cargoObj))
                return ToolExecutionResult.Error("Missing or invalid cargo parameter");

            var clusterId = clusterIdObj.ToString()!;
            var routeId = routeIdObj.ToString()!;

            var cargo = new Dictionary<string, int>();
            if (cargoObj is Dictionary<string, object> cargoDict)
            {
                foreach (var kvp in cargoDict)
                {
                    if (int.TryParse(kvp.Value?.ToString(), out var quantity))
                        cargo[kvp.Key] = quantity;
                }
            }

            if (cargo.Count == 0)
                return ToolExecutionResult.Error("Cargo must contain at least one resource type");

            var departureTime = DateTime.UtcNow;
            if (args.TryGetValue("departureTime", out var timeObj) && timeObj != null)
            {
                if (DateTime.TryParse(timeObj.ToString(), out var parsedTime))
                    departureTime = parsedTime;
            }

            try
            {
                var grainFactory = context.ServiceProvider.GetService(typeof(IGrainFactory)) as IGrainFactory;
                if (grainFactory == null)
                    return ToolExecutionResult.Error("Unable to access grain factory");

                var clusterGrain = grainFactory.GetGrain<IClusterGrain>(clusterId);

                // Get route to validate it exists
                // (In full implementation, we'd fetch the route first)
                var route = new TradeRoute { RouteId = routeId };

                var schedule = await clusterGrain.ScheduleTransportAsync(route, cargo, departureTime);

                return ToolExecutionResult.Ok($"Transport scheduled with ID: {schedule.ScheduleId}", new Dictionary<string, object>
                {
                    { "scheduleId", schedule.ScheduleId },
                    { "routeId", routeId },
                    { "departureTime", departureTime.ToString("O") },
                    { "arrivalTime", schedule.ArrivalTime.ToString("O") }
                });
            }
            catch (Exception ex)
            {
                return ToolExecutionResult.Error($"Failed to schedule transport: {ex.Message}");
            }
        }
    }
}

