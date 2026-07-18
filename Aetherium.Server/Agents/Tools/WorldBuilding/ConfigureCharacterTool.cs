using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Aetherium.Components;

namespace Aetherium.Server.Agents.Tools.WorldBuilding
{
    /// <summary>
    /// Sets a character's memory and recognition profile at runtime (add-identity-recognition), so an
    /// operator can make an NPC forgetful or sharp-eyed on a live world. Creates the
    /// <see cref="MemoryProfile"/> / <see cref="RecognitionProfile"/> components on demand and updates
    /// only the fields supplied. Requires world_edit and a <see cref="WorldBuildingToolContext"/>.
    /// </summary>
    [AgentTool("configurecharacter", "Set a character's memory/recognition profile",
        Categories = new[] { "worldbuilding", "entity_management" },
        RequiredCapabilities = new[] { "world_edit" })]
    public class ConfigureCharacterTool : IAgentTool
    {
        public string ToolId => "configurecharacter";
        public string Description => "Set a character's memory and recognition profile fields by entity ID";
        public IEnumerable<string> Categories => new[] { "worldbuilding", "entity_management" };
        public IEnumerable<string> RequiredCapabilities => new[] { "world_edit" };

        public ToolParameterSchema GetParameterSchema()
        {
            return new ToolParameterSchema
            {
                Properties = new Dictionary<string, ParameterDefinition>
                {
                    ["entityId"] = new() { Type = "string", Description = "Entity ID of the character to configure" },
                    ["halfLifeMultiplier"] = new() { Type = "number", Description = "MemoryProfile: scales decay half-life (<1 forgetful, >1 sharp)" },
                    ["stabilityGrowthMultiplier"] = new() { Type = "number", Description = "MemoryProfile: scales stability growth on reinforcement" },
                    ["maxLocationsOverride"] = new() { Type = "number", Description = "MemoryProfile: per-character location cap" },
                    ["recognitionEnabled"] = new() { Type = "boolean", Description = "RecognitionProfile: participate as a recognizer" },
                    ["recognitionRange"] = new() { Type = "number", Description = "RecognitionProfile: recognition range in tiles" },
                    ["ownKindAcuity"] = new() { Type = "number", Description = "RecognitionProfile: acuity toward own kind" },
                    ["otherKindAcuity"] = new() { Type = "number", Description = "RecognitionProfile: acuity toward other kinds" }
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
                    "ConfigureCharacterTool requires WorldBuildingToolContext with World reference"));

            if (!args.TryGetValue("entityId", out var entityIdObj) || string.IsNullOrWhiteSpace(entityIdObj?.ToString()))
                return Task.FromResult(ToolExecutionResult.Error("Missing required parameter: entityId"));

            var entityId = entityIdObj!.ToString()!;
            if (!worldContext.World.Entities.TryGetValue(entityId, out var entity))
                return Task.FromResult(ToolExecutionResult.Error($"Entity not found: {entityId}"));

            var changed = new List<string>();

            // --- MemoryProfile ---
            if (HasAny(args, "halfLifeMultiplier", "stabilityGrowthMultiplier", "maxLocationsOverride"))
            {
                var mem = entity.Has<MemoryProfile>() ? entity.Get<MemoryProfile>() : Attach(entity, new MemoryProfile());
                if (TryDouble(args, "halfLifeMultiplier", out var hlm)) { mem.HalfLifeMultiplier = hlm; changed.Add($"halfLifeMultiplier={hlm}"); }
                if (TryDouble(args, "stabilityGrowthMultiplier", out var sgm)) { mem.StabilityGrowthMultiplier = sgm; changed.Add($"stabilityGrowthMultiplier={sgm}"); }
                if (TryInt(args, "maxLocationsOverride", out var mlo)) { mem.MaxLocationsOverride = mlo; changed.Add($"maxLocationsOverride={mlo}"); }
            }

            // --- RecognitionProfile ---
            if (HasAny(args, "recognitionEnabled", "recognitionRange", "ownKindAcuity", "otherKindAcuity"))
            {
                var rec = entity.Has<RecognitionProfile>() ? entity.Get<RecognitionProfile>() : Attach(entity, new RecognitionProfile());
                if (TryBool(args, "recognitionEnabled", out var re)) { rec.EnabledOverride = re; changed.Add($"recognitionEnabled={re}"); }
                if (TryInt(args, "recognitionRange", out var rr)) { rec.RangeTilesOverride = rr; changed.Add($"recognitionRange={rr}"); }
                if (TryDouble(args, "ownKindAcuity", out var oka)) { rec.OwnKindAcuityOverride = oka; changed.Add($"ownKindAcuity={oka}"); }
                if (TryDouble(args, "otherKindAcuity", out var otka)) { rec.OtherKindAcuityOverride = otka; changed.Add($"otherKindAcuity={otka}"); }
            }

            if (changed.Count == 0)
                return Task.FromResult(ToolExecutionResult.Error(
                    "No profile fields supplied. Provide at least one memory or recognition field."));

            return Task.FromResult(ToolExecutionResult.Ok(
                $"Configured {entityId}: {string.Join(", ", changed)}",
                new Dictionary<string, object> { ["entityId"] = entityId, ["changed"] = changed }));
        }

        private static T Attach<T>(Aetherium.Core.Entity entity, T component) where T : Aetherium.Core.Component
        {
            entity.Set(component);
            return component;
        }

        private static bool HasAny(Dictionary<string, object> args, params string[] keys) =>
            keys.Any(k => args.ContainsKey(k));

        private static bool TryDouble(Dictionary<string, object> args, string key, out double value)
        {
            value = 0;
            return args.TryGetValue(key, out var o) && o != null
                && double.TryParse(o.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out value);
        }

        private static bool TryInt(Dictionary<string, object> args, string key, out int value)
        {
            value = 0;
            return args.TryGetValue(key, out var o) && o != null
                && int.TryParse(o.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out value);
        }

        private static bool TryBool(Dictionary<string, object> args, string key, out bool value)
        {
            value = false;
            return args.TryGetValue(key, out var o) && o != null && bool.TryParse(o.ToString(), out value);
        }
    }
}
