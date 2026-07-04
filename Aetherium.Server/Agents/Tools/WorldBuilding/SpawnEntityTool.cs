using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Aetherium.Core;
using Aetherium.Components;
using Aetherium.Entities;

namespace Aetherium.Server.Agents.Tools.WorldBuilding
{
    /// <summary>
    /// Tool for spawning new entities in the world.
    /// Requires world_edit capability and a <see cref="WorldBuildingToolContext"/>.
    /// </summary>
    [AgentTool("spawnentity", "Create a new entity at a specific location",
        Categories = new[] { "worldbuilding", "entity_management" },
        RequiredCapabilities = new[] { "world_edit" })]
    public class SpawnEntityTool : IAgentTool
    {
        public string ToolId => "spawnentity";
        public string Description => "Create a new entity at specified coordinates";
        public IEnumerable<string> Categories => new[] { "worldbuilding", "entity_management" };
        public IEnumerable<string> RequiredCapabilities => new[] { "world_edit" };

        /// <summary>
        /// Map of spawnable entity type name (lower-cased) to its concrete <see cref="Entity"/> type.
        /// Discovered by reflection from the assembly that defines the entity hierarchy: every
        /// non-abstract <see cref="Entity"/> subclass with a public parameterless constructor.
        /// <see cref="Terrain"/> is excluded automatically (it has no parameterless constructor and
        /// is created via <c>setterrain</c>).
        /// </summary>
        private static readonly Lazy<IReadOnlyDictionary<string, Type>> SpawnableTypes = new(() =>
            typeof(Item).Assembly.GetTypes()
                .Where(t => typeof(Entity).IsAssignableFrom(t)
                            && !t.IsAbstract
                            && t.GetConstructor(Type.EmptyTypes) != null)
                .ToDictionary(t => t.Name.ToLowerInvariant(), t => t));

        public ToolParameterSchema GetParameterSchema()
        {
            return new ToolParameterSchema
            {
                Properties = new Dictionary<string, ParameterDefinition>
                {
                    ["x"] = new() { Type = "number", Description = "X coordinate" },
                    ["y"] = new() { Type = "number", Description = "Y coordinate" },
                    ["z"] = new() { Type = "number", Description = "Z coordinate (level)", DefaultValue = 0 },
                    ["entityType"] = new()
                    {
                        Type = "string",
                        Description = "Type of entity to spawn (e.g., 'Item', 'Door', 'LightEntity'). "
                                    + "Case-insensitive; must match a concrete entity type."
                    }
                },
                Required = new() { "x", "y", "entityType" }
            };
        }

        public Task<ToolExecutionResult> ExecuteAsync(ToolExecutionContext context, Dictionary<string, object> args)
        {
            if (!context.HasCapability("world_edit"))
                return Task.FromResult(ToolExecutionResult.Error("Missing required capability: world_edit"));

            if (context is not WorldBuildingToolContext worldContext)
                return Task.FromResult(ToolExecutionResult.Error(
                    "SpawnEntityTool requires WorldBuildingToolContext with World reference"));

            if (!args.TryGetValue("x", out var xObj) || !int.TryParse(xObj?.ToString(), out var x))
                return Task.FromResult(ToolExecutionResult.Error("Invalid or missing x coordinate"));

            if (!args.TryGetValue("y", out var yObj) || !int.TryParse(yObj?.ToString(), out var y))
                return Task.FromResult(ToolExecutionResult.Error("Invalid or missing y coordinate"));

            int z = 0;
            if (args.TryGetValue("z", out var zObj))
                int.TryParse(zObj?.ToString(), out z);

            if (!args.TryGetValue("entityType", out var typeObj))
                return Task.FromResult(ToolExecutionResult.Error("Missing required parameter: entityType"));

            var entityType = typeObj?.ToString();
            if (string.IsNullOrWhiteSpace(entityType))
                return Task.FromResult(ToolExecutionResult.Error("Entity type cannot be empty"));

            if (!SpawnableTypes.Value.TryGetValue(entityType.ToLowerInvariant(), out var clrType))
            {
                var supported = string.Join(", ", SpawnableTypes.Value.Values.Select(t => t.Name).OrderBy(n => n));
                return Task.FromResult(ToolExecutionResult.Error(
                    $"Unknown entity type '{entityType}'. Supported types: {supported}"));
            }

            Entity entity;
            try
            {
                entity = (Entity)Activator.CreateInstance(clrType)!;
            }
            catch (Exception ex)
            {
                return Task.FromResult(ToolExecutionResult.Error(
                    $"Failed to construct entity '{clrType.Name}': {ex.Message}"));
            }

            var location = new WorldLocation(x, y, z);
            entity.Set(location);

            try
            {
                worldContext.World.AddEntity(entity);
            }
            catch (Exception ex)
            {
                return Task.FromResult(ToolExecutionResult.Error(
                    $"Failed to add entity to world: {ex.Message}"));
            }

            return Task.FromResult(ToolExecutionResult.Ok(
                $"Spawned {clrType.Name} '{entity.EntityId}' at ({x}, {y}, {z})",
                new Dictionary<string, object>
                {
                    ["entityId"] = entity.EntityId,
                    ["entityType"] = clrType.Name,
                    ["x"] = x,
                    ["y"] = y,
                    ["z"] = z
                }));
        }
    }
}
