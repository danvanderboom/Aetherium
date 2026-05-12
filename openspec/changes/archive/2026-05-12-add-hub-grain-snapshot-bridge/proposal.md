# Add Hub-to-Grain World Snapshot Bridge (Phase 1)

## Why

`GameHub.OnConnectedAsync` currently builds a private `FovDiagnosticWorldBuilder("open_space")` per connection ([Aetherium.Server/GameHub.cs:80-86](Aetherium.Server/GameHub.cs:80)), and `GameHub.JoinWorld` is a stub that returns `OperationResult.Error("Joining worlds via GameHub is not yet supported.")`. The multi-world infrastructure (`IWorldGrain`, `IGameMapGrain`, `OrleansWorldHost`, cluster/portal/economy systems) exists in parallel but is never actually bridged to the gameplay surface. Two clients connecting to the "same" world today get two separate private worlds.

This is the first of a phased migration toward a grain-authoritative World ownership model (the broader plan is documented in this change's `design.md`). Phase 1 establishes the hub-grain bridge with the minimum viable surface: a deterministic snapshot served by `IGameMapGrain`, hydrated locally by each session, with identical layouts and entity IDs across joiners. Live shared mutation is explicitly deferred to phase 2.

## What Changes

- `IGameMapGrain.GetWorldSnapshotAsync()` returns a `WorldSnapshot` DTO (recipe + entity placements) sufficient to reconstruct an equivalent `World` locally.
- `IGameMapGrain.JoinPlayerAsync(playerId)` returns a `JoinMapResult` containing a unique spawn location and the player's authoritative entity ID. The existing `bool AddPlayerAsync(string)` is preserved for cross-map moves where spawn details aren't needed.
- `GameHub.JoinWorld(worldId, mapId?)` is implemented end-to-end: it calls the grain, hydrates a new `World` from the snapshot via a new `SnapshotWorldBuilder`, and replaces `session.World`. The legacy "no `worldId`" path remains unchanged so existing tests and ad-hoc dev runs keep working.
- New types: `WorldSnapshot`, `WorldRecipe`, `EntityPlacement`, `JoinMapResult`, `JoinWorldResult` — all `[GenerateSerializer]` so they cross the grain boundary.
- New code: `SnapshotWorldBuilder` (regenerates terrain from recipe + overlays entities) and `WorldSnapshotBuilder.SnapshotOf` (flattens a live World to a snapshot).
- `GameMapGrain` captures its `WorldRecipe` at `InitializeAsync` time so it can serve deterministic snapshots later.
- `GameSession.ReplaceWorld(builder, worldId, mapId, spawnLocation)` swaps the underlying world under the session's existing state lock.
- **Not breaking**: the existing `GameSession(connectionId, builder)` constructor and the legacy `OnConnectedAsync` path are preserved.

## Impact

- Affected specs:
  - `client-server-communication` — MODIFIED `Session Management`, ADDED `World Joining via Snapshot Bridge`, ADDED `Snapshot-Driven World Hydration`
- Affected code:
  - [Aetherium.Server/GameHub.cs](Aetherium.Server/GameHub.cs) — `JoinWorld`, `OnConnectedAsync` query-param fork
  - [Aetherium.Server/GameSession.cs](Aetherium.Server/GameSession.cs) — `ReplaceWorld` method
  - [Aetherium.Server/GameSessionManager.cs](Aetherium.Server/GameSessionManager.cs) — `ReplaceSessionWorld`
  - [Aetherium.Server/MultiWorld/IGameMapGrain.cs](Aetherium.Server/MultiWorld/IGameMapGrain.cs) — two new methods
  - [Aetherium.Server/MultiWorld/GameMapGrain.cs](Aetherium.Server/MultiWorld/GameMapGrain.cs) — recipe capture + new method implementations
  - New: `Aetherium.Server/MultiWorld/WorldSnapshot.cs`, `WorldSnapshotBuilder.cs`, `JoinWorldResult.cs`
  - New: `Aetherium.Server/WorldBuilders/SnapshotWorldBuilder.cs`
  - New tests in `Aetherium.Test/` — round-trip, grain integration via Orleans TestingHost, hub join via WebApplicationFactory + SignalR.Client
- **Non-breaking**: all 703 existing tests continue passing without modification.
- Sets up phase 2 (live shared mutation) by establishing the snapshot DTO format and the hub→grain call path. Phase 2 will extend `EntityPlacement` to carry full component state.
