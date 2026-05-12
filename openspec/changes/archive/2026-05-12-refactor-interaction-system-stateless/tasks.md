# Implementation Tasks

## 1. ActionContext record
- [x] 1.1 Added `public sealed record class ActionContext(World World, Character Player, WorldLocation ViewLocation)` to `Aetherium.Server/InteractionSystem.cs`. Non-null fields enforced by record validation

## 2. InteractionSystem overload pairs (narrowed scope)
- [x] 2.1 `TryPickup`: added `(ActionContext, string targetEntityId)` canonical overload; session overload now a forwarder
- [x] 2.2 `TryDrop`: same pattern
- [x] 2.3 `TryOpen`: same pattern; underlying `ToggleDoor` private helper migrated to ActionContext
- [x] 2.4 `TryClose`: same pattern
- [ ] 2.5 — 2.9 **Deferred to a future change.** `TryUse` and the disambiguation chain (`GetUseOptions`, `TryUseWithMode`, `TryActivate`, `TryConsume`, `TryPlace`, `TryClimb`, `TryForceOpen`, `TryLockpick`, `TryEquip`) remain session-bound. Migrating them depends on extending the delta DTO vocabulary to cover consume/place/lockpick/climb post-conditions. Two existing internal call sites in `TryActivate` and `TryForceOpen` that call `ToggleDoor` were updated to construct an `ActionContext` from session inline (these callers are still session-rooted)

## 3. Grain method consolidation
- [x] 3.1 `GameMapGrain.PickupAsync` rewritten: builds `ActionContext` via new private `TryBuildActionContext(sessionId)` helper, captures target type for the delta placement, calls `_interactionSystem.TryPickup(ctx, target)`, emits `ItemTransferredDelta` on success
- [x] 3.2 `GameMapGrain.DropAsync`: same delegation pattern
- [x] 3.3 `GameMapGrain.OpenAsync` / `CloseAsync`: delegate to `_interactionSystem.TryOpen` / `TryClose`; emit `DoorStateChangedDelta` on success
- [x] 3.4 Native `ToggleDoorAsync` helper in `GameMapGrain` rewritten as a delegating wrapper (kept as `ToggleDoorAsync` private helper for Open/Close clarity, no longer duplicates the InteractionSystem logic)
- [x] 3.5 Native pickup/drop implementation in `GameMapGrain` deleted — the grain method bodies dropped by ~60 lines
- [x] 3.6 `GameMapGrain.UseAsync` stays — key-on-door only; documented as a known limitation

## 4. Validate
- [x] 4.1 All 740 tests pass without modification — same count and result set as the post-phase-2d baseline
- [x] 4.2 `Aetherium.Server` and `Aetherium.Test` build clean. Pre-existing build failures in `Aetherium.Dashboard` (unrelated: Orleans `IClusterClient.Connect/Close` API drift, missing `BehaviorAnalysis` properties) are not introduced by this change
- [x] 4.3 Audit confirmed: `GameMapGrain.cs` Pickup/Drop/Open/Close are now thin wrappers (~12 lines each) over `_interactionSystem.Try*(ctx, ...)` calls. The `TryBuildActionContext(sessionId)` helper centralizes the player-lookup boilerplate
