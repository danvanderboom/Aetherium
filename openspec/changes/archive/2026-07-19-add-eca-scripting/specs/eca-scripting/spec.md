# eca-scripting Specification (Delta)

## ADDED Requirements

### Requirement: ECA Rule Data Model

`Aetherium.Model.Eca.EcaConfig` SHALL define a list of `EcaRule`s, each with a stable `id`, one trigger
(`When`), an optional AND-ed condition list (`If`), and an ordered action list (`Do`), as pure
serializable per-world data threaded through `GameDefinition`, `CreateWorldRequest`, `WorldTemplate`,
and `WorldConfig` like the existing gameplay configs. Conditions and actions SHALL use the
`Kind` + per-kind-fields descriptor shape (no polymorphic hierarchy), mirroring `AbilityEffectDescriptor`.

**Verified by:** `GameDefinitionLoaderTests.LoadBundle_RulesSection_BindsTriggersConditionsActions`,
`GameDefinitionMapperTests.MapsEveryField_ToCreateWorldRequest`

#### Scenario: Bundle rules section binds

- **WHEN** a bundle declares rules in `rules.yaml` (or inline `rules:`)
- **THEN** `GameDefinition.Rules` holds every rule with its trigger, conditions, and actions, and the
  mapper carries it onto `CreateWorldRequest.EcaConfig`

### Requirement: Closed Rule Vocabulary

The trigger, condition, and action vocabularies SHALL be closed enums this slice: trigger
`creature_died`; conditions `creature_type_is`, `chance`; actions `spawn_creature`, `deal_damage`,
`apply_status`. Each action targets `killer` or `victim` where meaningful. The compiler SHALL reject any
unknown id.

**Verified by:** `EcaRuntimeTests.Evaluate_NoConditions_FiresOnTrigger`,
`EcaRuntimeTests.CreatureTypeIs_GatesByVictimType`

#### Scenario: A rule fires only for its creature type

- **WHEN** a rule triggers on `creature_died` with condition `creature_type_is: cult_acolyte`
- **THEN** it emits its actions when a cult_acolyte dies and emits nothing when a wolf dies

### Requirement: Reflectable Vocabulary Registry

Every trigger, condition, and action SHALL be a C# tile type exposing a static `EcaTileDefinition`
(id, role, description, typed parameter schema, valid targets) in pure metadata form. `EcaVocabulary`
SHALL enumerate every tile definition as an `Id → EcaTileDefinition` map, and it SHALL be the single
source of truth consumed by validation, documentation generation, and the runtime (which keys its
switch on the same tile-id constants).

**Verified by:** `EcaVocabularyTests.EveryTileHasUniqueIdAndParameters`,
`EcaVocabularyTests.RuntimeKinds_MatchVocabularyIds`

#### Scenario: Vocabulary is programmatically enumerable

- **WHEN** `EcaVocabulary.All` is read
- **THEN** it contains one `EcaTileDefinition` per shipped trigger/condition/action, each with a unique
  id, a role, a description, and a typed parameter list

### Requirement: ECA Validation

The validator SHALL reject: duplicate rule ids; unknown trigger/condition/action kinds; a
`spawn_creature` action naming a creature absent from the bundle's content (or when the bundle declares
no content); an `apply_status` action naming a status outside `{burning, slowed, prone}`; a `chance`
probability outside `[0, 1]`; a `deal_damage` amount ≤ 0. It SHALL warn when a `creature_type_is`
condition names a creature absent from the bundle's content.

**Verified by:** `GameDefinitionValidatorTests.Rule_DuplicateIds_AreErrors`,
`GameDefinitionValidatorTests.SpawnCreature_UnknownCreature_IsAnError`,
`GameDefinitionValidatorTests.ApplyStatus_UnknownStatus_IsAnError`,
`GameDefinitionValidatorTests.Chance_OutOfRange_IsAnError`,
`GameDefinitionValidatorTests.CreatureTypeIs_UnknownCreature_IsAWarning`

#### Scenario: Spawn action referencing an undefined creature

- **WHEN** a `spawn_creature` action names a creature id not present in the bundle's content
- **THEN** validation produces an Error diagnostic naming the rule and the missing creature id

### Requirement: Pure Evaluation, Grain Execution

`EcaRuntime.Evaluate` SHALL be a pure function from an `EcaEventContext` (trigger kind, victim creature
type, killer entity id, victim location) to an ordered list of resolved `EcaActionRequest`s, drawing
`chance` from an injected seeded RNG. `GameMapGrain` SHALL execute the requests after the engine's
built-in kill reactions, mutating the world and fanning out the same deltas the melee and ability paths
already emit.

**Verified by:** `EcaRuntimeTests.Chance_Zero_EmitsNothing_Chance_One_EmitsAction`,
`EcaRuntimeTests.Evaluate_ResolvesKillerAndVictimTargets`

#### Scenario: Chance gates deterministically

- **WHEN** a rule's only condition is `chance: 0.0`
- **THEN** evaluation emits no action requests; with `chance: 1.0` it emits the rule's actions

### Requirement: Rules React to Creature Death

In a world created with an `EcaConfig`, the `creature_died` event SHALL be raised when a monster is
defeated by a melee or ability kill — carrying the victim's creature type, the killer, and the death
location — and every matching rule's actions SHALL execute: `spawn_creature` materializing a
content-catalog creature at the death location, `deal_damage` routing through the map's damage pipeline,
`apply_status` applying a shipped status. A world with no `EcaConfig` SHALL behave exactly as today.

**Verified by:** `EcaInstanceTests.CreatureDeath_TriggersSpawnRule`,
`EcaInstanceTests.Rule_DealDamageToKiller_AppliesThroughPipeline`,
`EcaInstanceTests.NoEcaConfig_KillPathUnchanged`

#### Scenario: A fallen acolyte summons a wolf

- **WHEN** a player kills a `cult_acolyte` in a world whose rules include "on creature_died, if
  creature_type_is cult_acolyte, spawn_creature wolf"
- **THEN** a new `wolf` entity (its content-definition stats and glyph) appears at the acolyte's death
  location and is broadcast to the map

### Requirement: Rules Survive Reactivation

The per-world `EcaConfig` SHALL be captured in map state and recompiled on grain reactivation, so a
rehydrated world keeps its rules without re-running initialization.

**Verified by:** `EcaInstanceTests.Rules_RecompileOnReactivation`

#### Scenario: Rules persist across a grain restart

- **WHEN** a map grain with rules deactivates and reactivates
- **THEN** a subsequent creature death still fires the world's rules

### Requirement: Generated Language Documentation

`EcaVocabularyDoc.GenerateMarkdown()` SHALL render the vocabulary registry as a reference table, and
`docs/eca-scripting.md` SHALL embed that generated output between stable markers. A test SHALL
regenerate the section and assert it matches the committed file, so the documentation cannot drift from
the vocabulary.

**Verified by:** `EcaVocabularyDocTests.CommittedDoc_MatchesGeneratedVocabularyReference`

#### Scenario: Docs stay in sync with the code

- **WHEN** a tile's definition changes without the committed doc being regenerated
- **THEN** `EcaVocabularyDocTests` fails, naming the drift
