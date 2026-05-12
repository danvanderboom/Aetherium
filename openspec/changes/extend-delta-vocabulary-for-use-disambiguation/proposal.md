# Extend Delta Vocabulary for Use Disambiguation

## Why

`refactor-interaction-system-stateless` migrated `TryPickup` / `TryDrop` / `TryOpen` / `TryClose` to `ActionContext` and rewired the grain to delegate to `InteractionSystem`. It explicitly deferred the rest of the Use chain (`TryConsume`, `TryPlace`, `TryForceOpen`, `TryLockpick`, `TryClimb`, `TryEquip`, `TryActivate`, plus the `TryUseWithMode` dispatcher) because their post-conditions can't round-trip through the current delta vocabulary:

- `TryConsume` decrements `Consumable.Uses`, mutates `Health.Level`, and removes the item from inventory when uses hit zero. The current `ItemTransferredDelta` only covers world↔inventory transfers; it has no concept of an item ceasing to exist, and no delta exists for a numeric component field changing in place.
- `TryPlace` flips `PlaceableLight.IsPlaced` and `LightSource.IsDynamic` / `IsEnabled`, removes from inventory, and adds the same entity instance to the world at a location. Receivers would need to reconstruct the entity in place with the new component state, which `ItemTransferredDelta` (geared toward pickup) doesn't express.
- `TryForceOpen` and `TryLockpick` both decrement durability and may destroy the tool on zero, in addition to mutating door state. The destroy step has no representation.
- `TryActivate` flips `Activatable.IsActivated` and cascades to `LightSource.IsEnabled` / `OpensAndCloses` on target entities.
- `TryLockpick` separately introduces non-determinism: it instantiates `new System.Random()` per call, which is both wasteful (re-seeding from the clock every invocation) and untestable. Two grain calls within the same tick can produce identical results purely by accident of clock resolution.

This change extends the delta vocabulary, threads a swappable random source through `InteractionSystem`, migrates the remaining Use verbs to `ActionContext` overloads, and rewires `GameMapGrain.UseAsync` to delegate via `TryUseWithMode` so every Use mode works end-to-end across grain-bound sessions.

## What Changes

### New delta DTOs (`Aetherium.Server/MultiWorld/Deltas.cs`)
- **`ComponentFieldChangedDelta`** — a generic `(EntityId, ComponentType, FieldName, double NumericValue?, bool BoolValue?, string StringValue?)` carrier. Covers `Consumable.Uses`, `Health.Level`, `*Durability`, `PlaceableLight.IsPlaced`, `LightSource.IsEnabled`, `LightSource.IsDynamic`, `Activatable.IsActivated`, `Inventory.Capacity`. Receivers dispatch on `(ComponentType, FieldName)` to set the field. Discriminator: exactly one of the three value fields is populated per delta; the field type is implied by the component+field pair.
- **`ItemDestroyedDelta`** — `(EntityId, OwnerEntityId?)`. Emitted when an item's `Uses` / `Durability` hits zero and it's removed from inventory or world for good. Receivers remove from owner's inventory (if `OwnerEntityId` set) or from `World.Entities` directly.
- **`EntityPlacedDelta`** — `(EntityPlacement Placement, string SourceOwnerEntityId?)`. Emitted by `TryPlace` when an item leaves inventory and enters the world with mutated component state baked into the placement. Distinguishes "item placed from inventory" from generic `EntityAddedDelta` (which models grain-generated entities). Receivers remove from the source owner's inventory and reconstruct in the world via `EntityFactory`.

### Random source abstraction (`Aetherium.Server`)
- **`IRandomSource`** interface: `double NextDouble()` and `int NextInt(int maxExclusive)`.
- **`DefaultRandomSource`** wrapping `System.Random.Shared` (thread-safe, process-wide). Registered in DI.
- `InteractionSystem` gains an `IRandomSource` constructor parameter; `TryLockpick` calls `_random.NextDouble()` instead of `new System.Random().NextDouble()`. Tests substitute a `FixedRandomSource(double[] sequence)` to drive deterministic outcomes.
- **Non-goal**: per-grain seeded RNG for full-replay determinism. Single shared `Random.Shared` is sufficient for the current "live gameplay + injectable for tests" requirement. Per-map seeding can ride on a future grain-persistence change without breaking this interface.

