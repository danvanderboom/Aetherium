# World Persistence

## ADDED Requirements

### Requirement: Durable Grain Storage

The server SHALL persist Orleans grain state durably across reboots when configured for durable storage. The default development configuration MAY use in-memory storage; production and any opt-in development environment SHALL use a durable provider (SQLite locally, Azure Table or equivalent in the cloud).

#### Scenario: SQLite provider survives reboot

- **WHEN** the server is started with `ORLEANS_STORAGE=sqlite` and `AETHERIUM_DATA_DIR` set to a writable directory
- **AND** a `GameMapGrain` activates and records a `WorldRecipe`
- **AND** the server process is terminated and restarted with the same env vars
- **THEN** the same `GameMapGrain` SHALL reactivate with the same `WorldRecipe` loaded from storage

#### Scenario: In-memory provider remains the default

- **WHEN** the server is started with no `ORLEANS_STORAGE` env var (or `ORLEANS_STORAGE=memory`)
- **THEN** all three named providers (`narrativeStore`, `worldStore`, `mapStore`) SHALL be `MemoryGrainStorage`
- **AND** existing tests SHALL pass without modification

### Requirement: Snapshot-Plus-Delta-Log Storage

The server SHALL persist mid-game world mutations via a hybrid model: periodic full-region snapshots plus an append-only log of every `MapDelta` emitted between snapshots. The store interface is `IWorldSnapshotStore`. Implementations SHALL support: snapshot save/load, delta append, range query of deltas by `Sequence`, and compaction (drop log entries at or below a snapshot's sequence).

#### Scenario: Every delta lands in the log

- **WHEN** `GameMapGrain.FanOutAsync` emits a `MapDelta`
- **THEN** the grain SHALL `await IWorldSnapshotStore.AppendDeltaAsync(worldId, regionId, delta)` before fan-out completes
- **AND** the persisted row SHALL include the delta's `Sequence`, the delta type name, and the serialized payload

#### Scenario: Cold start replays log atop snapshot

- **WHEN** `GameMapGrain.OnActivateAsync` finds a snapshot in storage with `Sequence = S`
- **THEN** the grain SHALL deserialize entities from `RegionStateSnapshot.SerializedEntities`
- **AND** SHALL replay every `MapDelta` in the log with `Sequence > S` in ascending sequence order
- **AND** SHALL set `_nextSequence = max(S, last_replayed_sequence) + 1`

#### Scenario: No snapshot means regenerate from recipe

- **WHEN** `GameMapGrain.OnActivateAsync` finds no snapshot in storage
- **THEN** the grain SHALL regenerate the tile grid from `MapState.WorldRecipe` (existing behavior)
- **AND** SHALL start `_nextSequence` at 1

### Requirement: Compaction Bounds Recovery Time

The server SHALL periodically capture a fresh snapshot and truncate log entries at or below the snapshot's sequence, so cold-start replay time stays bounded.

#### Scenario: Periodic compaction

- **WHEN** a `GameMapGrain` compaction timer fires (default every 10 minutes or after 1000 deltas, whichever comes first)
- **THEN** the grain SHALL capture `WorldSnapshotBuilder.SnapshotOf(...)` at the current sequence `S`
- **AND** SHALL `await SaveSnapshotAsync(...)` to persist the snapshot
- **AND** SHALL `await CompactDeltasAsync(...)` to delete log entries with `Sequence <= S`
- **AND** SHALL leave log entries with `Sequence > S` intact

### Requirement: Delta Idempotency for Safe Replay

Every `MapDelta` subclass SHALL be idempotent under replay — applying the same delta twice (or applying a delta to a world that already reflects its effects) SHALL produce the same end state as applying it once. New delta types SHALL preserve this property; reviewers SHALL flag any delta that requires "once and only once" semantics.

#### Scenario: Replay-tolerant removal

- **WHEN** the log contains an `EntityRemovedDelta` for entity `E`
- **AND** entity `E` is not present in the loaded world (either never existed or already removed)
- **THEN** the replay SHALL silently succeed
- **AND** SHALL NOT throw

#### Scenario: Absolute field values

- **WHEN** a `ComponentFieldChangedDelta` is replayed
- **THEN** it SHALL set the field to its carried value
- **AND** SHALL NOT apply a delta-of-a-delta

### Requirement: Schema and Delta-Type Versioning

The persistence layer SHALL detect snapshot and delta-type versions that the current binary does not understand and SHALL refuse to load with a descriptive error rather than silently mis-applying state.

#### Scenario: Future snapshot version

- **WHEN** a stored snapshot's `SnapshotVersion` exceeds the binary's supported version
- **THEN** activation SHALL throw an exception identifying the version mismatch
- **AND** SHALL NOT attempt to load the snapshot

#### Scenario: Future delta type version

- **WHEN** a stored delta's `delta_type_version` exceeds the binary's known version for that type
- **THEN** replay SHALL throw an exception identifying the type and version
- **AND** SHALL NOT silently skip or partially apply the delta

### Requirement: Persistence Is Configurable and Opt-In

Persistence configuration SHALL be controlled by environment variables and `appsettings.json` so a developer can opt in or out without code changes.

#### Scenario: Disable compaction

- **WHEN** `appsettings.json` sets `Persistence:Compaction:Enabled = false`
- **THEN** the compaction timer SHALL NOT fire
- **AND** the delta log SHALL grow unbounded until enabled

#### Scenario: Tune compaction cadence

- **WHEN** `appsettings.json` sets `Persistence:Compaction:IntervalMinutes = 5` and `Persistence:Compaction:DeltaCountThreshold = 500`
- **THEN** compaction SHALL fire every 5 minutes or every 500 deltas, whichever comes first
