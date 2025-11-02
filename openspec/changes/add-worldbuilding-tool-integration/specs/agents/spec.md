## ADDED Requirements

### Requirement: Agent Tool System World Building Support
The agent tool system SHALL support world building operations through a specialized execution context that provides direct `World` access without requiring game sessions or Orleans grains.

#### Scenario: WorldBuildingToolContext creation
- **WHEN** a world building operation needs to execute a tool
- **THEN** a `WorldBuildingToolContext` is created with the current `World` reference
- **AND** the context grants `world_edit` capability
- **AND** the context includes optional `CurrentFeature` reference

#### Scenario: Tool execution with World context
- **WHEN** a tool is executed with `WorldBuildingToolContext`
- **THEN** the tool can access the `World` directly via `context.World`
- **AND** the tool validates that `World` is available before executing
- **AND** the tool returns an error if `World` is missing

#### Scenario: World building tool capability
- **WHEN** a tool requires `world_edit` capability during world building
- **THEN** `WorldBuildingToolContext` automatically grants `world_edit` capability
- **AND** tools validate capability requirements before execution

### Requirement: World Building Tools
The system SHALL provide tools for terrain and entity operations during world building: `SetTerrainTool`, `SpawnEntityTool`, `MoveEntityTool`, `ModifyEntityTool`, and `DestroyEntityTool`.

#### Scenario: SetTerrainTool execution
- **WHEN** `SetTerrainTool` is executed with valid coordinates and terrain type
- **THEN** the terrain is set at the specified location in the world
- **AND** existing terrain at that location is replaced

#### Scenario: World building tools require World context
- **WHEN** a world building tool is executed without a `World` reference
- **THEN** the tool returns an error indicating missing World context
- **AND** no world modifications are made

