# Client-Server Communication — Delta Vocabulary & Use Disambiguation

## MODIFIED Requirements

### Requirement: Grain Mutation Methods
`IGameMapGrain.UseAsync(sessionId, itemEntityId, onEntityId, usageId)` SHALL support every Use mode the session-bound `InteractionSystem.TryUseWithMode` supports: `unlock-door`, `consume`, `place`, `lockpick`, `force-open`, plus reactive disambiguation when `usageId` is null and multiple options are valid. The grain SHALL delegate to `InteractionSystem.TryUseWithMode(ActionContext, ...)` and SHALL emit deltas describing the resulting state changes by diffing the affected components before and after the call.

#### Scenario: UseAsync delegates to InteractionSystem.TryUseWithMode
- **WHEN** `IGameMapGrain.UseAsync` is invoked with a non-null `usageId`
- **THEN** the grain SHALL build an `ActionContext { World, Player, ViewLocation }` for the session
- **AND** SHALL call `InteractionSystem.TryUseWithMode(ctx, itemEntityId, onEntityId, usageId)`
- **AND** SHALL emit a `MapDelta` for every mutated field (component fields via `ComponentFieldChangedDelta`, door state via `DoorStateChangedDelta`, inventory destruction via `ItemDestroyedDelta`, item placement via `EntityPlacedDelta`)
- **AND** SHALL NOT reimplement any Use-mode logic natively

#### Scenario: UseAsync supports reactive disambiguation
- **WHEN** `IGameMapGrain.UseAsync` is invoked with a null `usageId` and multiple valid usage options exist
- **THEN** the grain SHALL call `InteractionSystem.GetUseOptions(ctx, ...)` and return the options via `InteractionResultDto.Options`
- **AND** SHALL NOT mutate `_world`
- **AND** the client SHALL retry with a selected `usageId`

#### Scenario: UseAsync auto-resolves single option
- **WHEN** `IGameMapGrain.UseAsync` is invoked with a null `usageId` and exactly one valid usage option exists
- **THEN** the grain SHALL invoke `TryUseWithMode` with the inferred `usageId` automatically
- **AND** SHALL emit deltas as for the explicit-usageId path

## ADDED Requirements

### Requirement: Generic Component Field Change Delta
The grain SHALL emit a `ComponentFieldChangedDelta` when a mutation changes a numeric, boolean, or string field on an entity's component, where the change cannot be expressed by a more specific delta type (door state, entity move, etc.).

The delta SHALL carry: the target `EntityId`, the `ComponentType` name, the `FieldName`, and exactly one of `NumericValue` (double), `BoolValue`, or `StringValue` populated with the new value.

#### Scenario: Consumable use decrement
- **WHEN** `TryConsume` decrements `Consumable.Uses` from 2 to 1
- **THEN** the grain SHALL emit `ComponentFieldChangedDelta { EntityId = item.Id, ComponentType = "Consumable", FieldName = "Uses", NumericValue = 1.0 }`
- **AND** receiving sessions SHALL find the item in their mirror and set `Consumable.Uses = 1`

#### Scenario: Durability decrement on force-open
- **WHEN** `TryForceOpen` decrements `ForcesDoor.Durability`
- **THEN** the grain SHALL emit `ComponentFieldChangedDelta { ComponentType = "ForcesDoor", FieldName = "Durability", NumericValue = <new value> }`

#### Scenario: Activatable toggle
- **WHEN** `TryActivate` flips `Activatable.IsActivated`
- **THEN** the grain SHALL emit `ComponentFieldChangedDelta { ComponentType = "Activatable", FieldName = "IsActivated", BoolValue = <new value> }`

#### Scenario: Unknown component/field pair fails loud
- **WHEN** a session applies a `ComponentFieldChangedDelta` for a `(ComponentType, FieldName)` pair it does not recognize
- **THEN** the session SHALL throw `NotImplementedException` with a message identifying the pair
- **AND** SHALL NOT silently ignore the delta

### Requirement: Item Destroyed Delta
The grain SHALL emit an `ItemDestroyedDelta` when an item is removed from the simulation entirely — typically because a `Consumable` reached zero uses or a `Lockpick` / `ForcesDoor` reached zero durability.

