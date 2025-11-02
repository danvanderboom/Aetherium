## Why
Unify world generation and runtime operations by enabling feature builders to execute tools, providing consistent validation, authorization, and side-effects. This allows world building services to use the same tool system that agents and players use, improving code reuse, testability, and extensibility.

## What Changes
- Add `WorldBuildingToolContext` extending `ToolExecutionContext` to provide `World` reference during world building
- Complete existing world building tools (`SpawnEntityTool`, `MoveEntityTool`, `ModifyEntityTool`, `DestroyEntityTool`) to work with `World` context
- Add `SetTerrainTool` for terrain placement operations
- Plumb `AgentToolRegistry` and `IServiceProvider` into `WorldFeatureBuilder` base class
- Enable feature builders to execute tools via helper methods (e.g., `ExecuteToolAsync`)
- Refactor `TorusFeatureBuilder` as proof-of-concept to use `SetTerrainTool`
- Update documentation with world building tool usage examples

## Impact
- Affected specs: `world-building` (ADDED tool execution capability, MODIFIED feature builder requirements), `agents` (NEW capability for tool system world building support)
- Affected code:
  - `Aetherium.Server/Agents/Tools/ToolExecutionContext.cs` - Add `WorldBuildingToolContext`
  - `Aetherium.Server/Agents/Tools/WorldBuilding/*.cs` - Complete tool implementations
  - `Aetherium.Server/WorldBuilders/Features/*.cs` - Add tool execution support
  - `Aetherium.Server/Core/World.cs` - May need minor updates for tool integration
- New files:
  - `Aetherium.Server/Agents/Tools/WorldBuilding/SetTerrainTool.cs`

