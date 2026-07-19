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
- **WHEN** `World.TryMove` targets a location whose terrain is not passable
- **THEN** the method MUST return false and not change the entity location

#### Scenario: Prevent moving Up without CanAscend
- **WHEN** the destination is one level above and the entity lacks `CanAscend` on its current location
- **THEN** `World.TryMove` MUST return false

#### Scenario: Prevent moving Down without CanDescend
- **WHEN** the destination is one level below and the entity lacks `CanDescend` on its current location
- **THEN** `World.TryMove` MUST return false

#### Scenario: Airborne flyer enters a ground-impassable tile
- **WHEN** an entity with a `Flight` component in the `Airborne` state calls `World.TryMove` toward a column whose ground-band terrain is not passable
- **AND** no obstruction reaches the flyer's current altitude band at that column
- **THEN** `World.TryMove` MUST allow the move and update the entity location

#### Scenario: Flyer changes altitude within its band range without ascent markers
- **WHEN** an airborne entity with a `Flight` component moves Up or Down to a destination band within `[MinBand, MaxBand]`
- **THEN** `World.TryMove` MUST allow the vertical move without requiring `CanAscend` or `CanDescend` markers on its current location

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

### Requirement: Altitude Bands and Layered Obstruction
The engine SHALL resolve obstruction per altitude band (Z), where each obstruction declares a height or band-extent and exposes three independent facets — movement via `ObstructsMovement`, sight via `ObstructsView.Opacity`, and light via `BlocksLight`. Grounded single-tile entities at band 0 MUST behave exactly as before.

#### Scenario: Ground obstacle does not obstruct an entity in a higher band
- **WHEN** a ground obstacle at column `(x,y)` declares an obstruction height `h` that blocks only bands `[0, h)`
- **AND** an entity occupies band `z` where `z >= h` at the same column
- **THEN** the obstacle MUST NOT obstruct that entity's movement, sight, or light in band `z`

#### Scenario: Glass skylight blocks movement but not sight
- **WHEN** a glass skylight declares `ObstructsMovement` at its band with `ObstructsView.Opacity = 0`
- **THEN** an entity MUST be prevented from moving through the skylight's band
- **AND** an observer MUST be able to see through the skylight to entities in bands above it

#### Scenario: Bridge blocks both movement and sight
- **WHEN** a bridge at band +1 declares both `ObstructsMovement` and an opaque `ObstructsView`
- **THEN** an entity MUST be prevented from moving through the bridge's band
- **AND** an observer beneath the bridge MUST NOT see entities in the bands above the bridge at that column

#### Scenario: Grounded single-tile behavior is unchanged
- **WHEN** an entity without a `Flight` component moves at band 0
- **THEN** passability MUST be evaluated exactly as the existing ground-band `PassableTerrain` rule

### Requirement: Flight Capability
The engine SHALL provide a `Flight` component carrying `MinBand`, `MaxBand`, `CruiseBand`, `CanLand`, and `State` (Airborne | Landed | TakingOff | Landing) that grants an entity band freedom while airborne.

#### Scenario: Airborne horizontal traversal over impassable ground
- **WHEN** an entity with a `Flight` component in the `Airborne` state makes a horizontal move over a column whose ground-band terrain is impassable
- **AND** no obstruction reaches the flyer's band at the destination column
- **THEN** the move MUST succeed, ignoring ground-band obstruction

#### Scenario: Free vertical movement within band range
- **WHEN** an airborne flyer moves vertically to a band within `[MinBand, MaxBand]`
- **THEN** the move MUST succeed without `CanAscend`/`CanDescend` markers

#### Scenario: Vertical movement blocked outside band range
- **WHEN** an airborne flyer attempts to move to a band outside `[MinBand, MaxBand]`
- **THEN** `World.TryMove` MUST return false and not change the entity's band

#### Scenario: Grounded fast path preserved
- **WHEN** an entity has no `Flight` component
- **THEN** movement MUST use the existing grounded passability and `CanAscend`/`CanDescend` rules unchanged

