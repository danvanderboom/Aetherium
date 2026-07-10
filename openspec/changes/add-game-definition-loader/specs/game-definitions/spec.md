## ADDED Requirements

### Requirement: Game Definition Bundle Loading
The system SHALL load game definition bundles — directories containing a `game.yaml` manifest (id, name, semver version, world section) with gameplay-rule sections (`death`, `abilities`, `progression`, `factions`) declared inline or in conventional sibling files — binding YAML (camelCase keys) directly to the shipped per-world config types. A section declared both inline and in a sibling file SHALL be rejected. A malformed or invalid bundle SHALL be rejected with diagnostics naming the bundle and section, and SHALL NOT prevent other bundles from loading or the server from starting.

**Verified by:** `Aetherium.Test.Games.GameDefinitionLoaderTests.LoadBundle_SingleFile_BindsAllSections`, `.LoadBundle_SplitFiles_BindsAllSections`, `.LoadBundle_DuplicateSection_IsRejected`, `.LoadBundle_MalformedYaml_ProducesDiagnosticAndSkips`, `.LoadBundle_UnknownKey_IsRejected`, `.LoadBundle_GeneratorParameters_ScalarsAreTyped`, `.LoadDirectory_BadBundle_DoesNotBlockOthers`

#### Scenario: A complete game loads from one YAML file
- **WHEN** a bundle directory contains a `game.yaml` declaring world, death, abilities, progression, and factions sections
- **THEN** loading produces a `GameDefinition` whose config objects match the YAML field-for-field

#### Scenario: Split section files bind identically
- **WHEN** the same sections are declared in sibling `abilities.yaml`/`factions.yaml`/etc. files instead of inline
- **THEN** the resulting `GameDefinition` is equivalent

#### Scenario: A typo'd key is an error, not a silent default
- **WHEN** a bundle contains an unrecognized key (e.g. `dammageType:`)
- **THEN** loading rejects the bundle with a diagnostic naming the offending section

### Requirement: Game Definition Validation
The system SHALL validate loaded definitions beyond deserialization: cross-section references (a skill's `unlocksAbilityId` must name a declared ability; XP-award and skill pool references must name declared progress pools; ability resource costs must name declared resource pools; faction relations must reference declared factions) and intra-section id uniqueness. Violations SHALL be reported as diagnostics carrying bundle and section context.

**Verified by:** `Aetherium.Test.Games.GameDefinitionValidatorTests.Skill_UnknownAbilityReference_IsAnError`, `.XpRule_UnknownPool_IsAnError`, `.AbilityCost_UnknownResourcePool_IsAnError`, `.Relation_UnknownFaction_IsAnError`, `.DuplicateIds_WithinSection_AreErrors`, `.ValidDefinition_ProducesNoDiagnostics`

#### Scenario: A dangling cross-reference is caught at load time
- **WHEN** a progression skill declares `unlocksAbilityId: fireball` and no ability `fireball` exists in the definition
- **THEN** validation reports an error naming the skill, the missing ability id, and the section

### Requirement: Game Definition Registry
The system SHALL maintain a registry of loaded definitions, populated from `Data/Games/**` at startup, exposing list and get-by-id; a duplicate game id across bundles SHALL be rejected with a diagnostic (first bundle wins).

**Verified by:** `Aetherium.Test.Games.GameDefinitionRegistryTests.LoadsAllValidBundlesFromDirectory`, `.GetById_ReturnsLoadedDefinition`, `.DuplicateGameId_SecondBundleRejected`

#### Scenario: Multiple game definitions coexist in the registry
- **WHEN** `Data/Games/` contains the `emberfall` and `neonveil` sample bundles
- **THEN** the registry lists both, each retrievable by id with its own configs

### Requirement: Game Instance Creation
The system SHALL create a running game instance from a registered definition via `CreateGameInstanceAsync(gameId)`, mapping the definition onto the existing world-creation path with all declared configs applied, and SHALL record the creating definition's id and version on the instance. Instances of a game SHALL be enumerable via `ListGameInstancesAsync(gameId)`.

**Verified by:** `Aetherium.Test.Games.GameInstanceTests.CreateInstance_AppliesAllConfigsToTheWorld`, `.CreateInstance_RecordsDefinitionIdAndVersion`, `.ListGameInstances_ReturnsOnlyThatGames_Instances`, `Aetherium.Test.Games.GameDefinitionMapperTests.MapsEveryField_ToCreateWorldRequest`

#### Scenario: A YAML-defined game becomes a playable world
- **WHEN** an instance is created from the loaded `emberfall` definition and a player joins it
- **THEN** the player can cast an emberfall ability, carries emberfall's faction ledger and progression pools, and the world honors emberfall's death policy

### Requirement: Concurrent Multi-Game Hosting
The system SHALL host multiple instances of multiple game definitions concurrently on one cluster, with full isolation: content declared by one definition SHALL be absent from another definition's instances, and instances of the same definition SHALL be independent worlds.

**Verified by:** `Aetherium.Test.Games.GameInstanceTests.ThreeEmberfall_TwoNeonveil_CoexistIsolated`, `.InstancesOfSameGame_AreIndependentWorlds`

#### Scenario: A fantasy RPG and a sci-fi game run side by side
- **WHEN** 3 `emberfall` and 2 `neonveil` instances run on one cluster
- **THEN** an emberfall spell casts in emberfall instances and is unknown in neonveil instances, each game's faction landscape appears only in its own instances, and instance listings return 3 and 2 respectively

### Requirement: Instance Config Immutability
A running instance SHALL be unaffected by subsequent changes to its game definition: bundle edits or registry reloads apply to newly created instances only.

**Verified by:** `Aetherium.Test.Games.GameInstanceTests.DefinitionReload_DoesNotAffectRunningInstance`

#### Scenario: Editing a bundle never mutates a live game
- **WHEN** an instance is created and its definition is then modified and reloaded
- **THEN** the running instance's behavior is unchanged, while a newly created instance reflects the new definition
