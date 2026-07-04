# narrative Specification

## Purpose
TBD - created by archiving change add-emergent-narrative-systems. Update Purpose after archive.
## Requirements
### Requirement: Procedural Quest Generation
The system SHALL generate multi-stage quests from NPC needs and world state with dependency chains.

#### Scenario: Quest chain generation
- **WHEN** NPC goals are provided to NarrativeGraphGenerator
- **THEN** it SHALL create fetch/rescue/defend quests based on goal types
- **AND** it SHALL build prerequisite chains linking simpler quests to more complex ones

#### Scenario: Quest prerequisites
- **WHEN** a quest has PrerequisiteQuestIds
- **THEN** those quests MUST be completed before the quest can be started
- **AND** the NarrativeStateGrain SHALL track prerequisite completion

### Requirement: Environmental Storytelling
The system SHALL place environmental storytelling elements (ruins, camps, lore fragments) with coherent history.

#### Scenario: Ruins placement
- **WHEN** EnvironmentalStoryPass executes during world generation
- **THEN** it SHALL place ancient ruins with historical inscriptions
- **AND** each ruin SHALL have coherent historical text based on region and era

#### Scenario: Abandoned camps
- **WHEN** EnvironmentalStoryPass executes
- **THEN** it SHALL place abandoned camps with clues about what happened
- **AND** camps SHALL contain inscription components with narrative text

#### Scenario: Lore fragments
- **WHEN** lore topics are specified in NarrativeGenerationConstraints
- **THEN** the system SHALL scatter lore fragments throughout the world
- **AND** fragments SHALL cross-reference each other for narrative continuity

### Requirement: NPC Relationship Networks
The system SHALL procedurally create social relationship graphs between NPCs that influence dialogue and quests.

#### Scenario: Relationship generation
- **WHEN** RelationshipMatrix.GenerateFromNPCGoals is called
- **THEN** it SHALL create relationships between NPCs based on types
- **AND** relationships SHALL be symmetric and range from -1.0 (enemy) to +1.0 (ally)

#### Scenario: Relationship influence
- **WHEN** quests are generated
- **THEN** NPC relationships SHALL influence quest targets and dialogue options
- **AND** the RelationshipMatrix SHALL be queryable to find allies/enemies of an NPC

### Requirement: Consequence Propagation
Player actions SHALL generate new story branches through consequence propagation.

#### Scenario: Event processing
- **WHEN** a player action succeeds (item collected, enemy defeated, etc.)
- **THEN** NarrativeConsequenceEngine SHALL process the event
- **AND** it SHALL generate follow-up quests based on event type

#### Scenario: Quest completion consequences
- **WHEN** a quest is completed
- **THEN** the system SHALL check for follow-up quests
- **AND** it SHALL generate new quests from grateful NPCs or discovered information

### Requirement: Hybrid Narrative State Persistence
Narrative state SHALL support both shared (per-narrative) and per-world persistence.

#### Scenario: Shared state
- **WHEN** NarrativeStateScope is "shared"
- **THEN** NarrativeStateGrain SHALL use grain key format "narrativeId"
- **AND** state SHALL be shared across all worlds using the same narrative

#### Scenario: Per-world state
- **WHEN** NarrativeStateScope is "per-world"
- **THEN** NarrativeStateGrain SHALL use grain key format "{worldId}:{narrativeId}"
- **AND** each world SHALL have isolated narrative state

### Requirement: Deterministic Narrative Generation
All narrative generation SHALL be deterministic when seeded for reproducible output.

#### Scenario: Deterministic quests
- **WHEN** NarrativeGraphGenerator is called with the same seed and inputs
- **THEN** it SHALL produce identical quest chains
- **AND** quest IDs, titles, and prerequisite chains SHALL be consistent

#### Scenario: Deterministic lore
- **WHEN** LoreGenerator is called with the same seed and topics
- **THEN** it SHALL produce identical lore fragments
- **AND** cross-references between fragments SHALL be consistent

### Requirement: Lore Fragment Components
Entities SHALL support inscription components that mark them as having narrative text.

#### Scenario: Inscription component
- **WHEN** an entity has an Inscription component
- **THEN** it SHALL contain text, topic, title, author, and era
- **AND** it SHALL track whether the inscription has been read

#### Scenario: Lore fragment entities
- **WHEN** a LoreFragment entity is created
- **THEN** it SHALL automatically have Inscription and Carriable components
- **AND** it SHALL have a visual symbol based on topic type (history, legend, journal, prophecy)

### Requirement: Cross-World Quest Constraints
The quest system SHALL support objectives that span multiple worlds within a cluster, requiring players to travel between worlds to complete quests.

#### Scenario: Cross-world travel objective
- **WHEN** a quest definition includes CrossWorldConstraint with a WorldSelector
- **THEN** NarrativeGraphGenerator SHALL emit a QuestObjective with Type="travel_to" and Parameters containing worldSelector/mapTag
- **AND** the objective SHALL specify target world/map by id, tag, or template

#### Scenario: Cross-world objective completion
- **WHEN** a player arrives at the target world/map specified by a travel_to objective
- **THEN** CrossWorldConstraintResolver SHALL verify the destination matches the objective
- **AND** NarrativeStateGrain SHALL mark the objective as complete via arrival event

#### Scenario: World selector resolution
- **WHEN** CrossWorldConstraintResolver evaluates a WorldSelector
- **THEN** it SHALL consult IClusterGrain to find reachable targets within the same cluster
- **AND** if multiple targets match, it SHALL select the nearest or most appropriate destination

