using System.Collections.Generic;
using System.Threading.Tasks;
using Aetherium.Core;
using Aetherium.Components;

namespace Aetherium.Server.Agents.Tools.WorldBuilding
{
    /// <summary>
    /// Tool for setting terrain at specific coordinates.
    /// Requires world_edit capability.
    /// </summary>
    [AgentTool("setterrain", "Set terrain type at specified coordinates", 
        Categories = new[] { "worldbuilding", "terrain_management" },
        RequiredCapabilities = new[] { "world_edit" })]
    public class SetTerrainTool : IAgentTool
    {
        public string ToolId => "setterrain";
        public string Description => "Set terrain type at specified coordinates";
        public IEnumerable<string> Categories => new[] { "worldbuilding", "terrain_management" };
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
                    ["terrainType"] = new()
                    {
                        Type = "string",
                        Description = "Type of terrain to set (e.g., 'Plains', 'Forest', 'Water', 'Mountain')"
                    }
                },
                Required = new() { "x", "y", "terrainType" }
            };
        }
        
        public async Task<ToolExecutionResult> ExecuteAsync(ToolExecutionContext context, Dictionary<string, object> args)
        {
            if (!context.HasCapability("world_edit"))
                return ToolExecutionResult.Error("Missing required capability: world_edit");
            
            // Check if we have World context (WorldBuildingToolContext)
            if (context is not WorldBuildingToolContext worldContext)
                return ToolExecutionResult.Error("SetTerrainTool requires WorldBuildingToolContext with World reference");
            
            // Validate parameters
            if (!args.TryGetValue("x", out var xObj) || !int.TryParse(xObj.ToString(), out var x))
                return ToolExecutionResult.Error("Invalid or missing x coordinate");
            
            if (!args.TryGetValue("y", out var yObj) || !int.TryParse(yObj.ToString(), out var y))
                return ToolExecutionResult.Error("Invalid or missing y coordinate");
            
            int z = 0;
            if (args.TryGetValue("z", out var zObj))
                int.TryParse(zObj.ToString(), out z);
            
            if (!args.TryGetValue("terrainType", out var typeObj))
                return ToolExecutionResult.Error("Missing required parameter: terrainType");
            
            var terrainType = typeObj.ToString();
            if (string.IsNullOrWhiteSpace(terrainType))
                return ToolExecutionResult.Error("Terrain type cannot be empty");
            
            // Check if terrain type is registered
            if (!worldContext.World.TerrainTypes.ContainsKey(terrainType))
                return ToolExecutionResult.Error($"Terrain type '{terrainType}' is not registered in this world");
            
            // Set terrain at location
            var location = new WorldLocation(x, y, z);
            try
            {
                worldContext.World.SetTerrain(terrainType, location);
                return ToolExecutionResult.Ok($"Set terrain '{terrainType}' at ({x}, {y}, {z})");
            }
            catch (System.Exception ex)
            {
                return ToolExecutionResult.Error($"Failed to set terrain: {ex.Message}");
            }
        }
    }
}

