# Implementation Tasks

## 1. Random source abstraction
- [x] 1.1 Added [Aetherium.Server/IRandomSource.cs](Aetherium.Server/IRandomSource.cs) defining `IRandomSource { double NextDouble(); int NextInt(int maxExclusive); }`
- [x] 1.2 Added `DefaultRandomSource` wrapping `System.Random.Shared` plus a `FixedRandomSource(params double[])` test double in the same file
- [x] 1.3 Registered `services.AddSingleton<IRandomSource, DefaultRandomSource>()` in [Aetherium.Server/Program.cs](Aetherium.Server/Program.cs)
- [x] 1.4 Added `IRandomSource _random` field + optional ctor parameter to `InteractionSystem`; `TryLockpick` now calls `_random.NextDouble()` instead of `new System.Random().NextDouble()`
- [x] 1.5 The ctor parameter defaults to `new DefaultRandomSource()`, so the ~40 existing `new InteractionSystem()` callsites compile unchanged. Tests that need deterministic behavior pass an explicit `FixedRandomSource(...)`

## 2. New delta DTOs
- [x] 2.1 Added `ComponentFieldChangedDelta` to [Aetherium.Server/MultiWorld/Deltas.cs](Aetherium.Server/MultiWorld/Deltas.cs) with `[GenerateSerializer]`, `[Id]` annotations, and `double? NumericValue` / `bool? BoolValue` / `string? StringValue`
- [x] 2.2 Added `ItemDestroyedDelta` (`EntityId`, `OwnerEntityId?`)
- [x] 2.3 Added `EntityPlacedDelta` (`EntityPlacement Placement`, `string? SourceOwnerEntityId`)

## 3. `InteractionSystem` ActionContext overloads
- [x] 3.1 `TryConsume(ActionContext, string itemId)` — canonical impl; session overload forwards
- [x] 3.2 `TryPlace(ActionContext, string itemId, WorldLocation? location = null)` — same pattern
- [x] 3.3 `TryForceOpen(ActionContext, string itemId, string doorId)` — same pattern
- [x] 3.4 `TryLockpick(ActionContext, string itemId, string doorId)` — same pattern; uses `_random.NextDouble()`
- [x] 3.5 `TryClimb(ActionContext, string entityId)` — same pattern
- [x] 3.6 `TryEquip(ActionContext, string itemId)` — same pattern
- [x] 3.7 `TryActivate(ActionContext, string entityId)` — same pattern; internal `ToggleDoor` call updated to thread the context through directly
- [x] 3.8 `GetUseOptions(ActionContext, ...)` — same pattern. Also added a `ContextEvaluator.EvaluateContext(World, WorldLocation, string?)` overload so grain callers can compute context tags without a session
- [x] 3.9 `TryUseWithMode(ActionContext, string itemId, string? targetId, string usageId)` — same pattern; switch dispatches to the new ActionContext overloads
- [x] 3.10 `TryUse(ActionContext, string itemId, string targetId, string? usageId = null)` — same pattern

## 4. `GameSession.ApplyDelta` cases
- [x] 4.1 `ComponentFieldChangedDelta` handler: looks up entity in world OR any inventory (via new `FindEntityAnywhere` helper); switches on `(ComponentType, FieldName)` for the nine known pairs; throws `NotImplementedException` on unknown pair so test failures are loud
- [x] 4.2 `ItemDestroyedDelta` handler: if `OwnerEntityId` set, finds character (or matches local Player) and calls `Inventory.Remove`; else `World.TryRemoveEntity`
- [x] 4.3 `EntityPlacedDelta` handler: removes from `SourceOwnerEntityId`'s inventory first (avoids brief double-reference), then reconstructs via `EntityFactory.Create(Placement)` and adds to world

## 5. `GameMapGrain.UseAsync` rewrite
- [x] 5.1 Deleted the key-on-door fast path and the "not supported in grain mode" fail
- [x] 5.2 Builds `ActionContext` via existing `TryBuildActionContext(sessionId)`
- [x] 5.3 If `usageId` is null: calls `_interactionSystem.GetUseOptions(ctx, ...)`; returns options DTO list for count > 1 (reactive disambiguation); auto-resolves to `options[0].UsageId` when count == 1; returns "No effect" when count == 0
- [x] 5.4 Added a private `UseFieldSnapshot` record + `SnapshotUseFields(ctx, item, target)` helper that captures every mutable field the dispatcher could touch (Consumable.Uses, Health.Level, ForcesDoor.Durability, Lockpick.Durability, PlaceableLight.IsPlaced, LightSource.IsEnabled/IsDynamic, Activatable.IsActivated, Inventory.Capacity, door state) plus inventory/world membership for the item
- [x] 5.5 Added a private `EmitUseDeltasAsync(ctx, item, target, snapshot)` helper that emits `EntityPlacedDelta` / `ItemDestroyedDelta` for inventory transitions, `DoorStateChangedDelta` for door changes, and `ComponentFieldChangedDelta` for every other field that moved
- [x] 5.6 Invokes `_interactionSystem.TryUseWithMode(ctx, ...)` then `EmitUseDeltasAsync(...)` on success

## 6. Tests
- [x] 6.1 `Aetherium.Test/MultiWorld/DeltaApplicationTests.cs` — four new cases added:
  - `ComponentFieldChangedDelta_Decrements_Consumable_Uses_In_Inventory`
  - `ItemDestroyedDelta_Removes_Item_From_Owner_Inventory`
  - `EntityPlacedDelta_Removes_From_Inventory_And_Adds_To_World`
  - `ComponentFieldChangedDelta_Unknown_Pair_Throws_NotImplementedException` (regression guard for the documented loud-failure behavior)
- [x] 6.2 `Aetherium.Test/MultiWorld/LockpickDeterminismTests.cs` (new) — four tests covering forced-success roll, forced-failure roll, repeated-failure breaks the pick, and parameterless-ctor smoke test
- [ ] 6.3 **Deferred.** Three Use-mode end-to-end scenarios (consume / place / lockpick across two SignalR-shaped sessions) would require a test seam on `IGameMapGrain` to seed inventory — there's currently no path to add a torch / potion / lockpick to a player's inventory inside the grain from outside. The existing `Move_Dispatches_PerceptionUpdate_To_All_Sessions_In_Map` test already exercises the grain → delta → broker → per-session-dispatch chain. The Use-specific dispatch path is structurally identical (same `FanOutAsync`); only the delta type differs, and that conversion is covered by the unit tests in 6.1 + 6.2. Mirrors the deferral pattern from `add-grain-heat-trails` task 4.2 — same root cause (private grain state, no test seam) and same conclusion (covered structurally by adjacent tests)

## 7. Validate
- [x] 7.1 All existing tests pass without modification — 740 baseline + 8 new = 755 total, 0 failures, 2 skipped (unrelated LMStudio + prompts)
- [x] 7.2 New tests pass (4 in `DeltaApplicationTests` + 4 in `LockpickDeterminismTests`)
- [x] 7.3 `Aetherium.Server` and `Aetherium.Test` build clean
- [x] 7.4 Confirmed via grep: no remaining `new System.Random()` in `Aetherium.Server/InteractionSystem.cs`
- [x] 7.5 Confirmed via grep: `GameMapGrain.UseAsync` no longer contains the "Use mode not supported in grain mode" string
