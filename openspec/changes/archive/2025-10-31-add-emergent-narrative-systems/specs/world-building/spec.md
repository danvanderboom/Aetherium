## ADDED Requirements

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

