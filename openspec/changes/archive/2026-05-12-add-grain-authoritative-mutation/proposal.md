# Add Grain-Authoritative Mutation with SignalR Delta Fan-Out (Phase 2b+c)

## Why

After `add-hub-grain-snapshot-bridge` (phase 1), two clients joining the same world see identical layouts and identical entity IDs but mutations are local — picking up an item in one session doesn't affect any other session or the grain's canonical world. After `add-map-mutation-gateway` (phase 2a), gameplay tools route through `IMapMutationGateway` but the only implementation is `LocalMutationGateway`. This change implements the second gateway, makes the grain the authoritative owner of mutation, and pushes deltas to every session in the same world via SignalR groups so live shared mutation finally works.

This is the change that turns "identical starting state" into actual multiplayer.

## What Changes

- New grain methods on `IGameMapGrain`: `MoveAsync(sessionId, direction, distance)`, `RotateAsync(sessionId, degrees)`, `ChangeLevelAsync(sessionId, deltaZ)`, `PickupAsync(sessionId, targetId)`, `DropAsync(sessionId, itemId)`, `UseAsync(sessionId, itemId, targetId, usageId?)`, `OpenAsync(sessionId, targetId)`, `CloseAsync(sessionId, targetId)`. Each mutates the grain's `_world` and is serialized by Orleans's single-threaded grain contract.
- New `GrainMutationGateway` implementation of `IMapMutationGateway` that calls the grain methods via `IGrainFactory`.
- `GameHub.JoinWorld` swaps the session's gateway from `LocalMutationGateway` to `GrainMutationGateway` after the join succeeds.
- New delta DTOs: `EntityAddedDelta`, `EntityRemovedDelta`, `EntityMovedDelta`, `DoorStateChangedDelta`, `ItemTransferredDelta`. All `[GenerateSerializer]`.
- `GameMapGrain` subscribes to its own `_world.WorldEvents` and translates each event into the appropriate delta.
- New SignalR group convention: clients joined to a map join `map:{mapId}` group. Grain calls `hubContext.Clients.Group("map:{mapId}").SendAsync("ApplyDelta", delta)` after each mutation.
- New client-side handler: `GameSession.ApplyDelta(delta)` reconciles the session's local `World` mirror with the grain's authoritative state. Runs under the session's existing `_stateLock`.
- `GameHub.OnConnectedAsync` joins/leaves the SignalR group when a session binds to a map.
- **Player Character entities are now grain-owned**. When a player joins a world via `JoinPlayerAsync`, the grain creates a `Character` entity in `_world` for them. Other joiners' snapshots include existing players. `EntityMovedDelta` covers other players' movement.
- **Phase 1's "Phase 1 Mutation Semantics" requirement is replaced** with a phase-2 equivalent: mutations are now grain-authoritative and propagate to all sessions in the same map. The independent-per-session caveat in `CLIENT_SERVER_README.md` is removed.
- **Not breaking for tests**: sessions in legacy mode (no `?worldId=`) still use `LocalMutationGateway`. The 714 existing tests touch session-local code only and continue to pass.

## Impact

- Affected specs:
  - `client-server-communication` —
    - ADDED `Grain Mutation Methods`, `Delta Fan-Out via SignalR Groups`, `Session Mirror Reconciliation`, `Cross-Session Player Visibility`
    - MODIFIED `Server-Authoritative Game State` (clarify grain is the authority; `_world` is the canonical state)
    - MODIFIED `Phase 1 Mutation Semantics` → renamed `Mutation Semantics` with new contents describing the phase 2 model
    - MODIFIED `Grain Bridge for Map Membership and Snapshots` (snapshot now includes player Character entities)
- Affected code:
  - `Aetherium.Server/MultiWorld/IGameMapGrain.cs` — 8 new mutation methods
  - `Aetherium.Server/MultiWorld/GameMapGrain.cs` — implementations; event-subscription to `_world.WorldEvents`; SignalR group fan-out
  - `Aetherium.Server/MultiWorld/GrainMutationGateway.cs` — new file
  - `Aetherium.Server/Model/Deltas/*.cs` — new delta DTOs
  - `Aetherium.Server/GameSession.cs` — `ApplyDelta(delta)` method
  - `Aetherium.Server/GameHub.cs` — group join/leave on `JoinWorld`/`OnDisconnectedAsync`; client receives `ApplyDelta`
  - `Aetherium.Server/MultiWorld/WorldSnapshotBuilder.cs` — no longer filters out `Character` entities (other players ship in the snapshot)
  - `Aetherium.Server/MultiWorld/EntityFactory.cs` — handle `Character` in the snapshot hydration path (preserving its grain-assigned entity ID for the player)
  - `CLIENT_SERVER_README.md` — phase 2 semantics
- Tests:
  - New: `GameMapGrainMutationTests` (per-method correctness on the grain)
  - New: `SessionApplyDeltaTests` (each delta type correctly reconciles a mirror)
  - New: `EndToEndSharedMutationTests` (two SignalR clients in one map; one mutates, the other sees the delta) — this is also the home for the deferred phase-1 `GameHubJoinWorldTests`
- Sequencing: must ship *after* `add-map-mutation-gateway`. Will ship *before* `remove-legacy-mutation-paths`.
