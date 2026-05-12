# Add World Persistence

## Why

World state is purely in-memory today. Every server reboot, crash, or deploy wipes:

- Mid-game terrain modifications (agent-built structures, dug tiles)
- Mutated entity state (doors opened/locked, inventories, consumable uses, durability)
- Per-session state (player position, heading, FOV memory)
- Heat trails and monster state
- The delta sequence number (`GameMapGrain._nextSequence`), so reconnecting clients can't resume cleanly

The codebase is already *shaped* for persistence — it just isn't wired:

- `Aetherium.Server/Program.cs:250` registers three Orleans storage providers (`narrativeStore`, `worldStore`, `mapStore`) but only `MemoryGrainStorage` is live; the Azure Table branch is commented out behind a missing NuGet.
- 18+ grains already declare `[PersistentState(...)]` slots — they'd become durable for free with a real provider.
- `Aetherium.Server/Persistence/IWorldSnapshotStore.cs` defines `AppendDeltaAsync` / `SaveSnapshotAsync` / `CompactDeltasAsync` — the snapshot-plus-delta-log API. Only the in-memory implementation exists, and nothing calls `AppendDeltaAsync` today.
- Every `MapDelta` in `Aetherium.Server/MultiWorld/Deltas.cs` is already `[GenerateSerializer]` and uses absolute (not relative) field values, so each is replay-idempotent given its `Sequence`.
- `MapState.WorldRecipe` (`Aetherium.Server/MultiWorld/WorldSnapshot.cs:43`) already lets `GameMapGrain` deterministically regenerate the tile grid from a seed on activation, so persistence only needs to capture *mutations atop* the recipe.

This change wires up durable storage, hooks every delta into an append-only log, and adds periodic snapshot+compaction so the recovery time stays bounded.

## What Changes

### Phase A — Durable Orleans grain storage
- Add `Microsoft.Orleans.Persistence.AdoNet` (or equivalent SQLite-compatible package) to `Aetherium.Server.csproj`.
- Extend the `ORLEANS_STORAGE` switch in `Aetherium.Server/Program.cs` with a `"sqlite"` branch that wires SQLite-backed grain storage for all three named providers.
- Default the dev-mode flag to SQLite when an `AETHERIUM_DATA_DIR` env var is set; fall back to in-memory otherwise so existing tests aren't disrupted.
- After Phase A: `WorldRecipe`, `WorldGrainState`, `WorldDirectory`, party/raid/instance state survive reboot. World still regenerates from recipe, mutation still ephemeral.

### Phase B — `SqliteWorldSnapshotStore`
- Implement `IWorldSnapshotStore` against SQLite (System.Data.Sqlite or Microsoft.Data.Sqlite).
- Two tables: `region_snapshots(worldId, regionId, sequence, snapshot_blob, saved_at)` and `delta_log(worldId, regionId, sequence, delta_type, delta_blob, recorded_at)`.
- WAL mode enabled for concurrent reads during append.
- DI registration switches from `MemoryWorldSnapshotStore` to `SqliteWorldSnapshotStore` when the SQLite branch is active. `MemoryWorldSnapshotStore` remains the test default.

### Phase C — Hook delta append into `FanOutAsync`
- In `Aetherium.Server/MultiWorld/GameMapGrain.FanOutAsync` (and `SendToActorAsync`), after the sequence stamp is applied and before fan-out, `await _snapshotStore.AppendDeltaAsync(...)`.
- Single chokepoint: every grain-side mutation already routes through here, so no other call sites need to change.
- Performance budget: <0.5ms per delta against local SQLite WAL; measured via a benchmark test.

### Phase D — Populate `RegionStateSnapshot.SerializedEntities`
- Extend `WorldSnapshotBuilder` to produce a serialized entity payload (Orleans binary encoding of `IReadOnlyList<EntityPlacement>`).
- `GameMapGrain.OnActivateAsync` prefers `SerializedEntities` when present, then replays deltas where `Sequence > snapshot.Sequence`, then attaches.
- `MapState.SerializedWorld` is replaced (or supplemented) by the per-region snapshots; the placeholder at `GameMapGrain.cs:1325` is filled in.

### Phase E — Compaction and retention
- Background grain timer on `GameMapGrain` (every N minutes or M deltas, configurable). When fired: capture a fresh `RegionStateSnapshot`, call `SaveSnapshotAsync`, then `CompactDeltasAsync` to drop the log entries at or below the snapshot's sequence.
- Bounded log growth and bounded cold-start replay time.

### Phase F — Schema and delta-type evolution
- Add `SnapshotVersion` checks (already declared on `WorldSnapshot.SnapshotVersion`) and a `delta_type_version` column to `delta_log`.
- On activation, if a stored delta type can no longer be replayed against the current code, refuse load with a clear error and require compaction at the prior version. Document each delta's `[Id]` assignments as part of the wire-stability contract.

## Impact

- **Affected specs**:
  - `client-server-communication` — MODIFIED: `Grain Mutation Methods` now describes delta-log append as part of `FanOutAsync`.
  - `world-persistence` (new) — ADDED: requirements for durable grain storage, snapshot+delta-log format, recovery semantics, compaction policy, and schema evolution.
- **Affected code**:
  - `Aetherium.Server/Program.cs` — SQLite branch in the `ORLEANS_STORAGE` switch; `IWorldSnapshotStore` DI binding.
  - `Aetherium.Server/Aetherium.Server.csproj` — new NuGet dependencies.
  - `Aetherium.Server/Persistence/SqliteWorldSnapshotStore.cs` (new) — SQLite implementation of `IWorldSnapshotStore`.
  - `Aetherium.Server/MultiWorld/GameMapGrain.cs` — `FanOutAsync` calls `AppendDeltaAsync`; `OnActivateAsync` loads snapshot + replays deltas; compaction timer.
  - `Aetherium.Server/MultiWorld/WorldSnapshotBuilder.cs` — produces serialized entity payloads.
  - `Aetherium.Test/Persistence/*` (new) — durability smoke tests and `SqliteWorldSnapshotStore` round-trip tests.
- **Not breaking**: `MemoryWorldSnapshotStore` remains the default for tests; `ORLEANS_STORAGE=memory` (current default) keeps the existing in-memory behavior; opting into SQLite is purely additive. Existing 755 tests continue to pass without modification.
- **Operational footprint**: One SQLite file per environment (configurable via `AETHERIUM_DATA_DIR`). No new services or daemons.
