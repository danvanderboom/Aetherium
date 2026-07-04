## ADDED Requirements

### Requirement: Autonomous Agent Live-Map Play
An autonomous agent SHALL be able to join a live, grain-hosted map as a first-class participant and act on the shared world, without requiring a pre-existing human session. Its actions SHALL mutate canonical world state and be perceived by co-located human players.

#### Scenario: Agent attaches to a live map
- **WHEN** an agent runner is attached to a map via `AttachToWorldAsync(worldId, mapId, agentId)`
- **THEN** the agent SHALL join the map as a Character (entity id == agentId) via the same path a player uses
- **AND** the map's player set SHALL include the agent

#### Scenario: Agent actions fan out to players
- **WHEN** the agent takes an action (e.g. a move) during a step
- **THEN** the action SHALL be routed through the map grain (a grain-routed mutation gateway), mutating canonical state
- **AND** every human session bound to the map SHALL receive a fresh perception update — the agent is visible in the shared world

#### Scenario: Agent perceives the shared world
- **WHEN** the agent computes a step
- **THEN** it SHALL obtain perception from the canonical map world for its entity (no SignalR session required)
- **AND** that perception SHALL reflect the same world state players see

#### Scenario: Agent detaches cleanly
- **WHEN** the agent detaches
- **THEN** its Character SHALL be removed from the shared map (an entity-removed delta fans out) so it does not linger as a frozen entity

### Requirement: Grain-Timer Agent Run Loop
The agent run loop SHALL execute on the Orleans grain scheduler, not an off-scheduler background task, so agent state is mutated only within the grain's activation turn.

#### Scenario: Loop runs on a grain timer
- **WHEN** `RunAsync(maxSteps, stepDelayMs)` is called
- **THEN** steps SHALL be driven by a grain timer whose callback runs on the grain's activation turn (non-interleaving)
- **AND** no agent state SHALL be mutated from a thread-pool thread outside the scheduler

#### Scenario: Loop self-stops at the step budget
- **WHEN** the loop has taken `maxSteps` steps
- **THEN** it SHALL stop and dispose its timer
- **AND** `GetStatusAsync` SHALL report `IsRunning == false` with the advanced step count
