# Tasks: add-content-definitions

## 1. Model tier

- [x] 1.1 `Aetherium.Model/Content/ContentConfig.cs`: `ContentConfig`, `CreatureDefinition`,
      `ItemDefinition` (+ `HealEffectDefinition`), `SpawnTableEntry` — `[GenerateSerializer]`.
- [x] 1.2 Add `Content` to `GameDefinition`; `ContentConfig` to `WorldTemplate`, `WorldConfig`,
      `CreateWorldRequest` (nullable, additive serializer ids).

## 2. Loader / validator / mapper

- [x] 2.1 `GameDefinitionLoader`: `content.yaml` conventional sibling (+ inline `content:`),
      duplicate-declaration rejection, same strict parsing.
- [x] 2.2 `GameDefinitionValidator`: content id uniqueness, spawn→creature, loot→item,
      behavior-preset, color-parse, positivity checks; doctrine `kill:` warning.
- [x] 2.3 `GameDefinitionMapper`: carry `Content` onto `CreateWorldRequest`.

## 3. Runtime compile + grain wiring

- [x] 3.1 `Aetherium.Server/Content/ContentCompiler.cs`: config → `ContentCatalog`
      (creatures/items by id, cumulative-weight spawn draw, per-world atlas, behavior preset
      registry with `wander-melee`).
- [x] 3.2 `GameMapGrain`: thread `contentConfig` through `InitializeAsync` → `MapState`;
      `ApplyContentConfig` on init + reactivation; post-generation deterministic re-skin;
      per-creature `TileType` registration.
- [x] 3.3 `SpawnEntityAsync`: catalog-first resolution, legacy switch fallback.
- [x] 3.4 `SpawnMonsterLoot(victim, …)`: definition loot → materialized item; null → nothing;
      unresolved → legacy `SwordItem`.
- [x] 3.5 `StepNpcsAsync`: behavior preset lookup per creature (only `wander-melee` exists).
- [x] 3.6 `EntityFactory`: `CreatureType` property round-trip; hydration re-skin hook.
- [x] 3.7 Thread through `WorldGrain`, `GameManagementGrain` (both paths), `OrleansWorldHost`.

## 4. Sample bundles

- [x] 4.1 Emberfall `content.yaml`: wolf (loot `wolf_pelt`), cult_acolyte (loot `ember_blade`),
      townsfolk (spawn-request only — makes `kill:townsfolk` doctrine real), spawn weights —
      `kill:wolf` doctrine becomes satisfiable.
- [x] 4.2 Neonveil inline `content:`: drone (loot `scrap_core`), netwatch_agent (loot
      `repair_patch`, a heal item), spawn weights — `kill:drone` doctrine becomes satisfiable.

## 5. Tests (names per spec Verified-by)

- [x] 5.1 Loader: `LoadBundle_ContentSection_BindsCreaturesItemsSpawns`.
- [x] 5.2 Validator: duplicate ids / unknown spawn creature / unknown loot item / unknown
      behavior preset / doctrine-kill warning.
- [x] 5.3 Compiler: deterministic spawn draw, item materialization, atlas registration.
- [x] 5.4 Snapshot: `CreatureTypeTag_RoundTrips`.
- [x] 5.5 Grain integration: populate-from-spawn-table, defined loot drop, spawn-by-id,
      Emberfall/Neonveil bestiary isolation.
- [x] 5.6 Full suite green (Aetherium.Test + Aetherctl.Test).

## Later (out of scope this change)

- L.1 Data-driven treasure/item placement and spawn regions.
- L.2 Additional behavior presets; ECA-scripted behaviors.
- L.3 Terrain/material/light/audio atlas sections in YAML.
- L.4 Multi-tag creature taxonomies for doctrine matching.
- L.5 Equipment slots / non-inventory weapon semantics.
