## Slice — YAML game-definition bundles → registry → concurrent playable instances

### 1. Model tier (Aetherium.Model.Games)
- [x] 1.1 `GameDefinition` (`Id`, `Name`, `Version`, `Description`, `Tags`, `World: GameWorldDefinition`, `DeathPolicy?`, `AbilityConfig?`, `ProgressionConfig?`, `FactionConfig?`) + `GameWorldDefinition` (`GeneratorType`, `GeneratorParameters`, `Size`, `MaxPlayers`, `NarrativeId?`, `NarrativeSeed?`) — `[GenerateSerializer]`, plain POCOs
- [x] 1.2 `GameDefinitionId`/`GameDefinitionVersion` (nullable, additive `[Id(n)]`) on `WorldConfig`, `CreateWorldRequest`, `WorldTemplate`; surfaced on `WorldInfo`
- [x] 1.3 `GameDefinitionDiagnostic { BundlePath, Section, Severity, Message }` + `GameDefinitionSummaryDto` for list surfaces

### 2. Loader (Aetherium.Server.Games)
- [x] 2.1 YamlDotNet package reference (Aetherium.Server only)
- [x] 2.2 `GameDefinitionLoader.LoadBundle(directory)`: `game.yaml` manifest; conventional sibling section files (`death|abilities|progression|factions.yaml`); inline-and-file duplicate section → error; camelCase naming convention; strict unknown-key handling
- [x] 2.3 Scalar-typing node deserializer for `Dictionary<string, object>` (`GeneratorParameters`): int/double/bool/string inference
- [x] 2.4 Malformed YAML / missing manifest / missing `id`/`version` → diagnostics, bundle skipped, other bundles unaffected

### 3. Validation
- [x] 3.1 Structural: required manifest fields; semver-parseable `version`; world section sanity (size > 0, generator nonempty)
- [x] 3.2 Cross-section referential: skill `unlocksAbilityId` ∈ abilities; skill/XP-rule `poolId`s ∈ declared progress pools; ability resource costs reference declared resource pools; faction relation from/to ids ∈ factions; unique ids within each section (abilities, skills, factions, bands)
- [x] 3.3 All failures as `GameDefinitionDiagnostic` lists with bundle/section context

### 4. Registry + instance path
- [x] 4.1 `GameDefinitionRegistry` (DI singleton, `PrefabLibrary` mold): loads `Data/Games/**` at startup; list/get; duplicate game id → diagnostic + skip; test-facing load-from-path API
- [x] 4.2 `GameDefinitionMapper.ToCreateWorldRequest(definition, instanceName?)` — pure, unit-testable
- [x] 4.3 `IGameManagementGrain.CreateGameInstanceAsync(gameId, instanceName?)` → mapper → existing `CreateWorldAsync`; `ListGameInstancesAsync(gameId)` filtering by definition id; definition id/version recorded on the world
- [x] 4.4 Registry wired into DI + startup (`Program.cs`), management grain resolves it

### 5. Sample bundles (Data/Games/)
- [x] 5.1 `emberfall/` — fantasy RPG: mana/stamina pools, 2–3 spells, town+cult factions with opposed doctrines, bands, XP pool + a skill, respawn-oriented death policy
- [x] 5.2 `neonveil/` — sci-fi netrunning: bandwidth pool, breach/spike programs, corp+collective factions, harsher death policy — deliberately shaped so no id overlaps emberfall (isolation tests read cleanly)

### 6. Management surface
- [x] 6.1 REST: `GET /api/management/games`, `POST /api/management/games/{id}/instances`, `GET /api/management/games/{id}/instances` (+ load diagnostics on the list)
- [x] 6.2 `aetherctl game list | instances <gameId> | create <gameId> [--name]`

### 7. Tests + spec
- [x] 7.1 Loader unit tests: single-file bundle, split-file bundle, duplicate section, malformed YAML, unknown key, scalar typing of generator params, bad bundle doesn't block directory load
- [x] 7.2 Validator unit tests: each cross-ref rule (positive + negative), diagnostics carry section context
- [x] 7.3 Mapper unit tests: full definition → `CreateWorldRequest` field-for-field, including all four configs
- [x] 7.4 Grain integration: create instance from loaded sample bundle → world runs with abilities castable, factions stamped, progression live, death policy honored; definition id/version recorded
- [x] 7.5 Concurrent multi-game integration: 3 emberfall + 2 neonveil instances on one cluster — emberfall spell casts there and is unknown in neonveil; faction landscapes differ per game; `ListGameInstancesAsync` returns 3 and 2
- [x] 7.6 Immutability: reload/modify definition after instance creation → running instance unchanged
- [x] 7.7 `specs/game-definitions/spec.md` requirements verified; full build + regression green

## Later (out of scope this change)
- [ ] L.1 Zip packaging, signing, capability model — the content SDK (§4.15) zips this directory layout
- [ ] L.2 New sections as their config types ship: economy, party, live events, telemetry, locale/grammar packs
- [ ] L.3 `autoStartInstances` + instance lifecycle policy (stop/archive idle instances)
- [ ] L.4 Multi-version hosting of one game id; definition hot-reload semantics beyond "new instances only"
- [ ] L.5 Dashboard UI for definitions/instances; JSON twin of the bundle format
- [ ] L.6 Authoring guide `docs/game-definition-bundles.md` (with implementation, when examples are runnable)
- [ ] L.7 LLM authoring flow: agents propose/validate bundles via the tool registry (`ContentAuthoring` profile, §4.15)
