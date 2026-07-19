## ADDED Requirements

### Requirement: Depth-Cued Rendering
The console map view SHALL composite the perception slab per screen cell, attenuating off-focus bands by `|dZ|` using the existing per-tile dimming ramp, so that the focus band renders at full detail and nearby bands are progressively dimmed.

#### Scenario: Off-focus bands dimmed by depth
- **WHEN** the slab contains cells at the focus band (`dZ = 0`) and at off-focus bands (`dZ != 0`)
- **THEN** the focus band cells SHALL render at full detail, FOV, and lighting
- **AND** each off-focus cell SHALL be dimmed by a factor keyed on `|dZ|` via the existing `DimColor`/`GetInfraredColor` ramp multiplied with its light level
- **AND** for each screen cell the topmost opaque glyph within the slab SHALL win, with up to `depthBelow` translucent glyphs blended beneath it

#### Scenario: Player stays centered during compositing
- **WHEN** depth compositing is active
- **THEN** the player SHALL remain centered using the existing viewport math with no change

### Requirement: Level Ribbon
The console HUD SHALL display a compact vertical level ribbon showing the band stack around the player and where the focus band sits within it.

#### Scenario: Ribbon shows band stack and focus marker
- **WHEN** the map is rendered with a multi-band slab
- **THEN** the HUD SHALL show a vertical gauge listing the occupied bands (for example `+3 viaduct / +0 street / -1 concourse / -2 platform`)
- **AND** the ribbon SHALL mark which band is the current focus band
- **WHEN** the focus band changes as the player takes stairs, a lift, or a vehicle
- **THEN** the ribbon's focus marker SHALL update to the new band

### Requirement: Cross-Section View
The console SHALL provide an optional side-on elevation (cross-section) view, toggled by a key, that renders the column of bands around the player as a stacked schematic.

#### Scenario: Toggle elevation view
- **WHEN** the player presses the cross-section toggle key
- **THEN** the console SHALL render a side-on schematic with one row per band around the player
- **AND** the schematic SHALL NOT require per-tile FOV
- **WHEN** the player presses the toggle key again
- **THEN** the console SHALL return to the plan (top-down) map view
