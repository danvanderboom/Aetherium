## Purpose
Defines how a `WorldBuilder` composes a `World` via features and includes torus and river generators.
## Requirements
### Requirement: WorldBuilder Registers Tile and Terrain Types
The builder SHALL add tile and terrain types to a fresh `World`.

#### Scenario: Tile and terrain registration
- WHEN a `WorldBuilder.Build()` implementation runs
- THEN it MUST add all `TileType`s and corresponding `TerrainType`s to the `World`

### Requirement: World Features Composition
The world SHALL be composed by a list of `WorldFeature`s each with a chunk and builder.

#### Scenario: Features applied during build
- WHEN `World.Build()` is called
- THEN each feature's `FeatureBuilder(world, feature).Build()` MUST be invoked exactly once

### Requirement: Torus Feature Generation
The torus feature SHALL populate a toroidal region based on major/minor radii and axis.

#### Scenario: Underground interior and border
- WHEN `TorusFeatureBuilder.Build()` runs for `z < 0`
- THEN locations inside the torus MUST be `Indoors`
- AND locations within border width outside the interior MUST be `Mountain`

#### Scenario: Ground level distribution
- WHEN `TorusFeatureBuilder.Build()` runs for `z == 0`
- THEN interior cells MUST be assigned Plains/Forest/Water by weighted random
- AND border cells MUST be `Mountain`

### Requirement: River Feature Generation
The river feature SHALL draw a meandering river band with forested borders.

#### Scenario: Meandering river with borders
- WHEN `RiverFeatureBuilder.Build()` runs
- THEN for each step along length, it MUST set a contiguous `Water` band centered at `riverCenter`
- AND set `Forest` borders on both sides with random widths within configured bounds

### Requirement: Environmental Story Generation Pass
World generation SHALL include an EnvironmentalStoryPass that places storytelling elements (ruins, camps, lore fragments) during the population phase.

#### Scenario: Environmental story pass execution
- **WHEN** EnvironmentalStoryPass executes during world generation
- **THEN** it SHALL place ruins if StoryPOIs contain "ruin" references
- **AND** it SHALL place abandoned camps if StoryPOIs contain "camp" references
- **AND** it SHALL place lore fragments if LoreTopics are specified in constraints

#### Scenario: Story features placement
- **WHEN** EnvironmentalStoryPass executes
- **THEN** RuinsFeature SHALL place ancient ruins with historical inscriptions
- **AND** AbandonedCampFeature SHALL place camps with clue inscriptions
- **AND** PlaceLoreFragmentsFeature SHALL scatter lore fragments based on topics

#### Scenario: Narrative constraints integration
- **WHEN** NarrativeGenerationConstraints includes LoreTopics or StoryPOIs
- **THEN** EnvironmentalStoryPass SHALL respect these constraints
- **AND** story elements SHALL be placed using deterministic RNG from GeneratorContext

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

### Requirement: Portal Network Generation Pass
World generation SHALL include a PortalNetworkPass that places portal entities during the Interactions phase, connecting worlds within clusters via link metadata.

#### Scenario: Portal placement during generation
- **WHEN** PortalNetworkPass executes during world generation
- **THEN** it SHALL place PortalComponent entities at strategic locations (major landmarks, zone boundaries, or narrative points)
- **AND** each portal SHALL contain link hints (TargetWorldId, TargetMapId, TargetTag, or hub references)
- **AND** portal placement SHALL use deterministic RNG from GeneratorContext

#### Scenario: Portal metadata structure
- **WHEN** PortalNetworkPass places a portal
- **THEN** the PortalComponent SHALL include PortalId, optional target identifiers, and Activation requirements
- **AND** portals SHALL be registered with the cluster grain for runtime link resolution

