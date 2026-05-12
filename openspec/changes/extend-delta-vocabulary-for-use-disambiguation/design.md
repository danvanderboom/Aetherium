# Design Notes

## Why a generic `ComponentFieldChangedDelta` and not per-component deltas?

The natural alternative is one delta class per mutable component field: `ConsumableUsesChangedDelta`, `HealthLevelChangedDelta`, `LockpickDurabilityChangedDelta`, etc. That keeps each delta strongly typed and trivially serialized. It also bloats the wire vocabulary by ~8 classes for this change alone, and every future component-field mutation adds another.

The generic carrier is a one-time cost: receivers learn one dispatcher (`switch on (ComponentType, FieldName)`) and adding a new mutable field is a single `case` line on both sides. The cost is loss of compile-time exhaustiveness checking on the receiver — if the server emits a `(ComponentType, FieldName)` the receiver doesn't recognize, it'll throw at runtime instead of failing to compile. We mitigate by:

- Throwing `NotImplementedException` with a descriptive message on unknown pairs so the test stand catches it immediately.
- Treating the `(ComponentType, FieldName)` enum-equivalent as a wire contract change (semver-relevant) just like adding a delta class would be.

The trade is worth it because the alternative scales linearly with the number of mutable fields and we're about to add a lot of them (durability counters across every tool-with-uses).

## Why three deltas instead of overloading existing ones?

`ItemTransferredDelta` already carries an "inventory direction" flag and could grow a `Destroyed` bool, an `EntityPlaced` mode, and so on. That's ergonomically worse than separate classes:

- `ItemDestroyedDelta` has no `OwnerEntityId` when destroying a world-side item (e.g., shattered crystal); `ItemTransferredDelta` requires it. Reusing forces awkward nullables and `if (Destroyed) ignore Owner` branches.
- `EntityPlacedDelta` carries an `EntityPlacement` for reconstruction; `ItemTransferredDelta` carries an optional one only for pickups. Overloading the field's meaning depending on a sibling flag is exactly the soup of conditional fields we'd be paying for.

Keeping each delta single-purpose makes the `ApplyDelta` switch flat and each handler short. The wire cost difference is one extra type-tag byte per delta — negligible.

## RNG: why `IRandomSource` and not just `System.Random.Shared` directly?

`Random.Shared` is fine for live gameplay (thread-safe, well-seeded, no per-call allocation), and it's what `DefaultRandomSource` wraps. The interface buys us two things:

1. **Test determinism.** `FixedRandomSource(new[] { 0.0, 0.9 })` lets `LockpickDeterminismTests` assert "the first attempt succeeds with chance 0.6, the second fails." Without the interface, the only way to test lockpick branches is to run thousands of trials and check the empirical rate, which is slow and flaky.
2. **Future per-map seeding.** When grain persistence lands and we want replay determinism, the grain can register a `SeededRandomSource(mapId.GetHashCode() ^ tickCounter)` against its scoped service container. The lockpick code path doesn't change.

The non-goal here is full replay determinism today. Multiple grain calls within a single tick still race against `Random.Shared`'s thread-local state and can produce different sequences run-over-run. That's acceptable for the current "live multiplayer" scope and the seam is in place for when we need more.

## `UseAsync` delta emission strategy: diff or instrument?

Two ways for the grain to know which deltas to emit after `TryUseWithMode` returns:

1. **Instrument** `InteractionSystem` to return a `(Result, IReadOnlyList<MapDelta>)` tuple. Each method explicitly builds the deltas describing its mutations.
2. **Diff** the affected components before and after the call.

(1) is cleaner long-term but requires changing every `InteractionSystem` return signature, which ripples through every existing caller including `LocalMutationGateway` and ~50 tests. (2) is a localized change in the grain: snapshot the player's inventory, the item's relevant components, and any door component before the call; compare after; emit deltas for what changed.

This change uses (2) for `UseAsync`. The diff is bounded (we know exactly which components each Use mode can touch — Consumable.Uses, Health.Level, *.Durability, Door.IsLocked/IsOpen, Placeable.IsPlaced, LightSource flags, Activatable.IsActivated, Inventory contents) so the diff helper stays under ~80 lines. A future change can reverse the choice if instrumentation becomes worth the cost.

## `EntityPlacedDelta` overlap with `EntityAddedDelta`

`EntityAddedDelta` already carries an `EntityPlacement` and adds to the world via `EntityFactory`. The new `EntityPlacedDelta` is structurally similar — same placement, same factory path. The reason for the separate type:

- `EntityAddedDelta` is emitted when the grain spawns something new (loot, monster respawn). The session has no prior representation of the entity.
- `EntityPlacedDelta` is emitted when an item the player owns transitions to the world. The session has the entity in its inventory mirror and needs to remove it from there before the placement happens; otherwise the inventory mirror and the world mirror briefly hold the same instance.

We could collapse to one delta with an optional `RemoveFromInventoryOf` field, but the asymmetry is real and the type tag clarifies intent at the call site. Keeping them separate.

## What's explicitly out of scope

- **Per-grain seeded RNG for replay.** The interface is in place; using it requires grain persistence work that's not happening here.
- **Inventory capacity persistence across reconnect.** `TryEquip`'s `CapacityBoost` permanently mutates `Inventory.Capacity`. The delta will replicate the live change; player reconnect/restore is the `add-player-persistence` change.
- **Concealment cloak perception effects.** `TryEquip` sets a `Hidden` component on the player. The visibility consequence is a perception-side concern (separate filter); the delta vocabulary here only needs to replicate the equip event itself.
- **Climbable Z-level changes.** Current `TryClimb` is validate-only and doesn't change Z. When climbing actually moves the player, that's an `EntityMovedDelta` (already exists). No new vocabulary needed.
