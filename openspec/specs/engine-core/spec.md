## Purpose
Defines the core entity-component model and world storage/movement rules for the console engine.

## Requirements

### Requirement: Entity-Component Structure
The engine SHALL represent game objects as `Entity` instances that own a tree of `Component`s.

#### Scenario: Entity has unique identity and default components
- WHEN a new `Entity` is constructed
- THEN it MUST have a unique `EntityId`
- AND it MUST contain `WorldLocation` and `Tile` components by default

#### Scenario: Component tree supports add/get/has semantics
- WHEN a component is added via `Set<T>`
- THEN `Get<T>` returns the instance
- AND `Has<T>` returns true

### Requirement: World Indexes and Storage
The engine SHALL index entities globally and by location.

#### Scenario: Add indexes entity and location
- WHEN `World.AddEntity` is called with an entity that has `WorldLocation`
- THEN the entity MUST be present in `World.Entities`
- AND the entity MUST be present in `World.EntitiesByLocation[location]`

#### Scenario: Remove clears indexes and characters
- WHEN `World.RemoveEntity` is called
- THEN the entity MUST be removed from `World.Entities`
- AND the entity MUST be removed from the location bucket
- AND if the bucket becomes empty, the location key MUST be removed
- AND if the entity is a `Character`, it MUST be removed from `World.Characters`

### Requirement: Movement Constraints
The engine SHALL prevent invalid moves and enforce vertical constraints.

#### Scenario: Prevent moving onto impassable terrain
- WHEN `World.TryMove` targets a location whose terrain is not passable
- THEN the method MUST return false and not change the entity location

#### Scenario: Prevent moving Up without CanAscend
- WHEN the destination is one level above and the entity lacks `CanAscend` on its current location
- THEN `World.TryMove` MUST return false

#### Scenario: Prevent moving Down without CanDescend
- WHEN the destination is one level below and the entity lacks `CanDescend` on its current location
- THEN `World.TryMove` MUST return false

### Requirement: Terrain Passability Rules
The engine SHALL treat specific terrain types as passable.

#### Scenario: Passable terrain names
- WHEN `World.PassableTerrain` is evaluated for terrain types
- THEN Indoors, Upstairs, Downstairs, Road, Plains, Forest, Cave MUST return true
- AND any other terrain MUST return false

### Requirement: Terrain Setting
The engine SHALL set terrain by location or chunk and create a `Terrain` entity if absent.

#### Scenario: Setting terrain creates or updates entity
- WHEN `World.SetTerrain(name, location)` is called
- THEN the corresponding `Terrain` entity MUST exist at `location`
- AND its `Tile` MUST reference the `TileType` of the target terrain


