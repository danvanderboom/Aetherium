## Context

The per-world config plumbing (`DeathPolicy`/`AbilityConfig`/`ProgressionConfig`/`FactionConfig` through `CreateWorldRequest` → `WorldConfig` → `WorldGrainState` → `MapState`) is shipped and test-proven, but no production code originates config values — only tests do. Meanwhile a real JSON file→object pipeline already exists for other asset categories (`PrefabLibrary`, `HubWorldLoader`, `JsonAudioProfileRepository`, benchmarks), proving the loader shape this change copies. Full survey: 2026-07-10 session; see also [docs/audits/2026-07-06-engine-gap-analysis/design-authoring-and-scripting.md](../../../docs/audits/2026-07-06-engine-gap-analysis/design-authoring-and-scripting.md) (Layer 1–2: declarative data authoring) — this change is the first shipping piece of that authoring model.

Vocabulary note: "game **definition**" = the authored bundle (the game); "game **instance**" = a running world created from it. An *instance* here is a whole world (potentially multi-map), not a dungeon instance in the `InstanceAllocatorGrain` sense — those continue to exist *within* a game instance.

## Goals / Non-Goals

- Goals:
  - A non-programmer can define a complete, playable game in YAML and run N concurrent instances of it beside other games on one server.
  - The loader binds to the **existing, shipped** config types — no parallel schema, no translation layer that can drift. The YAML keys *are* the camelCase of the config POCOs.
  - Validation that fails loudly and specifically at load time: a typo'd key, a skill unlocking a nonexistent ability, an XP rule feeding an undeclared pool — all named errors with file/section context, never silent no-ops at runtime.
  - Startup resilience: one bad bundle never blocks the server or other bundles (the `HubWorldLoader` behavior, but with a surfaced diagnostics report instead of console lines only).
- Non-Goals (deliberately later):
  - **Zip packaging, signing, and distribution** — that's the modding/content SDK (§4.15); this change establishes the directory layout a pack will zip.
  - **Hot-reload of running instances** — instances are config-immutable by construction (see Decisions); reload semantics for *definitions* are limited to "affects new instances only."
  - **Multi-version hosting** of the same game id (registry holds one version per id; duplicate ids are load errors). Instances *record* their creating version, so this can be added without migration.
  - **Sections without shipped config types** — economy, party, live events, telemetry, locale packs enter the schema when their `XConfig` types ship (each is a one-field addition to `GameDefinition` + validator rules).
  - **Content-atlas/prefab/loot-table overrides in bundles** — those asset categories aren't per-world-threaded yet; they join the schema when they are.
  - **A JSON twin of the bundle format** — trivial to add later (same POCOs), omitted to keep one canonical authoring format.

## Decisions

- **YAML via YamlDotNet, server-side only.** The user-facing promise across every design doc is YAML authoring; YamlDotNet is the standard .NET implementation. It lives in `Aetherium.Server` — `Aetherium.Model` stays dependency-free, and the loader binds YAML directly to Model POCOs (plain get/set properties, exactly what YamlDotNet handles). `CamelCaseNamingConvention` maps `minStanding:` → `MinStanding`.
- **The bundle is a directory, `game.yaml` is the manifest.** Sections may live inline in `game.yaml` or in conventional sibling files (`death.yaml`, `abilities.yaml`, `progression.yaml`, `factions.yaml`). A section present in both places is a load error ("one source of truth per section"). Directory-as-bundle is what the content SDK will zip and sign later; the layout is the future pack format's working tree.
- **Strict-by-default deserialization.** Unknown YAML keys are errors, not ignored. Rationale: in a data-driven engine, a misspelled `dammageType:` that silently deserializes to a default is a bug that ships; the colorblind-lint philosophy (mechanical enforcement at authoring time) applies to data as much as code.
- **`GeneratorParameters` (`Dictionary<string, object>`) gets a scalar-typing node deserializer** (int/double/bool/string inference for YAML scalars). This is the one place plain POCO binding needs help; it's contained in the loader, and generator parameters were already `object`-typed on the wire.
- **Instance creation reuses `CreateWorldAsync` verbatim.** `CreateGameInstanceAsync` is a thin mapper: definition → `CreateWorldRequest` (all configs, generator, size, narrative fields) → the existing, battle-tested creation path. No second world-creation machinery. The mapper (`GameDefinitionMapper`) is a pure function, unit-testable without a cluster.
- **Instance ↔ definition bookkeeping is two nullable fields**, `GameDefinitionId`/`GameDefinitionVersion`, added (additive `[Id(n)]`) to `WorldConfig`, `CreateWorldRequest`, `WorldTemplate`, and surfaced on `WorldInfo`. `ListGameInstancesAsync(gameId)` filters `ListWorldsAsync` output. No new grain: the management grain already owns world lifecycle, and a separate directory grain would just mirror its state.
- **Instance immutability is inherited, not built.** Configs are copied into `WorldGrainState`/`MapState` at creation (established by the four wire slices); a bundle edit or registry reload therefore cannot touch a running instance. The spec pins this with a test so the property survives future refactors.
- **Concurrent multi-game hosting needs no new isolation mechanism** — per-world config isolation is already proven by the per-world tests of all four wire slices. What this change adds is the *proof at the definition tier*: an integration test running 3 emberfall + 2 neonveil instances simultaneously, asserting a fantasy spell casts in emberfall instances and doesn't exist in neonveil ones, and faction landscapes differ per game.
- **Diagnostics are data** (`GameDefinitionDiagnostic { BundlePath, Section, Severity, Message }`), returned by loader/validator and surfaced by registry list APIs — not just console lines (and, per [localization.md](../../../docs/localization.md), not player-facing text; these are operator/designer artifacts).

