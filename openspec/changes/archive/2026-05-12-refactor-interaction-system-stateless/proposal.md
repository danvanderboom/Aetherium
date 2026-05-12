# Refactor InteractionSystem to Stateless API

## Why

After phase 2c+2d, `InteractionSystem` is still session-coupled: every method takes a `GameSession` and reads `session.Player` / `session.ViewLocation` / `session.World`. Two consequences:

1. **`GameMapGrain` can't reuse it.** Phase 2c grain mutation methods reimplemented Pickup/Drop/Open/Close natively because there's no API surface that takes raw `(World, Character, WorldLocation)`. Grain `UseAsync` is limited to "key on locked door" because reimplementing the disambiguation logic from scratch was prohibitive.
2. **Code is duplicated.** `LocalMutationGateway` calls `InteractionSystem.TryPickup(session, ...)`; the grain has its own pickup logic. Bug fixes in one don't propagate to the other.

This change adds an `ActionContext` record and a parallel set of overloads on every `InteractionSystem.Try*` method that take `ActionContext` instead of `GameSession`. The session-taking overloads become forwarders. `GameMapGrain.PickupAsync` / `DropAsync` / `OpenAsync` / `CloseAsync` are rewritten to delegate to `InteractionSystem` via the new API, eliminating the duplication for those four verbs.

`UseAsync` is intentionally not migrated in this change. The disambiguation logic in `InteractionSystem.TryUse` triggers a wide range of post-conditions (item destruction on consume, item placement on place, character relocation on climb, lockpick attempts, etc.) that need new delta DTOs to round-trip correctly. Bundling that in here would inflate scope. A future change can extend the delta vocabulary and complete the Use migration.

## What Changes

- New `ActionContext` record: `(World World, Character Player, WorldLocation ViewLocation)`. Lives in `Aetherium.Server` namespace.
- New `InteractionSystem.Try*(ActionContext, ...)` overloads for all 14 methods: `TryPickup`, `TryDrop`, `TryOpen`, `TryClose`, `TryUse`, `GetUseOptions`, `TryUseWithMode`, `ToggleDoor`, `TryActivate`, `TryConsume`, `TryPlace`, `TryClimb`, `TryForceOpen`, `TryLockpick`, `TryEquip`.
- Existing session-taking overloads become 2-line forwarders that build an `ActionContext` and call the new overload. Backward-compatible.
- `GameMapGrain.PickupAsync` / `DropAsync` / `OpenAsync` / `CloseAsync` rewritten to call `InteractionSystem.Try*(ActionContext, ...)`. Each grain method then emits the appropriate `MapDelta` based on the verb's result. Native pickup/drop/door-toggle logic in the grain is deleted.
- `GameMapGrain.UseAsync` stays as-is (key-on-door only). Documented as a known limitation in the proposal's design note.
- **Not breaking**: every consumer of `InteractionSystem` continues to work. `LocalMutationGateway` still calls the session-taking overloads. All 700+ tests pass unchanged.

## Impact

- Affected specs:
  - `client-server-communication` — MODIFIED `Grain Mutation Methods` (Pickup/Drop/Open/Close now delegate to InteractionSystem)
- Affected code:
  - `Aetherium.Server/InteractionSystem.cs` — add `ActionContext` record; add 14 new overloads; rewrite internals to use `ActionContext` exclusively; session overloads become forwarders
  - `Aetherium.Server/MultiWorld/GameMapGrain.cs` — rewrite `PickupAsync`/`DropAsync`/`OpenAsync`/`CloseAsync` (private `ToggleDoorAsync` helper removed; native implementations deleted)
- Tests:
  - No new test file required. The 700+ existing tests that exercise `InteractionSystem` via session-taking overloads continue to validate behavior end-to-end. Adding direct tests of the `ActionContext` overloads is low-value because they share implementations with the session overloads
- Defers:
  - Full grain-mode `Use` disambiguation (consume/place/lockpick/climb). Requires new delta DTOs and is its own change
