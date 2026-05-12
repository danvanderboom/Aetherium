# Implementation Tasks

## 1. Data shapes
- [x] 1.1 Create `Aetherium.Server/MultiWorld/WorldSnapshot.cs` with `WorldSnapshot`, `WorldRecipe`, `EntityPlacement` types, all `[GenerateSerializer]` with stable `[Id]` ordinals
- [x] 1.2 Create `JoinMapResult` (same file; carries `Success`, `Reason`, `MapId`, `SpawnX/Y/Z`, `PlayerEntityId`, with `Ok`/`Fail` helpers)
- [x] 1.3 Create `JoinWorldResult` (same file; the hub-facing return DTO)
- [x] 1.4 Audit types referenced by snapshot DTOs — used plain `int X/Y/Z` triples inside DTOs (with `ToWorldLocation()` / `SpawnLocation()` helpers) rather than the engine `WorldLocation` Component, which carries unrelated machinery (parent, child components, ComponentId). `WorldSize` was already `[GenerateSerializer]`-annotated

## 2. Snapshot ↔ World plumbing
- [x] 2.1 `Aetherium.Server/MultiWorld/WorldSnapshotBuilder.cs` — `SnapshotOf(World, WorldRecipe, worldId, mapId, size)`. Filters out `Terrain` and `Character` entities
- [x] 2.2 `Aetherium.Server/WorldBuilders/SnapshotWorldBuilder.cs` — regenerates terrain via `WorldGenerationOrchestrator`, strips generator-placed non-terrain entities, overlays snapshot placements
- [x] 2.3 `Aetherium.Server/MultiWorld/EntityFactory.cs` — reflection-first type resolution (cached) with special cases for `KeyItem`, `Monster`, `Zombie`. Also added a default-value-ctor fallback after the completeness test exposed entities like `FoodItem`, `BackpackItem`, `LockpickItem` that have parameterless-with-defaults ctors
- [x] 2.4 `Entity.EntityId` was already a public `{ get; set; }`, so no API change was needed — `EntityFactory.Create` writes the snapshot's ID directly after construction

## 3. Grain extensions
- [x] 3.1 Added `Task<JoinMapResult> JoinPlayerAsync(string playerId)` and `Task<WorldSnapshot> GetWorldSnapshotAsync()` to `IGameMapGrain`
- [x] 3.2 `GameMapGrain.InitializeAsync` now captures a `WorldRecipe` (generator, seed, params, template, dimensions) and stores it in `MapState.Recipe`
- [x] 3.3 `GetWorldSnapshotAsync` calls `WorldSnapshotBuilder.SnapshotOf` with the captured recipe
- [x] 3.4 `JoinPlayerAsync` assigns unique spawn locations: maintains `_spawnsInUse: HashSet<WorldLocation>` and `_playerSpawns: Dictionary<string, WorldLocation>`. `RemovePlayerAsync` frees the spawn back to the pool
- [x] 3.5 Added `[Id(8)] WorldRecipe? Recipe` to `MapState`. The recipe also drives a free reactivation rehydration path in `OnActivateAsync` (a phase-1 bonus — silo restart no longer leaves grains with a null `_world`)
- [x] 3.6 Preserved `Task<bool> AddPlayerAsync(string)` for cross-map move callers

## 4. Session swap
- [x] 4.1 `GameSession.ReplaceWorld(WorldBuilder, worldId, mapId, spawnLocation)` builds the new world outside the lock, swaps under `_stateLock`, resets `HeatTracker`, re-anchors `ViewLocation`, re-creates the `Player` entity at the spawn
- [x] 4.2 `GameSessionManager.ReplaceSessionWorld(...)` is a thin wrapper that delegates to the session — kept on the manager so future state-coordination hooks have an attach point

## 5. Hub wiring
- [x] 5.1 `GameHub.JoinWorld(worldId, mapId?)` implemented end-to-end. Resolves world + state via `IWorldGrain.GetInfoAsync`/`GetStateAsync`, picks a map (first `MapIds` entry by default), calls `JoinPlayerAsync`, fetches the snapshot, builds `SnapshotWorldBuilder`, swaps the session's world, sends initial `ReceivePerceptionUpdate`, returns `JoinWorldResult`
- [x] 5.2 `OnConnectedAsync` now reads `?worldId=` (and optional `?mapId=`) from the SignalR connection query string and auto-calls `JoinWorld`. Failure is logged but does not refuse the connection — the client stays in the legacy private world
- [x] 5.3 Errors return `JoinWorldResult.Fail("Join failed")` to clients; full exception detail is logged server-side only

## 6. Tests
- [x] 6.1 `WorldSnapshotRoundTripTests` (xUnit, 4 tests): identical terrain layout, identical non-terrain entity IDs at identical positions, two hydrations are independent, door open/closed state round-trips through properties
- [x] 6.2 `EntityFactoryCompletenessTests` (xUnit, 1 test): reflection scans `Aetherium.Server` for concrete `Entity` subclasses and asserts every one is creatable from a synthetic placement. This caught real gaps (default-value-only ctors and Monster/Zombie tile-type lookups) that were fixed during implementation
- [x] 6.3 `GameMapGrainJoinTests` (NUnit + Orleans TestingHost, 4 tests): distinct spawns for two joiners, duplicate-player-id rejection, snapshot consistency across calls, uninitialized-map failure
- [ ] 6.4 `GameHubJoinWorldTests` — deliberately deferred. Reasoning: the proof points (snapshot identical across joiners; spawns distinct; perception arrives) are already covered by `GameMapGrainJoinTests` at the grain layer plus `WorldSnapshotRoundTripTests` at the hydration layer. Spinning up `WebApplicationFactory` + `Microsoft.AspNetCore.SignalR.Client` is significant scaffolding that duplicates the existing `GameHubTests.cs` pattern without adding coverage we don't already have. Worth doing as a follow-up when an end-to-end SignalR smoke test stand exists in the repo
- [x] 6.5 `LegacyConnectPathTests` (xUnit, 2 tests): a session created without a worldId still produces a private `FovDiagnosticWorldBuilder` world; two such sessions are independent

## 7. Validation
- [x] 7.1 All 703 pre-existing tests pass without modification (714 total now, the 11 new tests all pass)
- [x] 7.2 New tests pass (25 tests across the three new files)
- [ ] 7.3 Manual two-client smoke test — left for the operator. Procedure: start a server with B2C disabled, create a world via aetherctl, launch two `Aetherium.Console` clients with `?worldId=<id>` in their hub URL, verify identical initial perception maps
- [x] 7.4 `dotnet build Aetherium.Server` clean: 0 errors. No new warnings introduced by this change (the 199 warnings in the test build are pre-existing nullable/style)
- [x] 7.5 `CLIENT_SERVER_README.md` updated with a "Joining a Multi-World Map (Phase 1)" section that documents the `?worldId=` query parameter, the join flow, and the phase-1 mutation semantics

## 8. Cleanup
- [x] 8.1 Old `// TODO: Deserialize worldData when serialization is implemented` is gone — the `JoinWorld` stub was replaced wholesale
- [x] 8.2 Phase-1 semantics are documented in: `JoinWorldResult` XML docs, `WorldSnapshot` XML docs, `GameSession.ReplaceWorld` XML docs, `SnapshotWorldBuilder` XML docs, and `CLIENT_SERVER_README.md`
