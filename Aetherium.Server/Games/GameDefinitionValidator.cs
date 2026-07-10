using System;
using System.Collections.Generic;
using System.Linq;
using Aetherium.Model.Games;

namespace Aetherium.Server.Games
{
    /// <summary>
    /// Validates a parsed <see cref="GameDefinition"/> beyond deserialization
    /// (openspec/changes/add-game-definition-loader): structural sanity plus the cross-section
    /// references that a per-section parse can't see — a skill unlocking an ability that doesn't
    /// exist, an XP rule feeding an undeclared pool, a relation naming an unknown faction. Every
    /// finding is a <see cref="GameDefinitionDiagnostic"/> with section context; a definition with
    /// any error-severity finding must not be registered or instantiated.
    /// </summary>
    public class GameDefinitionValidator
    {
        public List<GameDefinitionDiagnostic> Validate(GameDefinition definition, string bundlePath = "")
        {
            var diagnostics = new List<GameDefinitionDiagnostic>();
            void Error(string section, string message) => diagnostics.Add(new GameDefinitionDiagnostic
            {
                BundlePath = bundlePath,
                Section = section,
                Severity = GameDefinitionDiagnosticSeverity.Error,
                Message = message,
            });

            // --- Structural (manifest) ---
            if (string.IsNullOrWhiteSpace(definition.Id))
                Error("manifest", "Game definition 'id' is required.");
            if (string.IsNullOrWhiteSpace(definition.Name))
                Error("manifest", "Game definition 'name' is required.");
            if (string.IsNullOrWhiteSpace(definition.Version) || !System.Version.TryParse(definition.Version, out _))
                Error("manifest", $"Game definition 'version' must be a parseable version (got '{definition.Version}').");
            if (string.IsNullOrWhiteSpace(definition.World.GeneratorType))
                Error("world", "'world.generatorType' is required.");
            if (definition.World.Size is { } size && (size.Width <= 0 || size.Height <= 0 || size.Depth <= 0))
                Error("world", $"'world.size' dimensions must be positive (got {size.Width}x{size.Height}x{size.Depth}).");
            if (definition.World.MaxPlayers <= 0)
                Error("world", $"'world.maxPlayers' must be positive (got {definition.World.MaxPlayers}).");

            // --- Section-local id uniqueness ---
            var abilityIds = ToIdSet(definition.Abilities?.Abilities.Select(a => a.Id), "abilities", "ability id", Error);
            var poolTags = ToIdSet(definition.Abilities?.CharacterResourcePools.Select(p => p.Tag), "abilities", "resource pool tag", Error);
            var progressPoolIds = ToIdSet(definition.Progression?.Pools.Select(p => p.Id), "progression", "progress pool id", Error);
            var skillIds = ToIdSet(definition.Progression?.Skills.Select(s => s.Id), "progression", "skill id", Error);
            var factionIds = ToIdSet(definition.Factions?.Factions.Select(f => f.Id), "factions", "faction id", Error);
            ToIdSet(definition.Factions?.Bands.Select(b => b.Id), "factions", "band id", Error);

            // --- Cross-section references ---
            if (definition.Abilities != null)
            {
                foreach (var ability in definition.Abilities.Abilities)
                {
                    if (!string.IsNullOrEmpty(ability.ResourcePoolTag) && !poolTags.Contains(ability.ResourcePoolTag))
                        Error("abilities", $"Ability '{ability.Id}' costs resource pool '{ability.ResourcePoolTag}', which is not a declared characterResourcePools tag.");
                }
            }

            if (definition.Progression != null)
            {
                foreach (var skill in definition.Progression.Skills)
                {
                    if (!string.IsNullOrEmpty(skill.UnlocksAbilityId) && !abilityIds.Contains(skill.UnlocksAbilityId))
                        Error("progression", $"Skill '{skill.Id}' unlocks ability '{skill.UnlocksAbilityId}', which is not a declared ability.");
                    if (!string.IsNullOrEmpty(skill.RequiredPoolId) && !progressPoolIds.Contains(skill.RequiredPoolId))
                        Error("progression", $"Skill '{skill.Id}' requires pool '{skill.RequiredPoolId}', which is not a declared progress pool.");
                    foreach (var prerequisite in skill.Prerequisites.Where(p => !skillIds.Contains(p)))
                        Error("progression", $"Skill '{skill.Id}' lists prerequisite '{prerequisite}', which is not a declared skill.");
                }

                foreach (var rule in definition.Progression.XpAwardRules)
                {
                    if (!progressPoolIds.Contains(rule.PoolId))
                        Error("progression", $"XP award rule ({rule.OnEvent}) feeds pool '{rule.PoolId}', which is not a declared progress pool.");
                }
            }

            if (definition.Factions != null)
            {
                foreach (var relation in definition.Factions.Relations)
                {
                    if (!factionIds.Contains(relation.FromFactionId))
                        Error("factions", $"Relation references unknown faction '{relation.FromFactionId}' (fromFactionId).");
                    if (!factionIds.Contains(relation.ToFactionId))
                        Error("factions", $"Relation references unknown faction '{relation.ToFactionId}' (toFactionId).");
                }
            }

            return diagnostics;
        }

        private static HashSet<string> ToIdSet(IEnumerable<string>? ids, string section, string what,
            Action<string, string> error)
        {
            var set = new HashSet<string>(StringComparer.Ordinal);
            foreach (var id in ids ?? Enumerable.Empty<string>())
            {
                if (string.IsNullOrWhiteSpace(id))
                    error(section, $"A {what} is empty.");
                else if (!set.Add(id))
                    error(section, $"Duplicate {what} '{id}'.");
            }
            return set;
        }
    }
}
