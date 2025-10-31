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

