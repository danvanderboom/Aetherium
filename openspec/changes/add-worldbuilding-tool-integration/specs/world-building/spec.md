## ADDED Requirements

### Requirement: World Building Tool Execution
World building SHALL support executing agent tools during feature building. Feature builders MAY execute tools via the tool system when `AgentToolRegistry` and `IServiceProvider` are available. Tools executed during world building SHALL operate with `world_edit` capability.

#### Scenario: Tool execution during feature build
- **WHEN** a `WorldFeatureBuilder` executes a tool during `Build()`
- **THEN** the tool executes with `WorldBuildingToolContext` containing the current `World` reference
- **AND** the tool has `world_edit` capability granted
- **AND** tool execution results are handled appropriately (success or error)

#### Scenario: Tool validation in world building
- **WHEN** a world building tool is executed with invalid parameters
- **THEN** the tool returns an error result
- **AND** feature building continues without crashing

#### Scenario: Tool execution performance
- **WHEN** a feature builder executes tools in a tight loop during world generation
- **THEN** tool execution overhead is acceptable (<10ms per tool call)
- **AND** world generation completes within reasonable time bounds

## MODIFIED Requirements

### Requirement: World Features Composition
The world SHALL be composed by a list of `WorldFeature`s each with a chunk and builder. Feature builders MAY execute agent tools during the build process when tool infrastructure is available.

#### Scenario: Features applied during build
- **WHEN** `World.Build()` is called
- **THEN** each feature's `FeatureBuilder(world, feature).Build()` MUST be invoked exactly once
- **AND** feature builders MAY execute tools via the tool system if `AgentToolRegistry` and `IServiceProvider` are provided

