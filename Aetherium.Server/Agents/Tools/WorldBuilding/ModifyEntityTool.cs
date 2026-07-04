using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Aetherium.Core;
using Aetherium.Components;

namespace Aetherium.Server.Agents.Tools.WorldBuilding
{
    /// <summary>
    /// Tool for modifying an entity's components during world building.
    /// Supports adding and removing components by type name. Requires world_edit
    /// capability and a <see cref="WorldBuildingToolContext"/>.
    /// </summary>
    [AgentTool("modifyentity", "Modify an entity's components",
        Categories = new[] { "worldbuilding", "entity_management" },
        RequiredCapabilities = new[] { "world_edit" })]
    public class ModifyEntityTool : IAgentTool
    {
        public string ToolId => "modifyentity";
        public string Description => "Add or remove components on an existing entity";
        public IEnumerable<string> Categories => new[] { "worldbuilding", "entity_management" };
        public IEnumerable<string> RequiredCapabilities => new[] { "world_edit" };

        /// <summary>
        /// Core components that cannot be removed because the world indexes depend on them.
        /// </summary>
        private static readonly HashSet<string> ProtectedComponents =
            new(StringComparer.OrdinalIgnoreCase) { nameof(WorldLocation), nameof(Tile) };

        /// <summary>
        /// Map of addable component type name (lower-cased) to its concrete <see cref="Component"/>
        /// type: every non-abstract <see cref="Component"/> that is not itself an <see cref="Entity"/>
        /// and has a public parameterless constructor.
        /// </summary>
        private static readonly Lazy<IReadOnlyDictionary<string, Type>> ComponentTypes = new(() =>
            typeof(WorldLocation).Assembly.GetTypes()
                .Where(t => typeof(Component).IsAssignableFrom(t)
                            && !typeof(Entity).IsAssignableFrom(t)
                            && !t.IsAbstract
                            && t.GetConstructor(Type.EmptyTypes) != null)
                .ToDictionary(t => t.Name.ToLowerInvariant(), t => t));

        public ToolParameterSchema GetParameterSchema()
        {
            return new ToolParameterSchema
            {
                Properties = new Dictionary<string, ParameterDefinition>
                {
                    ["entityId"] = new() { Type = "string", Description = "Entity ID of the entity to modify" },
                    ["addComponents"] = new()
                    {
                        Type = "array",
                        Description = "Component type names to add (e.g., ['Carriable', 'Hidden'])"
                    },
                    ["removeComponents"] = new()
                    {
                        Type = "array",
                        Description = "Component type names to remove. Core components "
                                    + "(WorldLocation, Tile) cannot be removed."
                    }
                },
                Required = new() { "entityId" }
            };
        }

        public Task<ToolExecutionResult> ExecuteAsync(ToolExecutionContext context, Dictionary<string, object> args)
        {
            if (!context.HasCapability("world_edit"))
                return Task.FromResult(ToolExecutionResult.Error("Missing required capability: world_edit"));

            if (context is not WorldBuildingToolContext worldContext)
                return Task.FromResult(ToolExecutionResult.Error(
                    "ModifyEntityTool requires WorldBuildingToolContext with World reference"));

            if (!args.TryGetValue("entityId", out var entityIdObj))
                return Task.FromResult(ToolExecutionResult.Error("Missing required parameter: entityId"));

            var entityId = entityIdObj?.ToString();
            if (string.IsNullOrWhiteSpace(entityId))
                return Task.FromResult(ToolExecutionResult.Error("Entity ID cannot be empty"));

            if (!worldContext.World.Entities.TryGetValue(entityId, out var entity))
                return Task.FromResult(ToolExecutionResult.Error($"Entity not found: {entityId}"));

            var toAdd = args.TryGetValue("addComponents", out var addObj) ? ParseStringList(addObj) : new List<string>();
            var toRemove = args.TryGetValue("removeComponents", out var remObj) ? ParseStringList(remObj) : new List<string>();

            if (toAdd.Count == 0 && toRemove.Count == 0)
                return Task.FromResult(ToolExecutionResult.Error(
                    "No modifications specified. Provide 'addComponents' and/or 'removeComponents'."));

            var added = new List<string>();
            var removed = new List<string>();

            // Removals first so an add of a fresh instance of the same type wins if both are requested.
            foreach (var name in toRemove)
            {
                if (ProtectedComponents.Contains(name))
                    return Task.FromResult(ToolExecutionResult.Error(
                        $"Cannot remove protected component '{name}'"));

                foreach (var key in entity.Components.Keys.Where(k =>
                    string.Equals(k.Name, name, StringComparison.OrdinalIgnoreCase)).ToList())
                {
                    if (entity.Components.TryRemove(key, out _))
                        removed.Add(key.Name);
                }
            }

            foreach (var name in toAdd)
            {
                if (!ComponentTypes.Value.TryGetValue(name.ToLowerInvariant(), out var clrType))
                {
                    var supported = string.Join(", ", ComponentTypes.Value.Values.Select(t => t.Name).OrderBy(n => n));
                    return Task.FromResult(ToolExecutionResult.Error(
                        $"Unknown component type '{name}'. Supported types: {supported}"));
                }

                Component component;
                try
                {
                    component = (Component)Activator.CreateInstance(clrType)!;
                }
                catch (Exception ex)
                {
                    return Task.FromResult(ToolExecutionResult.Error(
                        $"Failed to construct component '{clrType.Name}': {ex.Message}"));
                }

                component.Parent = entity;
                entity.Components[clrType] = component;
                added.Add(clrType.Name);
            }

            var summary = $"Modified entity {entityId}: added [{string.Join(", ", added)}], removed [{string.Join(", ", removed)}]";
            return Task.FromResult(ToolExecutionResult.Ok(summary, new Dictionary<string, object>
            {
                ["entityId"] = entityId,
                ["added"] = added,
                ["removed"] = removed
            }));
        }

        /// <summary>
        /// Flexibly parses a tool argument into a list of strings. Accepts a JSON array
        /// (<see cref="JsonElement"/>), any non-string enumerable, or a single scalar value.
        /// </summary>
        private static List<string> ParseStringList(object? value)
        {
            var result = new List<string>();
            switch (value)
            {
                case null:
                    break;
                case string s:
                    if (!string.IsNullOrWhiteSpace(s)) result.Add(s.Trim());
                    break;
                case JsonElement json when json.ValueKind == JsonValueKind.Array:
                    foreach (var item in json.EnumerateArray())
                    {
                        var str = item.ValueKind == JsonValueKind.String ? item.GetString() : item.ToString();
                        if (!string.IsNullOrWhiteSpace(str)) result.Add(str!.Trim());
                    }
                    break;
                case IEnumerable enumerable:
                    foreach (var item in enumerable)
                    {
                        var str = item?.ToString();
                        if (!string.IsNullOrWhiteSpace(str)) result.Add(str!.Trim());
                    }
                    break;
                default:
                    var single = value.ToString();
                    if (!string.IsNullOrWhiteSpace(single)) result.Add(single!.Trim());
                    break;
            }
            return result;
        }
    }
}
