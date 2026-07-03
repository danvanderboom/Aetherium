# Audit: World Generation & PCG

*Audit date: 2026-07-03 · Scope: `Aetherium.Server/WorldGen` (~81 files), `WorldBuilders`, `WorldGenCLI`, PCG controllers, `Data/{Prefabs,Hubs,Curricula,Benchmarks}`. Findings marked **Verified** or **Suspected**.*

## Summary

The PCG core is the best-engineered part of the codebase — a clean phased pipeline, exemplary deterministic RNG (SHA256-derived namespaced streams), a genuinely feature-rich dungeon generator with graph-based gating proofs, and validation that records access-path artifacts. But it is undermined by a **wiring gap between the tooling path and the game-server path**: the server never populates its generator registry, runs a shorter pass list than the CLI/dashboard, and stubs the very features (prefab loading, NPC/item placement, difficulty parameters, world persistence) that would make generated worlds playable. The result is that the impressive tooling preview and the actual in-game world are materially different, and the training/difficulty system is decorative end-to-end.

| Severity | Count | Headline |
|---|---|---|
| High | 4 | Server registry never populated (ignores requested generator); A/B test yields identical maps; placement features are log stubs; generator params have no effect |
| Medium | 8 | Hub-load race; no mid-pipeline error handling; three divergent pass lists; dead pipeline abstractions; hybrid anchors ignored; token validation a facade; GameMapGrain world-loss; dashboard PCG hard-codes wrong port |
| Low | ~9 | Outdoor ignores requested generator; secret-room overwrite; vertical-connectivity over-approximation; CWD-relative paths; empty constraint schemas; … |

## High

**Server's `MapGeneratorRegistry` is never populated.** *Verified.* `Program.cs:167` registers the registry but never calls `DiscoverTypes` (only Aetherctl and tests do). In the co-hosted server, `GameMapGrain.InitializeAsync → DungeonLayoutPass` gets `null` from `GetGenerator` and **silently falls back to `AdvancedDungeonGenerator` for every request**, ignoring the requested `generatorType` (RoomsAndCorridors, GridCity, Maze, …). Tests mask it because their silo config calls `DiscoverTypes`.

**A/B test endpoint generates identical candidates.** *Verified.* `WorldGenApi.cs:217-219` auto-seeds with `Environment.TickCount + candidates.Count`, but `candidates.Count` is 0 for every element (list materialized before the loop) and `TickCount` is effectively constant — so all candidates share one seed and the comparison compares N identical maps.

**Placement/population features are console-log stubs.** *Verified.* `SpawnNPCsFeature.cs:73-76` ("Would spawn…"), `ItemDistributionFeature.cs:75` ("Would place…"), `PrefabStamper.cs:131-149` (entity spawning commented out), `AdaptationPass.cs:32-46` ("Would adapt content"). Prefab entity tiles, NPC density, item distribution, and behavior-driven adaptation do nothing but print.

**Generator parameters have no effect on dungeon generation.** *Verified.* `AdvancedDungeonGenerator` reads **zero** `GeneratorParams` entries (room counts, sizes, one hardcoded key/lock, exactly one trap are all hardcoded); `GenerationMetrics.CalculateDifficultyProfile` is never called. So curriculum-stage difficulty knobs (`WorldGenerationRequest.ApplyCurriculumStage`) and benchmark edge-case parameters alter nothing but the validator's `minLoopRatio` — the training/difficulty system is decorative end-to-end.

## Medium (verified)

- **Hub loading race + fire-and-forget** — `Program.cs:176-180` discards the `LoadHubsAsync` task (errors only console-logged); `HubWorldLoader` mutates a plain `Dictionary` on a background thread with no lock while reads can be served concurrently. A request arriving before load completes silently sees "no hubs."
- **No mid-pipeline error handling** — `WorldGenerationOrchestrator.cs:59-77` runs each pass unguarded (only `DungeonLayoutPass` self-catches); a throwing pass aborts with a raw exception (HTTP 500 or failed grain call), no partial-world capture, no per-pass containment, no regeneration.
- **Three divergent hand-rolled pass lists** — CLI (`WorldGenApi.cs:317-346`) and Aetherctl (`WorldGenCommands.cs:395-421`) run Hybrid+Theming+Population+Story+Audio+Interactions+Validation, but the game server (`GameMapGrain.cs:142-161`) runs only Layout+Interactions+Portal+Validation. **The game server produces materially emptier worlds than the editor/CLI preview of the same request** — monsters, treasure, and lighting from theming/population exist only in tooling output.
- **Dead pipeline abstractions** — the `GeneratorPipeline` class, `AdaptationPass`/`EventSeedPass`/`TemporalInitPass`, `EnsureExitsFeature`, `ClusterFeature`, and `MapGeneratorRegistry.GetFeature` have no live callers; the documented feature-composition extension point is inert (features are hardwired into passes).
- **Hybrid anchors stored but never respected** — `HybridLayoutPass.cs:50-72` writes anchor artifacts that "generators can check," but no generator reads them; authored anchors are overwritten by PCG.
- **Narrative tokens trivially "satisfied"** — `DungeonInteractionsPass.MarkGenericToken` maps any unknown token to the start location; `GenerationValidationService.ValidateTokens` only checks the key exists — pcg-narrative's Tokens API is a facade.
- **`GameMapGrain` world persistence stubbed → world loss on reactivation** — restore = `new World()` placeholder (`:44-49`), `SerializedWorld=null` (`:128`), `GetWorldAsync` returns null (`:163-167`). Any silo recycle discards the generated world while `MapState` claims it exists. (Cross-confirmed by the narrative/multiworld audit.)
- **Dashboard PCG client hard-codes the wrong port** — `PcgApiClient.cs:21` points at `http://localhost:5000/api`, but the WorldGen API is mounted by `aetherctl worldgen serve` (also default 5000, so a collision) and the game server at 5000 doesn't host `/api/generate` — every PCG dashboard call 404s at defaults.

