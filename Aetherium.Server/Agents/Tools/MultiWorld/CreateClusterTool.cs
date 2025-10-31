using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Aetherium.Server.MultiWorld;
using Orleans;

namespace Aetherium.Server.Agents.Tools.MultiWorld
{
    /// <summary>
    /// Tool for creating a new world cluster.
    /// Requires world_generate capability.
    /// </summary>
    [AgentTool("createcluster", "Create a new world cluster for managing multiple worlds and shared economy",
        Categories = new[] { "multiworld", "worldbuilding", "cluster_management" },
        RequiredCapabilities = new[] { "world_generate" })]
    public class CreateClusterTool : IAgentTool
    {
        public string ToolId => "createcluster";
        public string Description => "Create a new world cluster that can contain multiple worlds with shared economy";
        public IEnumerable<string> Categories => new[] { "multiworld", "worldbuilding", "cluster_management" };
        public IEnumerable<string> RequiredCapabilities => new[] { "world_generate" };

        public ToolParameterSchema GetParameterSchema()
        {
            return new ToolParameterSchema
            {
                Properties = new Dictionary<string, ParameterDefinition>
                {
                    ["name"] = new()
                    {
                        Type = "string",
                        Description = "Name of the cluster"
                    },
                    ["description"] = new()
                    {
                        Type = "string",
                        Description = "Description of the cluster"
                    }
                },
                Required = new() { "name" }
            };
        }

        public async Task<ToolExecutionResult> ExecuteAsync(ToolExecutionContext context, Dictionary<string, object> args)
        {
            if (!context.HasCapability("world_generate"))
                return ToolExecutionResult.Error("Missing required capability: world_generate");

            if (!args.TryGetValue("name", out var nameObj) || string.IsNullOrWhiteSpace(nameObj?.ToString()))
                return ToolExecutionResult.Error("Missing or invalid name parameter");

            var name = nameObj.ToString()!;
            var description = args.TryGetValue("description", out var descObj) ? descObj?.ToString() ?? "" : "";

            try
            {
                var grainFactory = context.ServiceProvider.GetService(typeof(IGrainFactory)) as IGrainFactory;
                if (grainFactory == null)
                    return ToolExecutionResult.Error("Unable to access grain factory");

                var clusterId = $"cluster-{Guid.NewGuid():N}";
                var clusterGrain = grainFactory.GetGrain<IClusterGrain>(clusterId);

                var clusterInfo = new ClusterInfo
                {
                    ClusterId = clusterId,
                    Name = name,
                    Description = description,
                    CreatedAt = DateTime.UtcNow,
                    WorldIds = new HashSet<string>()
                };

                await clusterGrain.InitializeAsync(clusterInfo);

                return ToolExecutionResult.Ok($"Cluster '{name}' created with ID: {clusterId}", new Dictionary<string, object>
                {
                    { "clusterId", clusterId },
                    { "name", name }
                });
            }
            catch (Exception ex)
            {
                return ToolExecutionResult.Error($"Failed to create cluster: {ex.Message}");
            }
        }
    }
}

