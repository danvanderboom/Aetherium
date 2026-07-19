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
