# Design Notes

## Why hybrid (snapshot + delta log), not pure event sourcing?

Pure event sourcing — keep an append-only log of every `MapDelta` from genesis and reconstruct world state by replay — has two attractive properties: the delta vocabulary is *already* the event log, and recovery is conceptually trivial. The reasons we don't go that route:

1. **Unbounded recovery time.** A long-running world accumulates millions of deltas. Replaying every one on cold start eventually dwarfs any reasonable startup budget.
2. **Forever-compatibility burden.** Every delta type ever emitted must remain replayable, which freezes the schema. Even cosmetic refactors of `MapDelta` subclasses become breaking.

The hybrid approach — periodic snapshot + delta log since the snapshot — keeps recovery bounded by compaction cadence and lets old delta types retire once the snapshot covering them is in place.

## Why not Orleans built-in persistence alone?

`[PersistentState]` writes the entire grain state through `WriteStateAsync`. For `GameMapGrain` that means re-serializing the full world on every door toggle. The codebase already noted this gap (the `SerializedWorld` slot at `GameMapGrain.cs:1325` carries a `// Phase 2 will replace this with real World serialization` comment). Orleans persistence is the right substrate for *metadata* (the `WorldRecipe`, sequence counters, region directories) but not for hot per-mutation writes. The hybrid plan uses both: Orleans persistence for the snapshot blob and metadata; the delta log for incremental writes between snapshots.

## Why SQLite, not Postgres / EventStore / Azure Table / file blobs?

- **SQLite** is single-file, zero-config, embedded, transactional, supports concurrent reads under WAL, and has battle-tested C# bindings (`Microsoft.Data.Sqlite`). Local-first development and small-scale deployments don't need a separate service.
- **Postgres / EventStore** introduce an external service and ops overhead. Acceptable later (the `IWorldSnapshotStore` interface stays the same — only the implementation swaps), but premature today.
- **Azure Table Storage** is already half-wired in `Program.cs` for grain state, but Table is awkward for the delta log: row-size limits, ordering quirks, no batched range scans by `Sequence`. Keep Azure Table as the *grain* provider for cloud deployments; use SQLite for the delta log regardless of grain provider. (Cloud delta-log store can ride on a future change with a `BlobWorldSnapshotStore` or similar.)
- **Plain file blobs** lose transactional append guarantees and complicate compaction.

## Sequence numbers as the durability anchor

`GameMapGrain._nextSequence` (`GameMapGrain.cs:38`) is the existing monotonic counter assigned to every delta in `FanOutAsync`. We make it load-bearing for persistence:

- Every `delta_log` row stores its `Sequence`.
- Every `region_snapshots` row stores the highest `Sequence` it incorporated.
- Cold start: read the snapshot, set `_nextSequence` to `snapshot.Sequence + 1`, replay all `delta_log` rows where `Sequence > snapshot.Sequence`, set `_nextSequence` to the highest replayed sequence + 1.
- Compaction: produce a snapshot at the current `_nextSequence - 1`, then `DELETE FROM delta_log WHERE sequence <= snapshot.Sequence`.

