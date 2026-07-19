# Harden world persistence: heat durability + observable delta-append failures (P3-8)

## Why

Two follow-ups noted inside the (otherwise done) P3-8 persistence work:

1. **Heat trails are lost on cold start.** The grain-authoritative per-cell heat map
   (`GameMapGrain._heatTracker`, a `HeatTrailTracker`) is a transient field — it is never
   serialized into the snapshot and `MapDeltaReplayer` deliberately no-ops heat deltas on
   replay. After a silo restart the heat map starts empty and only rebuilds as entities move
   again, so infrared/heat-vision perception silently loses all recent-trail state across a
   restart. (Confirmed: `WorldSnapshot` has no heat field, `WorldSnapshotBuilder.SnapshotOf`
   has no access to the tracker, and `MapDeltaReplayer.Apply` skips `HeatRecordedDelta`/
   `HeatExpiredDelta`.)

2. **`PersistDeltaAsync` swallows failures.** On an `AppendMapDeltaAsync` failure the grain
   only does `Console.WriteLine` (which also corrupts any Spectre TUI) and continues. There is
   no structured logging, no failure counter, and no recovery — a delta that fails to persist
   is silently dropped from the durable log, so on cold-start replay that mutation is lost with
   no operator-visible signal.

## What Changes

- **Heat durability:** add serializable heat-trail export/import to `HeatTrailTracker`; carry
  the exported trails on `RegionStateSnapshot` (a new `[Id(15)]` field — the snapshot is stored
  as an Orleans-serialized blob, so this needs no store schema change and old snapshots load
  with the field null). `ForceSnapshotAsync` captures the live trails; `TryHydrateFromSnapshotAsync`
  restores them. Fully-faded trails are dropped at capture time. Original trail timestamps are
  preserved so fade math continues correctly across the restart.
- **Observable failures + self-heal:** resolve an `ILogger<GameMapGrain>` and replace the
  persistence-path `Console.WriteLine`s with structured logging. Track a delta-append failure
  count + last error + timestamp, and expose them via a new `GetPersistenceHealthAsync()` on
  `IGameMapGrain`. On an append failure mark persistence dirty; on the next successful append,
  if dirty, force a healing snapshot (a full snapshot supersedes any deltas that failed to
  append) and clear the flag.

## Impact

- Affected specs: **world-persistence** (heat-durability requirement; delta-append
  failure-handling + observability requirement).
- Affected code: `Aetherium.Server/Perception/HeatTrailTracker.cs`,
  `Aetherium.Server/Persistence/RegionStateSnapshot.cs`,
  `Aetherium.Server/MultiWorld/GameMapGrain.cs`,
  `Aetherium.Server/MultiWorld/IGameMapGrain.cs`, and a new `PersistenceHealthDto`
  (`Aetherium.Model`).
- No breaking wire/schema changes: `RegionStateSnapshot` gains a nullable Orleans `[Id]` field;
  `WorldSnapshot` is untouched (joiner hydration path unchanged); the new grain method is additive.
- Tests: heat survives a snapshot→hydrate round-trip; a throwing store increments the failure
  count / marks unhealthy (not silently swallowed) and a later success triggers a heal snapshot.
