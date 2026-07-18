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

### Requirement: Flight Envelope in Perception
When the perceiving entity has a Flight component, the server SHALL surface its altitude envelope on the perception — the min/max bands, the current band (as an absolute Z, since relative perception reports the player at Z 0), and the flight state — as an additive, non-breaking field that is absent for non-flyers.

#### Scenario: Envelope present for a flyer
- **WHEN** perception is computed for a perceiver that has a Flight component with bands `[MinBand, MaxBand]` and is at band `z`
- **THEN** the perception SHALL include a flight envelope reporting `MinBand`, `MaxBand`, current band `z`, and the flight state

#### Scenario: Envelope absent for a non-flyer
- **WHEN** the perceiver has no Flight component (or no perceiving entity is supplied)
- **THEN** the perception SHALL omit the flight envelope (null), leaving the DTO otherwise unchanged

### Requirement: Context Tint by Band
When context tint is enabled for a world, the server SHALL derive the default lighting mode from the viewer's band — underground bands enclosed/torch-lit, skyway bands sunlit, surface bands ambient — reusing the existing lighting modes with no new machinery. Opt-in; when disabled the caller's requested lighting mode is used unchanged.

#### Scenario: Underground reads as enclosed
- **WHEN** context tint is enabled and the viewer is on a band below ground (Z < 0)
- **THEN** perception SHALL report the torch (enclosed) lighting mode regardless of the requested mode

#### Scenario: Skyway reads as sunlit
- **WHEN** context tint is enabled and the viewer is on a band at/above the sky threshold
- **THEN** perception SHALL report the sunlight lighting mode

#### Scenario: Disabled leaves the requested mode
- **WHEN** context tint is disabled
- **THEN** perception SHALL report exactly the caller's requested lighting mode
