## Why
World generation already accepts a rich parameter set — `WorldGenerationRequest.Parameters` (populated by `ApplyCurriculumStage` with `minRooms`/`maxRooms`, `keyLockChainDepth`, `trapDensity`, `enemyCount`, `resourceAvailability`, `secretRoomDensity`, …) and delivered to every pass via `GeneratorContext.GeneratorParams`. But nothing reads it. `AdvancedDungeonGenerator` hardcodes room count (6–10 at `BuildLevel:85`), exactly one key/lock pair (`PlaceGatingAndKeys:301`), and exactly one trap (`PlaceTrapsAndTools:553`); `DungeonPopulationPass` hardcodes monster count (a `/50` walkable-area ratio) and treasure (2 items); and `GenerationMetrics.CalculateDifficultyProfile` is dead code — never invoked. The result: the curriculum/benchmark/difficulty system (the whole archived `agent-training-pcg` stack) is decorative end-to-end — every difficulty knob generates the same world. This is Phase 5 item **P3-6**.

A clarification the audit understates: the four "placement" features (`SpawnNPCsFeature`, `ItemDistributionFeature`, `PrefabStamper.SpawnEntity`, `AdaptationPass`) are console-log stubs, but they are **dead alternates** — not in any pass list. The *live* placement path is `DungeonPopulationPass`/`OutdoorPopulationPass`, which really do `World.AddEntity` monsters and treasure. So worlds are populated; they are just not tunable, and the dead stubs mislead.

## What Changes
**Slice 1 — parameters drive the live generation paths (this pass):**
- `AdvancedDungeonGenerator` reads `GeneratorContext.GeneratorParams`: `minRooms`/`maxRooms` (room-count range) and `trapDensity` (trap count scaled by the number of eligible rooms). Absent or invalid params fall back to today's exact hardcoded values **and RNG draw order** (room count defaults to `rng.Next(6, 10)`; trap placement makes no RNG draws), so default (non-training) generation stays byte-for-byte identical — determinism preserved.
- `DungeonPopulationPass` honors `enemyCount` (exact monster count) and `resourceAvailability` (treasure count around the baseline pair), replacing the fixed area ratio / constant, with the same default-preserving fallback.
- New typed accessors on `GeneratorContext` (`HasParam`/`GetIntParam`/`GetDoubleParam`) read/parse/clamp params.
- `GenerationMetrics.CalculateDifficultyProfile` is invoked in the orchestrator from the effective params + measured layout metrics, and the resulting `DifficultyProfile` (+ `PredictedAgentSuccessRate`) is exposed on `GenerationMetrics`, so difficulty is finally introspectable end-to-end (benchmarks/analytics/curriculum register). Best-effort — never fails a generation.
- Tests: params measurably change output (room count, trap count, monster count, treasure count), the difficulty profile is computed with and without params, and default-path determinism is preserved (existing `DeterminismTests` stay green).

**Slice 2 — deeper parameterization + dead-stub resolution:**
- `keyLockChainDepth` → **N gated key/lock pairs** on the critical path. The bridge door remains the guaranteed cut; extra gates go on other primary-path corridors, and **every key sits in the start room** — exactly the model the validator enforces (each key reachable from start without crossing any locked door; the doors collectively gate the objective). The `LockedDoors` metric is fixed to count all gates. A test confirms the validator's gating access proof passes for the whole chain.
- `secretRoomDensity` → **N secret rooms** (extracted `PlaceSecretRoom`, looping over eligible corridors; default one).
- `OutdoorPopulationPass` honors `enemyCount` (default one trader + snake).
- **`PrefabStamper.SpawnEntity` implemented** via a new shared `Aetherium.Entities.SpawnableEntityFactory` (name → entity), which also now backs `SpawnEntityTool` — removing its duplicated reflection map. (Kept distinct from the snapshot-oriented `MultiWorld.EntityFactory`.)
- **Deleted** the dead, unreferenced stubs `SpawnNPCsFeature` / `ItemDistributionFeature` / `AdaptationPass` (superseded by the live `DungeonPopulationPass`; not in any pass list) per the honest-stub rule (P1-14).

Note: the computed difficulty *profile* reads `keyLockChainDepth`/`combatDifficulty`/etc. for its score — reflecting the curriculum's *intended* difficulty, now backed by layout that actually honors those knobs.

## Impact
- Affected specs: `pcg-dungeons` (ADDED: difficulty-parameterized generation), `pcg-core` (MODIFIED: difficulty profile in metrics)
- Affected code:
  - `Aetherium.Server/WorldGen/Generators/AdvancedDungeonGenerator.cs` — read room/key-lock/trap/secret params
  - `Aetherium.Server/WorldGen/Passes/DungeonPopulationPass.cs`, `OutdoorPopulationPass.cs` — read enemy/resource params
  - `Aetherium.Server/WorldGen/GenerationMetrics.cs` — invoke `CalculateDifficultyProfile` in the generation path
  - `Aetherium.Server/WorldGen/GeneratorContext.cs` — typed param accessors (`GetIntParam`/`GetDoubleParam`)
  - `Aetherium.Test/WorldGen/` — parameter-effect + determinism tests
- Build impact: no breaking changes; default generation output unchanged.

## Status
Slices 1 and 2 implemented on `feat/phase5-pcg-placement-difficulty` (branched from `develop`). Every P3-6 parameter now drives the live generation paths and the difficulty profile is computed end-to-end. Verified: full solution build 0 errors; **978 tests pass / 0 failed / 0 skipped** (+~14 new parameter-effect/factory tests). Defaults are byte-preserved (existing `DeterminismTests` and the single-gate/secret regression tests stay green); the key/lock chain passes the validator's gating access proof. Remaining P3-6 work is genuinely out of scope for this change: making generator *params affect layout geometry beyond counts* (e.g. `puzzleComplexity`, `branchingFactor` targets) and `resourceAvailability` on the outdoor path.