## Bundle format (illustrative)

```yaml
# Data/Games/emberfall/game.yaml
id: emberfall
name: Emberfall
version: 1.0.0
description: A fantasy dungeon RPG of mana-fueled spellcraft and uneasy townsfolk.
tags: [fantasy, rpg, sample]

world:
  generatorType: maze
  size: { width: 80, height: 80, depth: 3 }
  maxPlayers: 40

# abilities/progression/factions/death may be inline here or in sibling files:
```

```yaml
# Data/Games/emberfall/factions.yaml   (keys = camelCase of FactionConfig)
factions:
  - id: town
    name: Rivertown
    doctrineDeltas: { "kill:wolf": 10, "kill:townsfolk": -50 }
    rankRules:
      - { minStanding: 100, rankId: friend }
  - id: cult
    name: Cult of the Fang
    doctrineDeltas: { "kill:wolf": -15 }
relations:
  - { fromFactionId: town, toFactionId: cult, disposition: War, mutual: true }
bands:
  - { id: hostile,  minStanding: -1000 }
  - { id: neutral,  minStanding: -100 }
  - { id: friendly, minStanding: 100 }
```

Exact keys are, by construction, the camelCase renderings of the shipped config types — the loader has no schema of its own to drift.

## Risks / Trade-offs

- **YAML's type looseness** (`no` parsing as boolean, version strings as floats) — mitigated by strict binding to typed POCOs, the scalar-typing deserializer being scoped to the one `object`-typed bag, and validator errors carrying file/section context. Sample bundles + tests pin the happy path.
- **Schema/type coupling cuts both ways.** Binding YAML directly to config POCOs means config-type renames are bundle-breaking changes. Accepted: the alternative (a parallel DTO schema) drifts; instead, spec discipline — config-type property renames require an openspec change noting bundle impact — and the sample bundles act as canary tests that fail on any accidental rename.
- **Two sample games are engine-repo content.** Kept deliberately minimal (each well under ~100 lines of YAML) and clearly marked as samples/fixtures; real games live outside the engine repo. This does not violate "no game content in the engine" anti-goals — samples are documentation the tests execute.

## Migration Plan

Additive only. New nullable fields on existing serialized types (append-only `[Id(n)]`), new files, new endpoints. Worlds created through existing paths (dashboard REST, hubs, tests) are untouched and carry null definition ids.

## Open Questions

- **Startup instance policy**: should a bundle be able to declare `autoStartInstances: 1` so a server boots straight into playable games? Deferred — trivial to add, but instance lifecycle policy (who stops them?) belongs with the ops story.
- **Where `aetherctl game create` gets its server connection** — follows whatever convention the existing `aetherctl world`/`party` commands use; no new decision needed, noted for the implementer.
