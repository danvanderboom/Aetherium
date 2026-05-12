# Client-Server Communication — Delta Log Append

## MODIFIED Requirements

### Requirement: Grain Mutation Methods

`GameMapGrain.FanOutAsync` SHALL persist every `MapDelta` to the `IWorldSnapshotStore` delta log before fan-out to sessions completes, so that no delta observed by a session can be lost on a subsequent server restart.

#### Scenario: Append precedes fan-out

- **WHEN** `GameMapGrain.FanOutAsync` is invoked with a sequence-stamped `MapDelta`
- **THEN** the grain SHALL `await IWorldSnapshotStore.AppendDeltaAsync(MapId, regionId, delta)` before invoking the session manager fan-out
- **AND** sessions SHALL receive the delta only after persistence has succeeded (or failed and been logged per the failure-handling policy)

#### Scenario: Persistence failure does not stall game logic

- **WHEN** `AppendDeltaAsync` throws (disk full, lock contention exhausted, etc.)
- **THEN** the grain SHALL log the failure with severity `Error` and the delta's `Sequence`, `EntityId` if applicable, and exception detail
- **AND** SHALL proceed with fan-out so the live game does not stall
- **AND** SHALL increment a `persistence_append_failures_total` metric (or equivalent log counter) so persistent failures are observable

#### Scenario: Per-actor send also appends

- **WHEN** `GameMapGrain.SendToActorAsync` emits a delta to a single actor
- **THEN** the grain SHALL `await IWorldSnapshotStore.AppendDeltaAsync(...)` before sending
- **AND** SHALL apply the same failure-handling policy as `FanOutAsync`
