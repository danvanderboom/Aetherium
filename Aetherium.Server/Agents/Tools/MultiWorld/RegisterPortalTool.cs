using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Aetherium.Server.MultiWorld;
using Orleans;

namespace Aetherium.Server.Agents.Tools.MultiWorld
{
    /// <summary>
    /// Tool for registering a portal with a cluster for link resolution.
    /// Requires world_generate capability.
    /// </summary>
    [AgentTool("registerportal", "Register a portal with a cluster for cross-world travel",
        Categories = new[] { "multiworld", "worldbuilding", "portal_management" },
        RequiredCapabilities = new[] { "world_generate" })]
    public class RegisterPortalTool : IAgentTool
    {
        public string ToolId => "registerportal";
        public string Description => "Register a portal with a cluster so it can resolve target destinations";
        public IEnumerable<string> Categories => new[] { "multiworld", "worldbuilding", "portal_management" };
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
                    ["portalId"] = new()
                    {
                        Type = "string",
                        Description = "Unique ID for the portal"
                    },
                    ["sourceWorldId"] = new()
                    {
                        Type = "string",
                        Description = "World ID where the portal is located"
                    },
                    ["sourceMapId"] = new()
                    {
                        Type = "string",
                        Description = "Map ID where the portal is located"
                    },
                    ["targetTag"] = new()
                    {
                        Type = "string",
                        Description = "Target tag for link resolution (e.g., 'hub', 'city', 'dungeon')"
                    },
                    ["targetWorldId"] = new()
                    {
                        Type = "string",
                        Description = "Target world ID (if known, otherwise use targetTag)"
                    },
                    ["targetMapId"] = new()
                    {
                        Type = "string",
                        Description = "Target map ID (if known)"
                    }
                },
                Required = new() { "clusterId", "portalId", "sourceWorldId", "sourceMapId" }
            };
        }

        public async Task<ToolExecutionResult> ExecuteAsync(ToolExecutionContext context, Dictionary<string, object> args)
        {
            if (!context.HasCapability("world_generate"))
                return ToolExecutionResult.Error("Missing required capability: world_generate");

            if (!args.TryGetValue("clusterId", out var clusterIdObj) || string.IsNullOrWhiteSpace(clusterIdObj?.ToString()))
                return ToolExecutionResult.Error("Missing or invalid clusterId parameter");

            if (!args.TryGetValue("portalId", out var portalIdObj) || string.IsNullOrWhiteSpace(portalIdObj?.ToString()))
                return ToolExecutionResult.Error("Missing or invalid portalId parameter");

            if (!args.TryGetValue("sourceWorldId", out var sourceWorldIdObj) || string.IsNullOrWhiteSpace(sourceWorldIdObj?.ToString()))
                return ToolExecutionResult.Error("Missing or invalid sourceWorldId parameter");

            if (!args.TryGetValue("sourceMapId", out var sourceMapIdObj) || string.IsNullOrWhiteSpace(sourceMapIdObj?.ToString()))
                return ToolExecutionResult.Error("Missing or invalid sourceMapId parameter");

            var clusterId = clusterIdObj.ToString()!;
            var portalId = portalIdObj.ToString()!;
            var sourceWorldId = sourceWorldIdObj.ToString()!;
            var sourceMapId = sourceMapIdObj.ToString()!;
            var targetTag = args.TryGetValue("targetTag", out var tagObj) ? tagObj?.ToString() : null;
            var targetWorldId = args.TryGetValue("targetWorldId", out var worldObj) ? worldObj?.ToString() : null;
            var targetMapId = args.TryGetValue("targetMapId", out var mapObj) ? mapObj?.ToString() : null;

            try
            {
                var grainFactory = context.ServiceProvider.GetService(typeof(IGrainFactory)) as IGrainFactory;
                if (grainFactory == null)
                    return ToolExecutionResult.Error("Unable to access grain factory");

                var clusterGrain = grainFactory.GetGrain<IClusterGrain>(clusterId);

                var portalLink = new PortalLink
                {
                    PortalId = portalId,
                    SourceWorldId = sourceWorldId,
                    SourceMapId = sourceMapId,
                    TargetWorldId = targetWorldId,
                    TargetMapId = targetMapId,
                    TargetTag = targetTag,
                    IsResolved = !string.IsNullOrEmpty(targetWorldId)
                };

                await clusterGrain.RegisterPortalAsync(portalLink);

                return ToolExecutionResult.Ok($"Portal '{portalId}' registered to cluster '{clusterId}'", new Dictionary<string, object>
                {
                    { "clusterId", clusterId },
                    { "portalId", portalId },
                    { "sourceWorldId", sourceWorldId },
                    { "sourceMapId", sourceMapId }
                });
            }
            catch (Exception ex)
            {
                return ToolExecutionResult.Error($"Failed to register portal: {ex.Message}");
            }
        }
    }
}