#### Scenario: Consumable destroyed on zero uses
- **WHEN** `TryConsume` decrements `Uses` to zero and removes the item from the player's inventory
- **THEN** the grain SHALL emit `ItemDestroyedDelta { EntityId = item.Id, OwnerEntityId = player.Id }`
- **AND** receiving sessions SHALL remove the item from the named owner's inventory mirror

#### Scenario: Lockpick broke
- **WHEN** `TryLockpick` decrements `Lockpick.Durability` to zero and removes the item
- **THEN** the grain SHALL emit `ItemDestroyedDelta { EntityId = lockpick.Id, OwnerEntityId = player.Id }`

#### Scenario: World-side item destroyed
- **WHEN** a future mutation destroys an entity that lives in the world (not in any inventory)
- **THEN** the grain SHALL emit `ItemDestroyedDelta { EntityId = entity.Id, OwnerEntityId = null }`
- **AND** receiving sessions SHALL remove the entity from `World.Entities`

### Requirement: Entity Placed Delta
The grain SHALL emit an `EntityPlacedDelta` when an item leaves a player's inventory and enters the world (e.g., `TryPlace` for a torch). The delta carries the full `EntityPlacement` so receivers can reconstruct the entity with its mutated component state.

#### Scenario: Place torch from inventory
- **WHEN** `TryPlace` removes a torch from the player's inventory and adds it to the world at the player's view location with `PlaceableLight.IsPlaced = true`
- **THEN** the grain SHALL emit `EntityPlacedDelta { Placement = <torch placement with IsPlaced=true>, SourceOwnerEntityId = player.Id }`
- **AND** receiving sessions SHALL remove the torch from the named owner's inventory mirror
- **AND** SHALL reconstruct the torch in the world via `EntityFactory.Create(Placement)`

### Requirement: Injectable Random Source for Probabilistic Interactions
`InteractionSystem` SHALL accept an `IRandomSource` via constructor injection and SHALL route all probabilistic decisions through that source. The default DI registration SHALL bind `IRandomSource` to `DefaultRandomSource`, which wraps `System.Random.Shared`.

#### Scenario: Lockpick uses injected RNG
- **WHEN** `TryLockpick` evaluates its success chance
- **THEN** it SHALL call `_random.NextDouble()` on the injected `IRandomSource`
- **AND** SHALL NOT instantiate `new System.Random()` per call

#### Scenario: Tests substitute deterministic source
- **WHEN** a test constructs `InteractionSystem` with a `FixedRandomSource(new[] { 0.0 })`
- **THEN** the next call to `TryLockpick` against a door with success chance ≥ 0.0 SHALL succeed
- **AND** subsequent calls SHALL consume the next value in the fixed sequence

#### Scenario: Default registration uses Random.Shared
- **WHEN** `InteractionSystem` is resolved from DI with no test override
- **THEN** the injected `IRandomSource` SHALL be `DefaultRandomSource`
- **AND** calls to `NextDouble` SHALL delegate to `System.Random.Shared.NextDouble()`

### Requirement: Use Disambiguation via ActionContext
`InteractionSystem` SHALL expose `ActionContext` overloads for `TryUse`, `GetUseOptions`, `TryUseWithMode`, `TryActivate`, `TryConsume`, `TryPlace`, `TryClimb`, `TryForceOpen`, `TryLockpick`, and `TryEquip`. Each session-taking overload SHALL remain as a thin forwarder. The `ActionContext` overload SHALL hold the canonical implementation so `GameMapGrain.UseAsync` can reuse it without depending on `GameSession`.

#### Scenario: Both overloads produce identical outcomes
- **WHEN** equivalent calls are made via `interactionSystem.TryConsume(session, itemId)` and `interactionSystem.TryConsume(new ActionContext(session.World, session.Player, session.ViewLocation), itemId)`
- **THEN** both calls SHALL produce equivalent `InteractionResult` values
- **AND** both calls SHALL mutate the underlying world identically (same component fields changed, same inventory removals)

#### Scenario: TryUseWithMode dispatches through ActionContext
- **WHEN** `TryUseWithMode(ActionContext, itemId, targetId, usageId)` is invoked
- **THEN** the dispatcher SHALL route to the corresponding `ActionContext` overload (`TryConsume`, `TryPlace`, `TryLockpick`, `TryForceOpen`, or the inline unlock-door logic) without constructing a `GameSession`
