## ADDED Requirements

### Requirement: Runtime World Tool Execution
The grain SHALL execute world-building tools against a live, running world (resolved from the in-process world registry), so that operators can modify worlds at runtime without regenerating them.

#### Scenario: Execute a world-building tool at runtime
- **WHEN** `ExecuteWorldToolAsync` is called with a valid `worldId` and a tool that requires the `world_edit` capability
- **THEN** the grain SHALL execute the tool against that world via a world-building context
- **AND** SHALL return the tool's result, including any structured data (e.g. a spawned entity id)

#### Scenario: Spawn a creature into a running world
- **WHEN** `ExecuteWorldToolAsync` runs the `spawnentity` tool with a supported creature type at a passable, unoccupied location
- **THEN** a new entity SHALL be created and added to the world at that location
- **AND** the entity SHALL appear in subsequent world snapshots and, when visible, in character perception

#### Scenario: Reject non-world-building tools
- **WHEN** `ExecuteWorldToolAsync` is called with a tool that does not require the `world_edit` capability
- **THEN** the grain SHALL refuse to execute it and return a failure result

#### Scenario: Unknown world or tool
- **WHEN** `ExecuteWorldToolAsync` is called with an unknown `worldId` or an unregistered tool id
- **THEN** the grain SHALL return a failure result identifying the problem
- **AND** SHALL NOT throw

#### Scenario: Operator gating
- **WHEN** operator access is disabled
- **THEN** `ExecuteWorldToolAsync` SHALL be denied with a failure result
