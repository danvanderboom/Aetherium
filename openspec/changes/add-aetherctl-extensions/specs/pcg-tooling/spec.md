## ADDED Requirements
### Requirement: Worldgen PNG Rendering
The system SHALL support `aetherctl worldgen render --png <path>` to export a PNG preview of the generated map.

#### Scenario: Render PNG to path
- **WHEN** the operator runs `aetherctl worldgen render --png out.png --generator maze --width 64 --height 64`
- **THEN** the command exits zero and `out.png` is created with a visual of the map

