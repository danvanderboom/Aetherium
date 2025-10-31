## ADDED Requirements

### Requirement: Hybrid Layout System
The system SHALL support placing hand-crafted "anchors" that PCG respects during generation.

#### Scenario: Place anchor point
- **WHEN** an anchor point is specified with coordinates and constraints
- **THEN** the generator respects the anchor placement and does not place conflicting content

#### Scenario: Place anchor rectangle
- **WHEN** an anchor rectangle is specified with bounds and blocking flag
- **THEN** the generator avoids or uses the rectangle according to the blocking constraint

#### Scenario: Multiple anchors with priority
- **WHEN** multiple anchors are placed with different priorities
- **THEN** higher priority anchors take precedence over lower priority ones during conflict resolution

#### Scenario: Anchor constraints in request
- **WHEN** a `WorldGenerationRequest` includes `HybridAnchors`
- **THEN** the `HybridLayoutPass` processes anchors before layout generation

### Requirement: Real-time Preview
Designers SHALL be able to generate and visualize worlds without launching the full game.

#### Scenario: Generate preview
- **WHEN** constraints are edited in the PCG editor
- **THEN** a world preview is generated and displayed within 1-2 seconds for typical sizes (60×60)

#### Scenario: Live constraint editing
- **WHEN** a constraint value is changed
- **THEN** the preview updates automatically showing the new generation result

