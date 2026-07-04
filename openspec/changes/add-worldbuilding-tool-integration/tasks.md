> Status (2026-07-04): fully implemented — context, builder plumbing, all five world-building tools (SetTerrain/SpawnEntity/MoveEntity/ModifyEntity/DestroyEntity), Torus proof-of-concept, tests, and docs all exist in code. SpawnEntityTool and ModifyEntityTool (previously honest error stubs) are now complete.

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
- [x] 4.2 Complete SpawnEntityTool for World context (checked 2026-07-04: reflection factory over concrete Entity subclasses with a parameterless ctor; previously an honest error stub)
- [x] 4.3 Complete MoveEntityTool for World context (checked 2026-07-03: implemented; spatial-index update fixed in Phase 2)
- [x] 4.4 Complete ModifyEntityTool for World context (checked 2026-07-04: adds/removes components by type name; WorldLocation/Tile protected from removal. Per-field property editing deferred as a future enhancement. Previously an honest error stub.)
- [x] 4.5 Complete DestroyEntityTool for World context (checked 2026-07-03: implemented)

## 5. Proof of Concept
- [x] 5.1 Refactor TorusFeatureBuilder to use SetTerrainTool in key paths (checked 2026-07-03: calls ExecuteTool("setterrain", ...) with direct World.SetTerrain fallback)
- [x] 5.2 Test TorusFeatureBuilder with tool execution (checked 2026-07-03: WorldBuildingToolIntegrationTests covers with-tools, fallback, and tool-execution paths)

## 6. Testing
- [x] 6.1 Add unit tests for tools with WorldBuildingToolContext (checked 2026-07-04: SetTerrainToolTests, MoveEntityToolTests, DestroyEntityToolTests, SpawnEntityToolTests, ModifyEntityToolTests)
- [x] 6.2 Add integration test: Torus builder using tools (checked 2026-07-03: WorldBuildingToolIntegrationTests)
- [x] 6.3 Validate AgentToolRegistry resolves world building tools (checked 2026-07-03: registry resolution test covers all five tools)
- [x] 6.4 Validate tool profiles gate access correctly (checked 2026-07-03: world_edit capability checks in every tool + context tests)

## 7. Documentation
- [x] 7.1 Update docs/agents/TOOLS.md with world building examples (checked 2026-07-04: World Building Integration section present; statuses reconciled with implementation)
- [x] 7.2 Update docs/pcg-tools.md with world building usage (checked 2026-07-04: World Building Tool Integration section present)
- [ ] 7.3 Run openspec validate add-worldbuilding-tool-integration --strict (still open 2026-07-04: cannot verify — no openspec CLI available in this environment)
