using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Aetherium.Server.MultiWorld;
using Orleans;

namespace Aetherium.Server.Agents.Tools.MultiWorld
{
    /// <summary>
    /// Tool for registering a world with a cluster.
    /// Requires world_generate capability.
    /// </summary>
    [AgentTool("registerworldtocluster", "Register an existing world with a cluster for shared economy and portals",
        Categories = new[] { "multiworld", "worldbuilding", "cluster_management" },
        RequiredCapabilities = new[] { "world_generate" })]
    public class RegisterWorldToClusterTool : IAgentTool
    {
        public string ToolId => "registerworldtocluster";
        public string Description => "Register an existing world with a cluster to enable shared economy and portal networks";
        public IEnumerable<string> Categories => new[] { "multiworld", "worldbuilding", "cluster_management" };
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
                        Description = "ID of the cluster to register with"
                    },
                    ["worldId"] = new()
                    {
                        Type = "string",
                        Description = "ID of the world to register"
                    }
                },
                Required = new() { "clusterId", "worldId" }
            };
        }

        public async Task<ToolExecutionResult> ExecuteAsync(ToolExecutionContext context, Dictionary<string, object> args)
        {
            if (!context.HasCapability("world_generate"))
                return ToolExecutionResult.Error("Missing required capability: world_generate");

            if (!args.TryGetValue("clusterId", out var clusterIdObj) || string.IsNullOrWhiteSpace(clusterIdObj?.ToString()))
                return ToolExecutionResult.Error("Missing or invalid clusterId parameter");

            if (!args.TryGetValue("worldId", out var worldIdObj) || string.IsNullOrWhiteSpace(worldIdObj?.ToString()))
                return ToolExecutionResult.Error("Missing or invalid worldId parameter");

            var clusterId = clusterIdObj.ToString()!;
            var worldId = worldIdObj.ToString()!;

            try
            {
                var grainFactory = context.ServiceProvider.GetService(typeof(IGrainFactory)) as IGrainFactory;
                if (grainFactory == null)
                    return ToolExecutionResult.Error("Unable to access grain factory");

                var clusterGrain = grainFactory.GetGrain<IClusterGrain>(clusterId);
                await clusterGrain.RegisterWorldAsync(worldId);

                return ToolExecutionResult.Ok($"World '{worldId}' registered to cluster '{clusterId}'", new Dictionary<string, object>
                {
                    { "clusterId", clusterId },
                    { "worldId", worldId }
                });
            }
            catch (Exception ex)
            {
                return ToolExecutionResult.Error($"Failed to register world to cluster: {ex.Message}");
            }
        }
    }
}

