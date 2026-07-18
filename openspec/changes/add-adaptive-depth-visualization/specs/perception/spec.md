## ADDED Requirements

### Requirement: 3D Occluded Perception Slab
The server SHALL compute vision over a configurable range of altitude bands (not only the player's band), using per-band sight opacity (`ObstructsView.Opacity`) for a vertical line-of-sight test, and SHALL include only cells that pass the 3D FOV test, each tagged with its relative Z. The `PerceptionDto` schema SHALL remain unchanged.

#### Scenario: Flyer overhead visible through a clear column
- **WHEN** a flyer occupies a band above the viewer and every intervening band at that column has `ObstructsView.Opacity = 0` (open air)
- **THEN** the flyer's cell SHALL pass the 3D FOV test
- **AND** the cell SHALL be included in perception tagged with its positive `relativeZ`

#### Scenario: Flyer hidden by an opaque band between
- **WHEN** a flyer occupies a band above the viewer and an intervening band at that column has an opaque `ObstructsView` (for example a stone bridge)
- **THEN** the flyer's cell SHALL fail the vertical line-of-sight test
- **AND** the flyer's cell SHALL NOT be included in perception
- **AND** the opaque bridge underside SHALL be the visible cell for that column

#### Scenario: Visible through a transparent skylight
- **WHEN** the intervening band has `ObstructsMovement` but `ObstructsView.Opacity = 0` (a glass skylight)
- **THEN** the vertical ray SHALL be treated as clear
- **AND** the cell beyond the skylight SHALL be included in perception tagged with its `relativeZ`

#### Scenario: Level below visible through an open grate
- **WHEN** the viewer looks down a column whose intervening band is open (an open stairwell or grate with no opaque `ObstructsView`)
- **THEN** the cell on the lower band SHALL be included in perception tagged with a negative `relativeZ`
- **WHEN** the same column is solid pavement (opaque `ObstructsView`)
- **THEN** the lower cell SHALL NOT be included in perception

#### Scenario: Configurable band range with unchanged DTO schema
- **WHEN** the per-world slab range is configured as `[focusZ - depthBelow, focusZ + depthAbove]`
- **THEN** the server SHALL evaluate the 3D FOV test across exactly those bands and no others
- **AND** the server SHALL emit visible cells using the existing `PerceptionDto`, `VisualDto`, and `WorldLocationDto` fields (`"x,y,z"` visual keys and `relativeZ`) with no schema change

### Requirement: Adaptive Slab Depth
When adaptive slab depth is enabled for a world, the server SHALL bound the emitted band range each frame to the local vertical extent around the viewer — expanding toward the configured budget to cover occupied bands and collapsing toward single-Z over flat terrain — without ever exceeding the configured `depthBelow`/`depthAbove` budget or the depth cap, and without changing which visible cells are emitted relative to a fixed budget of the same size.

#### Scenario: Collapses over flat terrain
- **WHEN** adaptive slab depth is enabled and no band within the configured budget around the viewer's column is occupied
- **THEN** the server SHALL evaluate only the focus band (effective depth 0 in both directions)

#### Scenario: Expands to cover an interchange
- **WHEN** adaptive slab depth is enabled and the furthest occupied band within the budget is `k` bands away in a direction
- **THEN** the server SHALL evaluate that direction to exactly `k` bands
- **AND** SHALL emit the same visible cells it would emit with a fixed budget large enough to include band `k`

#### Scenario: Never exceeds the configured budget
- **WHEN** adaptive slab depth is enabled and content exists beyond the configured `depthBelow`/`depthAbove` budget (or the depth cap)
- **THEN** the server SHALL NOT expand the evaluated range past that budget (or cap)
