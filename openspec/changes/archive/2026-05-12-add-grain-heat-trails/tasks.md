# Implementation Tasks

## 1. HeatTrailTracker API extension
- [x] 1.1 Added `RemoveTrailsAt(WorldLocation)` to `HeatTrailTracker`. Removes all trail entries at the given cell (for applying `HeatExpiredDelta` on the session side)
- [x] 1.2 Added `RecordRaw(entityId, location, timestamp, intensity, duration)` overload — lets `GameSession.ApplyDelta` record heat from delta payloads when the source entity isn't in the local mirror
- [x] 1.3 Added `SnapshotCounts()` for tests/diagnostics — returns a read-only `(location → trail-count)` map. Lets the grain compute the before/after diff for emitting expiry deltas

## 2. Grain ownership
- [x] 2.1 Added `private readonly HeatTrailTracker _heatTracker = new()` field on `GameMapGrain`
- [x] 2.2 New `AttachWorldEventSubscriber()` helper hooks `_world.WorldEvents += OnWorldEvent`. Called from `InitializeAsync` (first activation) and `OnActivateAsync` (silo restart rehydration)
- [x] 2.3 `OnWorldEvent` filters to `EntityMoved` events on entities with a `HeatSignature` component; records the trail; emits a `HeatRecordedDelta` via `FanOutAsync`
- [x] 2.4 `TickAsync` runs heat cleanup: snapshot cell counts → `_heatTracker.CleanupOldTrails(cutoff)` → diff cell sets → emit `HeatExpiredDelta` only for cells that just lost their last trail. No spam for already-empty cells
- [x] 2.5 Subscriber failures are wrapped in try/catch so heat translation issues don't roll back the underlying mutation

## 3. Session-side reconciliation
- [x] 3.1 `GameSession.ApplyDelta` `HeatRecordedDelta` handler: looks up the entity in the local mirror to use its `HeatSignature` duration. Falls back to `RecordRaw` with a 10-second default duration when the entity isn't mirrored — heat is observable independently of entity visibility
- [x] 3.2 `HeatExpiredDelta` handler: calls `HeatTracker.RemoveTrailsAt`
- [x] 3.3 `GameSession.UpdateHeatTracker()` deleted; `GetPerception` no longer calls it. The "iterate-entities-to-collect-heat" pattern is gone
- [x] 3.4 `HeatTracker` instance and `GetHeatAtLocation` consumers (vision modes, perception layer) unchanged

## 4. Tests
- [x] 4.1 `SessionHeatMirrorTests` (xUnit, 4 tests): HeatRecordedDelta updates the mirror for known entities; HeatRecordedDelta records even for unknown entities (perception-independence); HeatExpiredDelta removes per-cell trails; GetPerception does NOT self-collect heat (regression guard against the deleted UpdateHeatTracker)
- [ ] 4.2 Grain-side `GrainHeatTrailTests` — deferred. The grain's `_heatTracker` is private and the integration would require either: (a) exposing it via a test hook on `IGameMapGrain` (pollutes the public surface), or (b) observing emitted deltas through the host-side broker (requires the full SignalR test stand, which is what `add-end-to-end-shared-mutation-tests` builds). The integration is naturally covered by the next change rather than duplicating scaffolding here
- [ ] 4.3 Phase 3 (`add-end-to-end-shared-mutation-tests`) will validate the grain → delta → session-mirror loop end-to-end, which exercises this change's wire path as a side effect

## 5. Validate
- [x] 5.1 All 744 tests pass (740 baseline + 4 new heat-mirror tests)
- [x] 5.2 `dotnet build` clean for Server and Test projects
- [x] 5.3 Confirmed via grep: no remaining references to `UpdateHeatTracker` anywhere in `Aetherium.Server`
