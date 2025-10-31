using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Aetherium.Server.MultiWorld;
using Orleans;

namespace Aetherium.Server.Agents.Tools.MultiWorld
{
    /// <summary>
    /// Tool for creating trade routes between markets in a cluster.
    /// Requires world_generate capability.
    /// </summary>
    [AgentTool("createtraderoute", "Create a trade route between two markets for cross-world commerce",
        Categories = new[] { "multiworld", "worldbuilding", "economy" },
        RequiredCapabilities = new[] { "world_generate" })]
    public class CreateTradeRouteTool : IAgentTool
    {
        public string ToolId => "createtraderoute";
        public string Description => "Create a trade route between two markets in a cluster for transporting resources";
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
                    ["sourceWorldId"] = new()
                    {
                        Type = "string",
                        Description = "Source world ID"
                    },
                    ["sourceMapId"] = new()
                    {
                        Type = "string",
                        Description = "Source map ID"
                    },
                    ["destinationWorldId"] = new()
                    {
                        Type = "string",
                        Description = "Destination world ID"
                    },
                    ["destinationMapId"] = new()
                    {
                        Type = "string",
                        Description = "Destination map ID"
                    },
                    ["resourceTypes"] = new()
                    {
                        Type = "array",
                        Description = "List of resource types that can be transported (e.g., ['ore', 'food', 'tools'])"
                    },
                    ["capacity"] = new()
                    {
                        Type = "number",
                        Description = "Maximum cargo capacity per transport",
                        DefaultValue = 100
                    },
                    ["travelTimeHours"] = new()
                    {
                        Type = "number",
                        Description = "Travel time in hours",
                        DefaultValue = 1
                    }
                },
                Required = new() { "clusterId", "sourceWorldId", "sourceMapId", "destinationWorldId", "destinationMapId" }
            };
        }

        public async Task<ToolExecutionResult> ExecuteAsync(ToolExecutionContext context, Dictionary<string, object> args)
        {
            if (!context.HasCapability("world_generate"))
                return ToolExecutionResult.Error("Missing required capability: world_generate");

            if (!args.TryGetValue("clusterId", out var clusterIdObj) || string.IsNullOrWhiteSpace(clusterIdObj?.ToString()))
                return ToolExecutionResult.Error("Missing or invalid clusterId parameter");

            var clusterId = clusterIdObj.ToString()!;
            var sourceWorldId = args["sourceWorldId"]?.ToString() ?? "";
            var sourceMapId = args["sourceMapId"]?.ToString() ?? "";
            var destinationWorldId = args["destinationWorldId"]?.ToString() ?? "";
            var destinationMapId = args["destinationMapId"]?.ToString() ?? "";

            var resourceTypes = new List<string>();
            if (args.TryGetValue("resourceTypes", out var resourcesObj))
            {
                if (resourcesObj is List<object> resourcesList)
                    resourceTypes = resourcesList.Select(r => r.ToString()!).ToList();
                else if (resourcesObj is IEnumerable<string> resources)
                    resourceTypes = resources.ToList();
            }

            var capacity = 100;
            if (args.TryGetValue("capacity", out var capacityObj) && capacityObj != null)
                int.TryParse(capacityObj.ToString(), out capacity);

            var travelTimeHours = 1.0;
            if (args.TryGetValue("travelTimeHours", out var timeObj) && timeObj != null)
                double.TryParse(timeObj.ToString(), out travelTimeHours);

            try
            {
                var grainFactory = context.ServiceProvider.GetService(typeof(IGrainFactory)) as IGrainFactory;
                if (grainFactory == null)
                    return ToolExecutionResult.Error("Unable to access grain factory");

                var clusterGrain = grainFactory.GetGrain<IClusterGrain>(clusterId);

                // Ensure markets exist
                await clusterGrain.RegisterMapAsync(sourceWorldId, sourceMapId);
                await clusterGrain.RegisterMapAsync(destinationWorldId, destinationMapId);

                var sourceMarketId = $"{sourceWorldId}:{sourceMapId}";
                var destMarketId = $"{destinationWorldId}:{destinationMapId}";

                var route = new TradeRoute
                {
                    RouteId = $"route-{Guid.NewGuid():N}",
                    SourceMarketId = sourceMarketId,
                    DestinationMarketId = destMarketId,
                    ResourceTypes = resourceTypes,
                    Capacity = capacity,
                    TravelTime = TimeSpan.FromHours(travelTimeHours)
                };

                var createdRoute = await clusterGrain.CreateTradeRouteAsync(route);

                return ToolExecutionResult.Ok($"Trade route created from {sourceMarketId} to {destMarketId}", new Dictionary<string, object>
                {
                    { "routeId", createdRoute.RouteId },
                    { "sourceMarketId", sourceMarketId },
                    { "destinationMarketId", destMarketId },
                    { "capacity", capacity }
                });
            }
            catch (Exception ex)
            {
                return ToolExecutionResult.Error($"Failed to create trade route: {ex.Message}");
            }
        }
    }
}