## Low (verified unless noted)

Outdoor pass ignores the requested generator (always `AdvancedOutdoorGenerator`); secret-room fallback can overwrite existing geometry (dead retry loop, no `CanPlace`); validator over-approximates vertical connectivity (unconditional z±1 neighbors, ignores stairs placement); curriculum/benchmark paths are CWD-relative; `CurriculumProgressionGrain` has no persisted state; dead code in `WorldGenApi` (unused `genContext`, async-without-await); unstable pass sort within a phase; O(n·m)/O(k²) scans (tolerable at default sizes); `[GeneratorParam]` attribute mechanism exists but no generator uses it, so constraint schemas are always empty; multi-level requests to non-Advanced generators silently yield single-level worlds (*Suspected*).

## Verified leads (from the brief)

1. **Partial** — `PrefabLibrary.LoadFromDirectory` (sync) *is* implemented and tested; the `Program.cs:207` TODO is stale — the missing piece is a one-line call. Net: nothing calls it, so `Data/Prefabs/*.json` are unreachable and `BuilderAI` always sees an empty catalog. (Grain storage is also a TODO.)
2. **Confirmed** — `GeneratorsDeterminismTests` never tests determinism (one generation, no comparison); the real determinism test (`WorldGenerationPipelineTests`) compares only 4 metrics, far short of the spec's "identical" requirement. RNG *discipline* in WorldGen proper is solid; unseeded `Random` is confined to legacy WorldBuilders and the Adaptation module (which uses `Guid`/`DateTime` for IDs by design).
3. **Confirmed** — the "Argentinaable" typo (`MapStandards.md:72`) plus a mojibake comment in `MapValidator.cs:218`; `MapValidator` fully implements the standards but is **never called at runtime** (only tests call `Validate`; live session worlds from `GameHub` skip it). The separate `GenerationValidationService` *is* enforced in the pipeline path.
4. **Confirmed (worse)** — `CurriculumController.GetAllCurricula` always returns empty (TODO), and the controller and `CurriculumProgressionGrain` are unconnected (the grain's library is populated only via test reflection), so curricula on disk can never drive a runtime progression.

## Strengths

- Clean phased pipeline (`IWorldGenerationPass` + `GenerationPhase` + orchestrator with per-phase timing); `Success` correctly requires world ≠ null ∧ validation ∧ zero errors.
- **Exemplary deterministic RNG**: SHA256-derived, versioned, namespaced streams (`GeneratorContext.GetRandom`) — matches pcg-core precisely.
- `AdvancedDungeonGenerator` is genuinely rich: MST-connected rooms + 35% loop edges, room-shape variety enforcement, bridge-edge detection for provably-gating key/lock placement, telegraphed traps with alternate solutions, graph metrics.
- Validation records `start-to-key`/`start-to-objective` BFS proof artifacts; failures are fatal in `GameMapGrain`.
- Tooling surface (CLI generate/serve/render with PNG export, REST routes, template CRUD, `MapRenderDto` overlays) matches pcg-tooling closely.
- `BenchmarkLibrary` uses a proper lazy, locked, double-checked load — the pattern `PrefabLibrary`/curricula should copy.
- Solid unit breadth in places: `MapValidationTests` (9), `MapGeneratorRegistryTests` (16), `OutdoorTerrainGeneratorTests` (13), `CityGeneratorTests` (16), `PrefabLibraryTests` (15).

## Spec alignment

Mostly aligned on the **pcg-core / pcg-dungeons / pcg-tooling / geometry-maze** specs (phased orchestration, RNG namespaces, gating proofs, REST endpoints, maze classification). Weaker on **pcg-outdoor** (no traversal-cost layer, no biome-adjacency enforcement), **pcg-interactives** (single hardcoded key/lock, no puzzle templates), **pcg-narrative** (token API is a facade, no critical-placement reservation), and **pcg-validation** (multi-level validation is physically wrong; **no regeneration/fallback strategy exists**, violating the threshold-enforcement and time-budget scenarios). **world-building**: hybrid anchors are processed but never respected by any generator.

## Test coverage & gaps

Good breadth on generators/validation/registry. **Gaps:** no byte-for-byte/golden-file determinism test (the headline pcg-core requirement); zero tests for `OrganicCityGenerator`, `AdvancedOutdoorGenerator`, several features, `HubWorldLoader` (the startup race), and the dead passes; no HTTP-level `WorldGenApi` tests (a two-candidates-differ assertion would catch the A/B seed bug); no test that the **server's** DI-composed registry can resolve a generator (would catch the missing `DiscoverTypes`); no cross-entry-point consistency test (server vs tooling pass lists); no size/perf regression tests beyond 200×200.
