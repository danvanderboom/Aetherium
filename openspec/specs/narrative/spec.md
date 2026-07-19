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

### Requirement: Quest Activation and Progression
The narrative state SHALL support activating a quest so that its objectives are tracked and completed at runtime. Before this, a quest's objectives cannot progress because no quest is ever active.

#### Scenario: Starting a quest activates it
- **WHEN** `StartQuestAsync(questId)` is called for a quest whose prerequisites are met and which is neither active nor completed
- **THEN** the quest id is added to the active set and its objectives are registered for tracking
- **AND** the call reports success

#### Scenario: Activation respects prerequisites and state
- **WHEN** `StartQuestAsync(questId)` is called for an unknown quest, an already-active quest, an already-completed quest, or one whose prerequisite quests are not completed
- **THEN** the quest is not activated and the call reports failure

#### Scenario: A travel objective completes on arrival
- **WHEN** a quest with a `travel_to` objective is active and a `player_arrived` event arrives whose world/map matches the objective's target
- **THEN** the objective is marked complete
- **AND** when all of the quest's objectives are complete, the quest moves to the completed set and is removed from the active set

#### Scenario: Direct-target travel objective resolves without cluster metadata
- **WHEN** a `travel_to` objective names its destination directly (a world id and/or map id)
- **THEN** arrival is matched against that destination directly
- **AND** tag- or template-based selectors are still resolved through the cross-world constraint resolver

### Requirement: Broader Objective Completion
The narrative state SHALL advance and complete `collect`, `kill`, and `reach_location` objectives from world events, in addition to `travel_to`. Count-based objectives SHALL accumulate progress until they reach their required count.

#### Scenario: Collect objective accumulates toward its required count
- **WHEN** a quest with a `collect` objective (item type and required count) is active and `item_collected` events matching the item type arrive
- **THEN** each matching event increments the objective's progress
- **AND** the objective completes once progress reaches the required count (default 1), completing the quest when it is the last objective
- **AND** events whose item type does not match do not advance the objective

#### Scenario: Kill objective completes after the required number of defeats
- **WHEN** a quest with a `kill` objective (enemy type and required count) is active and `enemy_defeated` events matching the enemy type arrive
- **THEN** the objective completes once the required number of matching defeats is recorded

#### Scenario: Reach-location objective completes on a matching arrival
- **WHEN** a quest with a `reach_location` objective is active and a `player_arrived` (or `location_reached`) event arrives
- **THEN** the objective completes when the arrival matches an explicit world/map target, or fuzzy-matches the objective's location hint against the arrival's fields
- **AND** an unrelated arrival does not complete it

### Requirement: Player-Facing Quest Surface
Players SHALL be able to list startable quests, accept a quest, and view their quest log for the world they are in, and this surface SHALL be reachable both over the game hub and via an agent tool and the CLI.

#### Scenario: Accepting a quest over the game hub
- **WHEN** a joined player calls `GameHub.AcceptQuest(questId)` for their current world
- **THEN** the world's narrative-state grain is resolved from the session and the quest is started when startable
- **AND** `GameHub.ListAvailableQuests()` returns the currently-startable quests and `GameHub.GetQuestLog()` returns active quests with per-objective progress plus completed quest ids

#### Scenario: Arrival is emitted on joining a world
- **WHEN** a player joins a world via `GameHub.JoinWorld`
- **THEN** a `player_arrived` event is emitted for that world/map so travel_to and reach_location objectives targeting it can complete (previously only portal travel emitted arrival)

#### Scenario: Quest surface is reachable by agents and the CLI
- **WHEN** a player-profile agent uses the `list_quests` / `accept_quest` / `quest_log` tools, or an operator runs `aetherctl quest available|accept|log <worldId>`
- **THEN** the same narrative-state grain is resolved (worldId → narrativeId → scope) and the corresponding activation/inspection is performed

