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

        // Entity resolution lives in the shared Aetherium.Entities.EntityFactory (also used by the
        // prefab stamper), so the spawnable-type set stays defined in exactly one place.

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

            var world = worldContext.World;
            var location = new WorldLocation(x, y, z);

            // Location must be passable terrain and not already hold a character. This applies to
            // creature types (see the World-dependent fallback below); general entities resolved via
            // SpawnableEntityFactory (items, doors, lights, ...) may legitimately share or occupy
            // non-passable tiles, so the check only gates the creature path.
            bool IsOccupiedByCharacter() =>
                world.EntitiesByLocation.TryGetValue(location, out var atLoc)
                && atLoc.Values.Any(e => e is Aetherium.Character);

            Entity entity;

            // SpawnableEntityFactory only discovers types with a public parameterless constructor,
            // so it cannot construct World-dependent creatures (Monster, Zombie, ...). Try it first
            // since it's the shared, general-purpose factory (items/doors/lights/etc.); fall back to
            // the creature factory — which also accepts creature aliases (wolf/bear/bandit) — for
            // types it can't reach.
            if (SpawnableEntityFactory.IsKnownType(entityType))
            {
                try
                {
                    SpawnableEntityFactory.TryCreate(entityType, out entity);
                }
                catch (Exception ex)
                {
                    return Task.FromResult(ToolExecutionResult.Error(
                        $"Failed to construct entity '{entityType}': {ex.Message}"));
                }
            }
            else
            {
                // Resolve the type before validating placement — an unknown type should fail as
                // "unknown type", not as a location error, regardless of what's at that location.
                var creature = Aetherium.Server.Entities.EntityFactory.TryCreate(entityType!, world);
                if (creature == null)
                {
                    var supported = string.Join(", ", SpawnableEntityFactory.SupportedTypeNames
                        .Concat(Aetherium.Server.Entities.EntityFactory.SupportedTypes).Distinct().OrderBy(n => n));
                    return Task.FromResult(ToolExecutionResult.Error(
                        $"Unknown entity type '{entityType}'. Supported types: {supported}"));
                }

                if (!world.PassableTerrain(location))
                    return Task.FromResult(ToolExecutionResult.Error($"Location ({x}, {y}, {z}) is not passable"));
                if (IsOccupiedByCharacter())
                    return Task.FromResult(ToolExecutionResult.Error($"Location ({x}, {y}, {z}) is already occupied"));

                entity = creature;
            }

            entity.Set(location);

            try
            {
                world.AddEntity(entity);
            }
            catch (Exception ex)
            {
                return Task.FromResult(ToolExecutionResult.Error(
                    $"Failed to add entity to world: {ex.Message}"));
            }

            var resolvedName = entity.GetType().Name;
            return Task.FromResult(ToolExecutionResult.Ok(
                $"Spawned {resolvedName} '{entity.EntityId}' at ({x}, {y}, {z})",
                new Dictionary<string, object>
                {
                    ["entityId"] = entity.EntityId,
                    ["entityType"] = resolvedName,
                    ["x"] = x,
                    ["y"] = y,
                    ["z"] = z
                }));
        }
    }
}
