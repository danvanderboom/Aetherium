using System.Collections.Generic;
using System.Threading.Tasks;
using Aetherium.Core;
using Aetherium.Components;

namespace Aetherium.Server.Agents.Tools.WorldBuilding
{
    /// <summary>
    /// Tool for spawning new entities in the world.
    /// Requires world_edit capability.
    /// </summary>
    [AgentTool("spawnentity", "Create a new entity at a specific location", 
        Categories = new[] { "worldbuilding", "entity_management" },
        RequiredCapabilities = new[] { "world_edit" })]
    public class SpawnEntityTool : IAgentTool
    {
        public string ToolId => "spawnentity";
        public string Description => "Create a new entity at specified coordinates with components";
        public IEnumerable<string> Categories => new[] { "worldbuilding", "entity_management" };
        public IEnumerable<string> RequiredCapabilities => new[] { "world_edit" };
        
        public ToolParameterSchema GetParameterSchema()
        {
            return new ToolParameterSchema
            {
                Properties = new Dictionary<string, ParameterDefinition>
                {
                    ["x"] = new()
                    {
                        Type = "number",
                        Description = "X coordinate"
                    },
                    ["y"] = new()
                    {
                        Type = "number",
                        Description = "Y coordinate"
                    },
                    ["z"] = new()
                    {
                        Type = "number",
                        Description = "Z coordinate (level)",
                        DefaultValue = 0
                    },
                    ["entityType"] = new()
                    {
                        Type = "string",
                        Description = "Type of entity to spawn (e.g., 'key', 'door', 'item')"
                    },
                    ["properties"] = new()
                    {
                        Type = "object",
                        Description = "Additional properties for the entity (JSON object)"
                    }
                },
                Required = new() { "x", "y", "entityType" }
            };
        }
        
        public async Task<ToolExecutionResult> ExecuteAsync(ToolExecutionContext context, Dictionary<string, object> args)
        {
            if (!context.HasCapability("world_edit"))
                return ToolExecutionResult.Error("Missing required capability: world_edit");
            
            // Validate parameters
            if (!args.TryGetValue("x", out var xObj) || !int.TryParse(xObj.ToString(), out var x))
                return ToolExecutionResult.Error("Invalid or missing x coordinate");
            
            if (!args.TryGetValue("y", out var yObj) || !int.TryParse(yObj.ToString(), out var y))
                return ToolExecutionResult.Error("Invalid or missing y coordinate");
            
            int z = 0;
            if (args.TryGetValue("z", out var zObj))
                int.TryParse(zObj.ToString(), out z);
            
            if (!args.TryGetValue("entityType", out var typeObj))
                return ToolExecutionResult.Error("Missing required parameter: entityType");
            
            var entityType = typeObj.ToString();
            if (string.IsNullOrWhiteSpace(entityType))
                return ToolExecutionResult.Error("Entity type cannot be empty");
            
            // Check if we have World context (WorldBuildingToolContext)
            if (context is not WorldBuildingToolContext worldContext)
                return ToolExecutionResult.Error("SpawnEntityTool requires WorldBuildingToolContext with World reference");
            
            // Create location
            var location = new WorldLocation(x, y, z);

            var world = worldContext.World;

            // Location must be passable terrain
            if (!world.PassableTerrain(location))
                return ToolExecutionResult.Error($"Location ({x}, {y}, {z}) is not passable");

            // Location must not already hold a character
            if (world.EntitiesByLocation.TryGetValue(location, out var entitiesAtLoc))
            {
                foreach (var existing in entitiesAtLoc.Values)
                {
                    if (existing is Aetherium.Character)
                        return ToolExecutionResult.Error($"Location ({x}, {y}, {z}) is already occupied");
                }
            }

            // Create the entity via the factory (creature types mirroring GameMapGrain.SpawnEntityAsync)
            var entity = Aetherium.Server.Entities.EntityFactory.TryCreate(entityType!, world);
            if (entity == null)
                return ToolExecutionResult.Error($"Unsupported entity type '{entityType}'. Supported: {string.Join(", ", Aetherium.Server.Entities.EntityFactory.SupportedTypes)}");

            entity.Set(location);
            world.AddEntity(entity);

            return ToolExecutionResult.Ok(
                $"Spawned '{entityType}' at ({x}, {y}, {z})",
                new Dictionary<string, object> { ["entityId"] = entity.EntityId });
        }
    }
}

