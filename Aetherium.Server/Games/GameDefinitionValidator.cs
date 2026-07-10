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
            void Warn(string section, string message) => diagnostics.Add(new GameDefinitionDiagnostic
            {
                BundlePath = bundlePath,
                Section = section,
                Severity = GameDefinitionDiagnosticSeverity.Warning,
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
            var creatureIds = ToIdSet(definition.Content?.Creatures.Select(c => c.Id), "content", "creature id", Error);
            var itemIds = ToIdSet(definition.Content?.Items.Select(i => i.Id), "content", "item id", Error);

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

            // --- Content (add-content-definitions) ---
            if (definition.Content != null)
            {
                foreach (var creature in definition.Content.Creatures)
                {
                    if (creature.Health <= 0)
                        Error("content", $"Creature '{creature.Id}' has non-positive health ({creature.Health}).");
                    if (string.IsNullOrWhiteSpace(creature.Glyph))
                        Error("content", $"Creature '{creature.Id}' has an empty glyph.");
                    if (!Enum.TryParse<ConsoleColor>(creature.Color, ignoreCase: false, out _))
                        Error("content", $"Creature '{creature.Id}' color '{creature.Color}' is not a valid ConsoleColor name (e.g. Gray, DarkRed, Cyan).");
                    if (!Aetherium.Server.Content.ContentCatalog.BehaviorPresets.Contains(creature.Behavior))
                        Error("content", $"Creature '{creature.Id}' behavior '{creature.Behavior}' is not a known preset ({string.Join(", ", Aetherium.Server.Content.ContentCatalog.BehaviorPresets)}).");
                    if (creature.LootItemId is { } lootId && !itemIds.Contains(lootId))
                        Error("content", $"Creature '{creature.Id}' drops loot item '{lootId}', which is not a declared item.");
                }

                foreach (var spawn in definition.Content.Spawns)
                {
                    if (!creatureIds.Contains(spawn.CreatureId))
                        Error("content", $"Spawn table references unknown creature '{spawn.CreatureId}'.");
                    if (spawn.Weight <= 0)
                        Error("content", $"Spawn table entry '{spawn.CreatureId}' has non-positive weight ({spawn.Weight}).");
                }

                // Typo detector: a doctrine that judges kill:<x> where <x> is no defined creature
                // will never fire from creature kills in this game. Warning, not error — tags like
                // kill:player are legitimately outside the bestiary.
                if (definition.Factions != null)
                {
                    foreach (var faction in definition.Factions.Factions)
                    {
                        foreach (var tag in faction.DoctrineDeltas.Keys)
                        {
                            if (tag.StartsWith("kill:", StringComparison.Ordinal)
                                && tag["kill:".Length..] is { Length: > 0 } subject
                                && subject != "player"
                                && !creatureIds.Contains(subject))
                            {
                                Warn("content", $"Faction '{faction.Id}' doctrine judges '{tag}', but no creature '{subject}' is defined — the rule can never fire from this game's bestiary.");
                            }
                        }
                    }
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
