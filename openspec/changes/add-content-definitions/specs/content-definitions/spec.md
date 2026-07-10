# content-definitions Specification (Delta)

## ADDED Requirements

### Requirement: Content Config Data Model

`Aetherium.Model.Content.ContentConfig` SHALL define creatures (`CreatureDefinition`: id, name,
description, glyph, color, health, attackPower, speed, behavior preset, optional lootItemId),
items (`ItemDefinition`: id, name, icon, weight, optional heal effect, optional weaponBonus),
and a weighted spawn table (`SpawnTableEntry`: creatureId, weight) as pure serializable
per-world data, threaded through `GameDefinition`, `CreateWorldRequest`, `WorldTemplate`, and
`WorldConfig` like the existing gameplay configs.

**Verified by:** `GameDefinitionLoaderTests.LoadBundle_ContentSection_BindsCreaturesItemsSpawns`,
`GameDefinitionMapperTests.MapsEveryField_ToCreateWorldRequest`

#### Scenario: Bundle content section binds

- **WHEN** a bundle declares creatures/items/spawns in `content.yaml` (or inline `content:`)
- **THEN** `GameDefinition.Content` holds every declared field, and the mapper carries it onto
  `CreateWorldRequest.ContentConfig`

### Requirement: Content Validation

The validator SHALL reject: duplicate creature or item ids, spawn entries referencing unknown
creatures, `lootItemId` referencing unknown items, unknown behavior presets, unparsable colors,
and non-positive health or spawn weight. It SHALL warn (not error) when a faction doctrine
`kill:<x>` tag matches no defined creature in a bundle that defines content.

**Verified by:** `GameDefinitionValidatorTests.Content_DuplicateIds_AreErrors`,
`GameDefinitionValidatorTests.Spawn_UnknownCreature_IsAnError`,
`GameDefinitionValidatorTests.Loot_UnknownItem_IsAnError`,
`GameDefinitionValidatorTests.Creature_UnknownBehaviorPreset_IsAnError`,
`GameDefinitionValidatorTests.Doctrine_KillTag_UnknownCreature_IsAWarning`

#### Scenario: Loot reference typo caught at load time

- **WHEN** a creature's `lootItemId` names an item that does not exist in the bundle
- **THEN** validation produces an Error diagnostic naming the creature and the missing item id

### Requirement: Data-Driven Population

WHEN a world is created with a `ContentConfig` whose spawn table is non-empty, every monster
placed by the population passes SHALL be re-materialized from a weighted draw over the spawn
table — health, attack power, action speed, glyph/color tile, and `CreatureTypeTag` all taken
from the drawn `CreatureDefinition` — deterministically per world seed. A world with no
`ContentConfig` SHALL be populated exactly as today (legacy `Monster`, glyph `M`).

**Verified by:** `GameInstanceTests.CreateInstance_PopulatesFromSpawnTable`,
`GameInstanceTests.ContentIsolation_EmberfallWolves_NeonveilDrones`,
`ContentCompilerTests.SpawnDraw_IsDeterministicPerSeed`

#### Scenario: Two games, two bestiaries, one server

- **WHEN** an Emberfall instance (wolves, cult acolytes) and a Neonveil instance (drones,
  NetWatch agents) run concurrently
- **THEN** every creature in the Emberfall world carries an Emberfall creature id and every
  creature in the Neonveil world carries a Neonveil creature id, with no cross-contamination

### Requirement: Data-Driven Loot

A defeated creature whose definition names a `lootItemId` SHALL drop that item (materialized
from its `ItemDefinition`); a defined creature with no `lootItemId` SHALL drop nothing; a
victim with no resolvable definition SHALL drop the legacy `SwordItem`.

**Verified by:** `GameInstanceTests.Kill_DropsDefinedLootItem`,
`ContentCompilerTests.MaterializeItem_BindsCarriableConsumableWeapon`

#### Scenario: Wolf drops a pelt, not a sword

- **WHEN** a player kills a `wolf` whose definition declares `lootItemId: wolf_pelt`
- **THEN** the drop at the fall location is a `wolf_pelt` item with the definition's
  label/icon, not a `SwordItem`

### Requirement: Spawn Request Catalog Resolution

`SpawnEntityAsync` SHALL resolve a requested creature type against the world's content catalog
first — materializing the definition when found — and fall back to the legacy hardcoded switch
otherwise, preserving `CreatureTypeTag` in both paths.

**Verified by:** `GameInstanceTests.SpawnEntity_ResolvesDefinedCreature`

#### Scenario: Spawning a defined creature by id

- **WHEN** `SpawnEntityAsync(creatureType: "drone")` is called on a Neonveil map
- **THEN** the spawned entity has the drone definition's health/glyph and
  `CreatureTypeTag == "drone"`

### Requirement: Content Survives Reactivation

Snapshot capture SHALL round-trip `CreatureTypeTag`, and grain re-hydration SHALL re-apply each
tagged entity's definition from the persisted `ContentConfig` — preserving current damage
(captured health re-applies after the re-skin).

**Verified by:** `EntityFactorySnapshotTests.CreatureTypeTag_RoundTrips`

#### Scenario: Damaged wolf survives grain restart

- **WHEN** a wolf at 5/20 HP is captured in a snapshot and the map grain re-hydrates
- **THEN** the rebuilt entity is a wolf (definition glyph/stats) at 5 HP

### Requirement: Per-World Entity-Kind Atlas

The compiled catalog SHALL expose a per-world `ContentAtlas` containing the engine default tags
plus one `EntityKindTag` per defined creature and item.

**Verified by:** `ContentCompilerTests.Atlas_RegistersDefinedEntityKinds`

#### Scenario: Defined creatures appear in the world's vocabulary

- **WHEN** a config defining `wolf` and `healing_salve` is compiled
- **THEN** the catalog's atlas contains entity-kind tags `wolf` and `healing_salve` alongside
  the engine defaults