### `InteractionSystem` API extension
- Add `ActionContext` overloads for: `TryConsume`, `TryPlace`, `TryForceOpen`, `TryLockpick`, `TryClimb`, `TryEquip`, `TryActivate`, `GetUseOptions`, `TryUseWithMode`, `TryUse`. Session overloads remain as forwarders.
- `TryUseWithMode(ActionContext, ...)` dispatches to the new `ActionContext` overloads of the per-mode methods.
- The internal `ToggleDoor` callers already use `ActionContext`; no change needed there.

### `GameMapGrain.UseAsync` rewrite
- Build an `ActionContext` via the existing `TryBuildActionContext(sessionId)` helper.
- Delegate to `_interactionSystem.TryUseWithMode(ctx, itemEntityId, onEntityId, usageId ?? "unlock-door")`. When `usageId` is null, fall back to the existing `GetUseOptions` path for disambiguation.
- After a successful call, emit the appropriate deltas based on which fields the method mutated. Strategy: snapshot the affected component fields before the call, diff after, and emit one `ComponentFieldChangedDelta` per changed field plus an `ItemDestroyedDelta` if the item was removed from inventory.
- The "Use mode not supported in grain mode" fail-path at [GameMapGrain.cs:1067](Aetherium.Server/MultiWorld/GameMapGrain.cs:1067) is deleted.

### `GameSession.ApplyDelta`
- New cases for the three new delta types.
- `ComponentFieldChangedDelta` looks up the entity, finds the named component (via reflection over `Entity.AllComponents`), and sets the field. To avoid reflection cost on the hot path, dispatch via a small static switch on `(ComponentType, FieldName)` for the eight known pairs; throw `NotImplementedException` for unknowns so test failures are loud.
- `ItemDestroyedDelta` removes from inventory if `OwnerEntityId` is set, else from `World.Entities`.
- `EntityPlacedDelta` removes from `SourceOwnerEntityId`'s inventory and calls `EntityFactory.Create(Placement)` to add to the world.

### Tests
- `DeltaApplicationTests` extended with one case per new delta type.
- `EndToEndSharedMutationTests` extended with three scenarios: (a) two clients see a consumed potion's count tick down then disappear; (b) two clients see a torch placed at the player's location with `IsPlaced=true`; (c) two clients see a successful lockpick unlock a door given a stubbed `IRandomSource` that returns 0.0.
- New unit-level test `LockpickDeterminismTests` confirming `FixedRandomSource` produces predictable outcomes across repeated calls.

## Impact

- **Affected specs**: `client-server-communication` — MODIFIED `Grain Mutation Methods` (`UseAsync` no longer limited to key-on-door); ADDED requirements for the three new delta types and the `IRandomSource` injection contract.
- **Affected code**:
  - `Aetherium.Server/MultiWorld/Deltas.cs` — three new delta classes
  - `Aetherium.Server/InteractionSystem.cs` — `IRandomSource` field + ctor param; `ActionContext` overloads for 10 methods; `TryLockpick` uses injected random
  - `Aetherium.Server/IRandomSource.cs` (new) — interface + `DefaultRandomSource`
  - `Aetherium.Server/MultiWorld/GameMapGrain.cs` — `UseAsync` rewritten; emits diffed deltas
  - `Aetherium.Server/GameSession.cs` — three new `ApplyDelta` cases
  - `Aetherium.Server/Program.cs` — register `IRandomSource` → `DefaultRandomSource`
  - `Aetherium.Test/MultiWorld/DeltaApplicationTests.cs` — extended
  - `Aetherium.Test/MultiWorld/EndToEndSharedMutationTests.cs` — extended
  - `Aetherium.Test/MultiWorld/LockpickDeterminismTests.cs` (new)
- **Not breaking**: legacy session-bound callers (`LocalMutationGateway`) keep working because session overloads remain. Existing 740+ tests pass unchanged; new tests are additive.