This makes the recovery procedure deterministic and the compaction race-free (the snapshot's sequence is a high-water mark; nothing earlier needs to remain in the log).

## Delta idempotency audit

For the log replay to be safe, every delta must be re-applicable without "double-counting." The current vocabulary already meets this:

| Delta | Idempotent? | Notes |
|---|---|---|
| `EntityMovedDelta` | ✅ | Carries `Old*` and `New*`; absolute position. |
| `EntityAddedDelta` | ✅ | Carries `EntityPlacement`; reconstructs entity. |
| `EntityRemovedDelta` | ✅ | Removal by id; replay-tolerant if already removed. |
| `EntityHeadingChangedDelta` | ✅ | Absolute degrees. |
| `DoorStateChangedDelta` | ✅ | Absolute `IsOpen` / `IsLocked`. |
| `ItemTransferredDelta` | ✅ | Symmetric; absolute owner + placement. |
| `ComponentFieldChangedDelta` | ✅ | Absolute field values (numeric/bool/string). |
| `ItemDestroyedDelta` | ✅ | Removal by id; same tolerance as `EntityRemovedDelta`. |
| `EntityPlacedDelta` | ✅ | Carries placement; idempotent reconstruction. |
| `HeatRecordedDelta` | ⚠️ | Additive; replaying *adds* heat again. Mitigation: snapshot includes the full heat map, so post-snapshot replay starts from a known baseline. |
| `HeatExpiredDelta` | ✅ | Cell-keyed removal; idempotent. |

Heat trails are the one case requiring care. They're already grain-authoritative in `_heatTracker`, so the snapshot path captures the full state; the delta log handles only the recent additive tail. Provided compaction runs reasonably often (every N minutes), drift is bounded.

## Schema evolution

Two axes to manage:

1. **`RegionStateSnapshot` schema changes.** Orleans `[GenerateSerializer]` + `[Id]` annotations already give us forward-compatible field addition. The `SnapshotVersion` int (already on `WorldSnapshot.SnapshotVersion`) covers breaking layout changes — bump it, then refuse to load older snapshots.
2. **`MapDelta` subclass schema changes.** Same `[Id]` story for additive fields. For deltas that retire entirely, the rule is: a release that removes a delta type must ship with a compaction migration that produces a snapshot covering all deltas-of-the-retiring-type, then drops them from the log.

The `delta_log` table gets a `delta_type_version` column so a future Aetherium can detect "I have a delta of type X version 3 but this binary only handles version 4" and either upgrade-in-place or refuse with a clear error rather than silently mis-applying.

## Performance budget

Local SQLite (WAL, single writer, journal_mode=WAL, synchronous=NORMAL) routinely sustains 10k+ small inserts/sec. Aetherium delta rates are dominated by movement and heat updates — peak observed at ~200/sec across a busy map. Headroom is comfortable.

A benchmark test asserts `AppendDeltaAsync` p99 < 1ms; if that fails, the fallback is to batch-append per tick instead of per delta (group all deltas emitted in one `FanOutAsync` round into one INSERT).

## Test seam

`MemoryWorldSnapshotStore` already exists and is the default in tests. Phase B adds `SqliteWorldSnapshotStore` with the same interface. `Aetherium.Test/Persistence/SqliteWorldSnapshotStoreTests` covers:

- Round-trip a snapshot, list region IDs, load by id.
- Append N deltas, query `GetDeltasSinceAsync(t)` and assert correct ordering by `Sequence`.
- Compaction: save snapshot at sequence S, append more deltas, call `CompactDeltasAsync`, verify rows with `sequence <= S` are gone and rows above are intact.
- Concurrent reads under WAL while a writer is appending.

End-to-end: `Aetherium.Test/Persistence/MapGrainRecoveryTests` stands up a `TestCluster`, mutates state, "reboots" the silo by reactivating the grain with the same storage backend, and asserts the post-reboot world matches the pre-reboot world.

## Non-goals

- **Cloud-scale snapshot store.** A `BlobWorldSnapshotStore` for Azure Blob is a follow-up. The interface won't change.
- **Per-session perception cache persistence.** `SpaceTimeMemory` is large and rebuildable on reconnect from the perception fan-out. Skipping until a real need surfaces.
- **Cross-region transactional consistency.** Each region's snapshot+log is independent. Multi-region atomic mutations (item teleport across regions) remain a single `GameMapGrain` operation today; if that changes, the cross-region story needs its own design.
- **Encryption at rest.** Local SQLite file inherits filesystem permissions; sufficient for dev and self-hosted. Hosted deployments handle this at the storage layer.
- **Player-account persistence (auth, profile, saves-as-a-game-concept).** This change persists *world* state, not user accounts. Player save slots are a separate concept and a separate change.
