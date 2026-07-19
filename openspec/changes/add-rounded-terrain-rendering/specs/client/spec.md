## ADDED Requirements

### Requirement: Rounded Terrain Region Rendering
The Unity client SHALL render designated region terrains (e.g. water) as smooth, curved filled shapes derived from the per-cell terrain mask — faithful to each cell while curving around corners — rendered per band and coexisting with the Tilemap that draws all other terrain.

#### Scenario: Lake renders as a smooth curved shape
- **WHEN** the client receives a frame containing a contiguous group of water cells
- **THEN** it SHALL render the water as a filled mesh whose outline follows the cell-region boundary
- **AND** the outline SHALL be smoothed (curved around corners) rather than axis-aligned squares
- **AND** every water cell centre SHALL lie within the rendered shape

#### Scenario: Land and water never double-draw
- **WHEN** a frame mixes water and non-water terrain
- **THEN** water cells SHALL be drawn only by the region mesh
- **AND** non-water cells SHALL remain drawn only by the Tilemap

#### Scenario: Region mesh respects band depth
- **WHEN** water appears on an off-focus band of the perception slab
- **THEN** its mesh SHALL use the same depth alpha falloff and `sortingOrder` as that band's Tilemap

### Requirement: Animated Coastline Shading
The Unity client SHALL shade region terrain with a coastline treatment whose foam and shallow-water gradient are a function of distance to the region boundary and animate over time.

#### Scenario: Foam hugs the shore
- **WHEN** the water mesh is rendered
- **THEN** vertices near the region boundary SHALL show a foam/shallows band
- **AND** the interior SHALL show open-water color
- **AND** the foam SHALL animate over time

#### Scenario: Shading preserves depth falloff
- **WHEN** water is on an off-focus band
- **THEN** the coastline material SHALL scale its output alpha by that band's depth alpha so deeper water renders more transparent

### Requirement: Terrain Lighting and Atmosphere
The Unity client SHALL modulate rendered terrain color by per-cell light level and a scene ambient tint, without changing the render pipeline.

#### Scenario: Dim cells render darker
- **WHEN** a cell's `LightLevel` is below 1.0
- **THEN** its rendered color SHALL be darkened proportionally to that light level

#### Scenario: Ambient tint colors the scene
- **WHEN** the frame carries a non-neutral ambient tint (e.g. a sunset hue)
- **THEN** rendered terrain SHALL be tinted toward that color
- **AND** a neutral (white) ambient tint SHALL leave terrain colors unchanged

### Requirement: Runnable Rounded-Terrain Demo
The Unity client SHALL provide a single-component bootstrap that composes a playable rounded-terrain scene from a mock lake frame, so the effect can be seen without hand-wiring a scene.

#### Scenario: Play from an empty scene
- **WHEN** a developer adds the demo bootstrap component to an empty scene and enters Play mode
- **THEN** the client SHALL load the lake mock frame
- **AND** render the rounded, lit water with the follow camera without further manual wiring
