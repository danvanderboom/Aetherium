> Status (2026-07-03): substantially implemented despite most boxes being unchecked — context, builder plumbing, SetTerrain/MoveEntity/DestroyEntity tools, Torus proof-of-concept, tests, and docs all exist in code. SpawnEntityTool and ModifyEntityTool remain honest error stubs (return ToolExecutionResult.Error with a "requires entity factory / component modification" message).

## 1. OpenSpec Change
- [x] 1.1 Scaffold OpenSpec change add-worldbuilding-tool-integration with deltas

## 2. Context & Wiring
- [x] 2.1 Add WorldBuildingToolContext extending ToolExecutionContext (checked 2026-07-03: Aetherium.Server/Agents/Tools/WorldBuildingToolContext.cs)
- [x] 2.2 Verify AgentToolRegistry registration in Program.cs (should already exist) (checked 2026-07-03: registered as singleton in Program.cs)

## 3. WorldBuilder Plumbing
- [x] 3.1 Update WorldFeatureBuilder base class with optional registry/provider support (checked 2026-07-03: optional AgentToolRegistry/IServiceProvider ctor params)
- [x] 3.2 Add ExecuteToolAsync helper method to WorldFeatureBuilder (checked 2026-07-03: exists; sync ExecuteTool wrapper uses GetAwaiter().GetResult() — audit-flagged sync-over-async)
- [x] 3.3 Update feature builder constructors to accept registry/provider when available (checked 2026-07-03: TorusFeatureBuilder has the registry/provider ctor overload)

## 4. Tool Implementation
- [x] 4.1 Implement SetTerrainTool with schema and execution (checked 2026-07-03: fully implemented)
- [ ] 4.2 Complete SpawnEntityTool for World context (still open 2026-07-03: honest error stub — validates args then returns "requires entity factory/prefab system")
- [x] 4.3 Complete MoveEntityTool for World context (checked 2026-07-03: implemented; spatial-index update fixed in Phase 2)
- [ ] 4.4 Complete ModifyEntityTool for World context (still open 2026-07-03: honest error stub — validates args then returns not-implemented for component modification)
- [x] 4.5 Complete DestroyEntityTool for World context (checked 2026-07-03: implemented)

## 5. Proof of Concept
- [x] 5.1 Refactor TorusFeatureBuilder to use SetTerrainTool in key paths (checked 2026-07-03: calls ExecuteTool("setterrain", ...) with direct World.SetTerrain fallback)
- [x] 5.2 Test TorusFeatureBuilder with tool execution (checked 2026-07-03: WorldBuildingToolIntegrationTests covers with-tools, fallback, and tool-execution paths)

## 6. Testing
- [x] 6.1 Add unit tests for tools with WorldBuildingToolContext (checked 2026-07-03: SetTerrainToolTests, MoveEntityToolTests, DestroyEntityToolTests)
- [x] 6.2 Add integration test: Torus builder using tools (checked 2026-07-03: Aetherium.Test/Agents/Tools/Integration/WorldBuildingToolIntegrationTests.cs)
- [x] 6.3 Validate AgentToolRegistry resolves world building tools (checked 2026-07-03: ToolRegistry_ShouldDiscoverWorldBuildingTools resolves all five tools)
- [x] 6.4 Validate tool profiles gate access correctly (checked 2026-07-03: ToolProfile_WorldBuilderShouldAccessWorldBuildingTools)

## 7. Documentation
- [x] 7.1 Update docs/agents/TOOLS.md with world building examples (checked 2026-07-03: World Building Integration section present)
- [x] 7.2 Update docs/pcg-tools.md with world building usage (checked 2026-07-03: World Building Tool Integration section present)
- [ ] 7.3 Run openspec validate add-worldbuilding-tool-integration --strict (still open 2026-07-03: cannot verify — no openspec CLI available in this environment)
