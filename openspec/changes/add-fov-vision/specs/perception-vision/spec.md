## ADDED Requirements

### Requirement: Field-of-View Computation
The engine SHALL compute visible cells on the character's current Z using cumulative opacity along rays.

#### Scenario: Origin is visible
- **WHEN** FOV is computed
- **THEN** the origin location MUST be visible

#### Scenario: Fully opaque cell blocks beyond
- **WHEN** a ray reaches a cell whose cumulative opacity ≥ 1
- **THEN** that cell MAY be visible
- **AND** further cells along the ray MUST NOT be visible

#### Scenario: Partial opacity accumulates
- **WHEN** a ray passes through multiple partially opaque cells (e.g., Forest with 0.5 opacity)
- **THEN** visibility MUST continue until cumulative opacity reaches ≥ 1
- **AND** the cell where it reaches ≥ 1 MAY be visible but cells beyond MUST NOT be

#### Scenario: Doors open are transparent
- **WHEN** a cell contains an entity with `OpensAndCloses.IsOpen = true`
- **THEN** its view obstruction MUST be treated as 0 for vision

#### Scenario: Water is transparent
- **WHEN** a cell contains Water terrain
- **THEN** it MUST NOT block line of sight

### Requirement: Corner Occlusion
The engine SHALL prevent looking around corners; line-of-sight MUST respect blocking geometry.

#### Scenario: Corridor corner blocks line of sight
- **WHEN** a wall adjacent to the viewing path forms a corner between origin and target
- **THEN** the target around the corner MUST NOT be visible

### Requirement: Vision Frame Composition
The engine SHALL produce a `VisionFrame` with a per-location summary of terrain and entities for visible cells.

#### Scenario: Terrain included in visuals
- **WHEN** a cell is visible
- **THEN** its terrain `TileType` MUST be present in the `Visual` for that location

#### Scenario: Entities summarized in visuals
- **WHEN** entities are present at a visible location
- **THEN** the `Visual` MUST record counts by broad type (Character/Object)

### Requirement: Vision-Based Console Rendering
The console map view SHALL render only cells included in the current `VisionFrame`.

#### Scenario: Hidden cells are not drawn
- **WHEN** `ConsoleMapView.DrawContents()` executes
- **THEN** cells not present in the `VisionFrame` MUST render as background/empty

#### Scenario: Draw priority preserved within visible cells
- **WHEN** multiple entities occupy a visible cell
- **THEN** characters MUST render above objects, which render above terrain

#### Scenario: Vision refresh on movement
- **WHEN** the controlled character's location or move timestamp changes
- **THEN** the map view MUST recompute vision for the visible rectangle before rendering


