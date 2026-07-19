# client Specification

## Purpose
TBD - created by archiving change add-adaptive-depth-visualization. Update Purpose after archive.
## Requirements
### Requirement: Unity Multi-Band Rendering and Camera
The Unity client SHALL render nearby bands of the perception slab as stacked layers with opacity falloff, and SHALL provide an orthographic follow camera whose framing adapts to the local vertical extent.

#### Scenario: Deeper bands render more transparent
- **WHEN** the Unity client receives a multi-band perception slab
- **THEN** it SHALL render one `Tilemap` per band stacked by `sortingOrder`
- **AND** the focus band SHALL render opaque
- **AND** each off-focus band SHALL render with `TilemapRenderer` alpha reduced as `|dZ|` increases

#### Scenario: Camera reframes in a tall interchange
- **WHEN** the local column around the player has few occupied bands (a flat street)
- **THEN** the orthographic camera SHALL track the player at a close framing
- **WHEN** the local column has many occupied bands (a tall interchange)
- **THEN** the camera SHALL increase `orthographicSize` (pull back) or surface the cross-section overlay to frame the local vertical extent

### Requirement: Altitude Gauge
While the player is flying or piloting (or otherwise changing Z), the Unity client SHALL display a discrete N-step altitude gauge spanning the flyer's band envelope with the current band highlighted.

#### Scenario: Altitude gauge while flying
- **WHEN** the player is flying or piloting a vehicle with a band envelope `[MinBand, MaxBand]`
- **THEN** the client SHALL display a HUD altitude gauge of `N` discrete steps spanning that envelope
- **AND** the gauge SHALL highlight the step for the current band
- **WHEN** the player climbs or descends to another band
- **THEN** the highlighted step SHALL move to the step for the new band

