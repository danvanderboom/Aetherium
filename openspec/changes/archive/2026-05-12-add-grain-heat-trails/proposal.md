# Add Grain-Authoritative Heat Trail Tracking

## Why

Phase 2c shipped the wire-format hooks for grain-authoritative heat trails (`HeatRecordedDelta`, `HeatExpiredDelta`, placeholder handlers in `GameSession.ApplyDelta`) but didn't actually wire them. Heat trails today are still session-local: each `GameSession` owns its own `HeatTrailTracker` and `UpdateHeatTracker()` runs on every `GetPerception()` call, iterating every entity in the session's mirror. Two consequences:

1. **Heat is divergent.** Two sessions in the same map can disagree about which cells are hot. That contradicts the design principle (D9 from phase 2c) that heat is "objective reality of the world" — a recently-walked cell is hot regardless of who's looking. Whether a session can *perceive* the trail (e.g. `VisionMode.Infrared` vs `Normal`) is a session-side filter on the same underlying data, but the data should be the same.
2. **Tick-driven movement is invisible.** Monsters moving during world ticks (the `WorldTickService` → `IWorldGrain.TickAsync` → region grains chain) update the grain's `_world` but never touch any session's `HeatTrailTracker`. Even single-player heat tracking misses NPC movement unless the session happens to call `GetPerception` while the monster is at the cell.

This change moves heat trail ownership to `GameMapGrain` and uses the phase-2c delta broker to keep session mirrors in sync.

## What Changes

- New field `HeatTrailTracker _heatTracker` on `GameMapGrain`. Owned by the grain; persisted with the rest of the map state via the existing recipe-driven reactivation (heat is transient and doesn't need separate persistence; on silo restart heat resets to empty, which matches the recipe-rehydration semantics).
- `GameMapGrain.OnActivateAsync` subscribes to `_world.WorldEvents`. On `EntityMoved`, if the entity has a `HeatSignature` component, the grain records heat in `_heatTracker` and emits `HeatRecordedDelta { EntityId, X, Y, Z, GameTimeHours, Intensity }`.
- `GameMapGrain.TickAsync` calls `_heatTracker.CleanupOldTrails(cutoff)` and emits `HeatExpiredDelta { X, Y, Z }` for each cleared cell. Cutoff is `currentGameTime - 60 seconds` (same as the current per-session cleanup window).
- `GameSession.ApplyDelta` is updated: the existing `HeatRecordedDelta` placeholder now correctly attaches the entity reference looked up from the session's mirror. `HeatExpiredDelta` triggers a per-location cleanup on the local tracker.
- `GameSession.UpdateHeatTracker()` (the per-perception iteration) is **deleted**. Heat data flows in via deltas only. Sessions in grain-bound mode receive heat updates automatically. Sessions in legacy mode (no grain binding) lose the self-iterating heat update — but legacy mode is test/diagnostic only and the diagnostic worlds don't have entities with `HeatSignature` (player only, and player heat isn't relevant to itself).
- New tests verify: a grain-bound session's local `HeatTracker` receives heat records when entities move; two sessions in the same map have identical heat data after a sequence of movements; expired trails get the cleanup delta.

## Impact

- Affected specs:
  - `client-server-communication` — MODIFIED `Heat Trail Tracking is Grain-Authoritative` (the placeholder requirement from phase 2c now has fully-wired scenarios)
- Affected code:
  - `Aetherium.Server/MultiWorld/GameMapGrain.cs` — `_heatTracker` field; `OnActivateAsync` subscribes to `_world.WorldEvents`; `TickAsync` runs cleanup and emits expiry deltas; `EntityMoved` handler emits `HeatRecordedDelta`
  - `Aetherium.Server/GameSession.cs` — `ApplyDelta` handler for `HeatRecordedDelta` attaches the entity reference; `HeatExpiredDelta` removes per-cell trails; `UpdateHeatTracker` deleted; `GetPerception` no longer calls it
  - `Aetherium.Server/Perception/HeatTrailTracker.cs` — add `RemoveTrailsAt(WorldLocation)` method for the expiry delta to call; existing API otherwise unchanged
- Tests:
  - New `GrainHeatTrailTests` (NUnit + Orleans TestingHost): verifies heat-on-move emission and expiry delta firing
  - New `SessionHeatMirrorTests` (xUnit): verifies `ApplyDelta` correctly mutates the local tracker for both delta types
- **Non-breaking**: 740 existing tests continue to pass. The deleted `UpdateHeatTracker` was only invoked from inside `GetPerception`; removing it means perception no longer self-collects heat, but heat-aware perception tests already work against pre-populated trackers
