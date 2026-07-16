using System;
using System.Collections.Generic;
using System.Linq;
using Aetherium.Model.Games;
using Aetherium.Model.Eca;
using Aetherium.Server.Eca;

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
            // Topology (docs/grid-topologies.md): omitted → square; otherwise must be a
            // registered tiling. The registry knows only "square" today; P1/P2 register
            // "hex"/"tri" and this check accepts them automatically.
            if (!string.IsNullOrWhiteSpace(definition.World.Topology)
                && !Aetherium.Topology.GridTopologyRegistry.TryGet(definition.World.Topology, out _))
                Error("world", $"'world.topology' '{definition.World.Topology}' is not a known tiling ({string.Join(", ", Aetherium.Topology.GridTopologyRegistry.Names)}).");

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

            // --- Rules (add-eca-scripting), validated against the reflectable EcaVocabulary ---
            if (definition.Rules != null)
            {
                ToIdSet(definition.Rules.Rules.Select(r => r.Id), "rules", "rule id", Error);

                foreach (var rule in definition.Rules.Rules)
                {
                    if (!EcaVocabulary.TryGet(rule.When, out var trigger) || trigger.Role != EcaTileRole.Trigger)
                        Error("rules", $"Rule '{rule.Id}' has unknown trigger '{rule.When}'.");

                    foreach (var condition in rule.If)
                        ValidateTile(rule.Id, condition.Kind, EcaTileRole.Condition,
                            EcaParamValues(condition), creatureIds, Error, Warn);

                    foreach (var action in rule.Do)
                        ValidateTile(rule.Id, action.Kind, EcaTileRole.Action,
                            EcaParamValues(action), creatureIds, Error, Warn);
                }
            }

            return diagnostics;
        }

        /// <summary>
        /// Validates one condition/action tile against its <see cref="EcaTileDefinition"/> — the
        /// registry is the source of truth, so this loop is generic over parameter metadata (required
        /// presence, CreatureRef/StatusRef resolution) rather than a per-kind switch. Only the two
        /// numeric range guards this slice needs are keyed on the specific tile+parameter.
        /// </summary>
        private static void ValidateTile(string ruleId, string kind, EcaTileRole expectedRole,
            IReadOnlyDictionary<string, object?> values, HashSet<string> creatureIds,
            Action<string, string> error, Action<string, string> warn)
        {
            if (!EcaVocabulary.TryGet(kind, out var def) || def.Role != expectedRole)
            {
                error("rules", $"Rule '{ruleId}' uses unknown {expectedRole.ToString().ToLowerInvariant()} '{kind}'.");
                return;
            }

            foreach (var p in def.Parameters)
            {
                values.TryGetValue(p.Name, out var value);

                // Required-presence is only meaningful for string-ish parameters — a required number
                // like probability legitimately allows 0 and is covered by its range check below.
                bool isStringish = p.ValueType is EcaValueType.Text or EcaValueType.CreatureRef
                    or EcaValueType.StatusRef or EcaValueType.EnumChoice;
                if (p.Required && isStringish && string.IsNullOrEmpty(value as string))
                {
                    error("rules", $"Rule '{ruleId}' {kind} is missing required parameter '{p.Name}'.");
                    continue;
                }

                switch (p.ValueType)
                {
                    // An action referencing a missing creature will fail at runtime (error); a
                    // condition referencing one simply never matches (warning) — the typo detector.
                    case EcaValueType.CreatureRef when value is string cref && cref.Length > 0 && !creatureIds.Contains(cref):
                        if (def.Role == EcaTileRole.Action)
                            error("rules", $"Rule '{ruleId}' {kind} references creature '{cref}', which is not defined in content.");
                        else
                            warn("rules", $"Rule '{ruleId}' {kind} references creature '{cref}', which is not defined in content — it can never match.");
                        break;

                    case EcaValueType.StatusRef when value is string sref && !p.EnumChoices.Contains(sref):
                        error("rules", $"Rule '{ruleId}' {kind} references status '{sref}', which is not one of: {string.Join(", ", p.EnumChoices)}.");
                        break;
                }
            }

            if (kind == ChanceCondition.Id && values.TryGetValue(ChanceCondition.ProbabilityParam, out var prob)
                && prob is double probability && (probability < 0 || probability > 1))
                error("rules", $"Rule '{ruleId}' chance probability {probability} is outside [0, 1].");
            if (kind == DealDamageAction.Id && values.TryGetValue(DealDamageAction.AmountParam, out var amt)
                && amt is double amount && amount <= 0)
                error("rules", $"Rule '{ruleId}' deal_damage amount {amount} must be greater than 0.");
        }

        /// <summary>Adapts a condition descriptor's typed fields to a param-name→value map — the one
        /// place that knows the flat descriptor's shape, so <see cref="ValidateTile"/> stays generic.</summary>
        private static IReadOnlyDictionary<string, object?> EcaParamValues(EcaConditionDescriptor c) => c.Kind switch
        {
            CreatureTypeIsCondition.Id => new Dictionary<string, object?>
                { [CreatureTypeIsCondition.CreatureTypeParam] = c.CreatureType },
            ChanceCondition.Id => new Dictionary<string, object?>
                { [ChanceCondition.ProbabilityParam] = c.Probability },
            _ => new Dictionary<string, object?>(),
        };

        private static IReadOnlyDictionary<string, object?> EcaParamValues(EcaActionDescriptor a) => a.Kind switch
        {
            SpawnCreatureAction.Id => new Dictionary<string, object?>
            {
                [SpawnCreatureAction.CreatureIdParam] = a.CreatureId,
                [SpawnCreatureAction.OffsetXParam] = a.OffsetX,
                [SpawnCreatureAction.OffsetYParam] = a.OffsetY,
            },
            DealDamageAction.Id => new Dictionary<string, object?>
            {
                [DealDamageAction.AmountParam] = a.Amount,
                [DealDamageAction.DamageTypeParam] = a.DamageType,
            },
            ApplyStatusAction.Id => new Dictionary<string, object?>
            {
                [ApplyStatusAction.StatusIdParam] = a.StatusId,
                [ApplyStatusAction.DurationTicksParam] = a.DurationTicks,
                [ApplyStatusAction.MagnitudeParam] = a.Magnitude,
            },
            _ => new Dictionary<string, object?>(),
        };

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
