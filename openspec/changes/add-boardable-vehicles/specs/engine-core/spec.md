## ADDED Requirements

### Requirement: Multi-Tile Footprint Occupancy
The engine SHALL allow an entity to declare a `Footprint` describing the tiles it occupies relative to its anchor `WorldLocation`, and SHALL make world indexing, movement, placement, and collision footprint-aware. A footprint MAY be expressed as a box `Size3d` or as an explicit list of relative cells (explicit cells override the box size when non-empty). The world MUST index every occupied tile, MUST validate every destination tile on movement and placement, and MUST prevent two footprints from overlapping. Entities without a `Footprint` MUST retain their single-tile indexing and movement fast path, guarded by `Has<Footprint>()`.

#### Scenario: Placement validates all footprint tiles
- **WHEN** a footprint entity is placed and every tile under its footprint is in-bounds, passable, and unoccupied
- **THEN** placement MUST succeed
- **AND** the world MUST index the entity at the anchor tile and at every relative cell of its footprint

#### Scenario: Placement blocked when any tile is invalid
- **WHEN** a footprint entity is placed and at least one tile under its footprint is impassable, out of bounds, or already occupied by another footprint
- **THEN** placement MUST fail
- **AND** the world MUST NOT index the entity at any tile

#### Scenario: Move blocked if any destination tile is impassable or occupied
- **WHEN** `World.TryMove` targets a destination where any tile under the moving entity's footprint would be impassable or occupied by another entity's footprint
- **THEN** `TryMove` MUST return false
- **AND** the entity location MUST NOT change

#### Scenario: Move succeeds when every destination tile is valid
- **WHEN** `World.TryMove` targets a destination where every tile under the footprint is passable and unoccupied
- **THEN** `TryMove` MUST return true
- **AND** the world MUST update the anchor and re-index every newly occupied tile, releasing the previously occupied tiles

#### Scenario: Single-tile entities keep the fast path
- **WHEN** an entity without a `Footprint` component is added, moved, or removed
- **THEN** the engine MUST index and move it by its single anchor tile exactly as before
- **AND** footprint validation MUST be skipped because `Has<Footprint>()` is false
