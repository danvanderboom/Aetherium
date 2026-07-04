## ADDED Requirements

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

