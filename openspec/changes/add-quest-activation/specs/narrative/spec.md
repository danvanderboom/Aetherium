## ADDED Requirements

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
