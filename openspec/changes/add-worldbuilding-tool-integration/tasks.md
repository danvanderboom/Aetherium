## 1. OpenSpec Change
- [x] 1.1 Scaffold OpenSpec change add-worldbuilding-tool-integration with deltas

## 2. Context & Wiring
- [x] 2.1 Add WorldBuildingToolContext extending ToolExecutionContext
- [x] 2.2 Verify AgentToolRegistry registration in Program.cs (registered + DiscoverTools over executing assembly)

## 3. WorldBuilder Plumbing
- [x] 3.1 Update WorldFeatureBuilder base class with optional registry/provider support
- [x] 3.2 Add ExecuteToolAsync helper method to WorldFeatureBuilder (plus sync ExecuteTool wrapper)
- [x] 3.3 Update feature builder constructors to accept registry/provider when available

## 4. Tool Implementation
- [x] 4.1 Implement SetTerrainTool with schema and execution
- [x] 4.2 Complete SpawnEntityTool for World context (reflection factory over concrete Entity subclasses with a parameterless ctor)
- [x] 4.3 Complete MoveEntityTool for World context
- [x] 4.4 Complete ModifyEntityTool for World context (add/remove components by type name; WorldLocation/Tile protected. Per-field property editing deferred as a future enhancement.)
- [x] 4.5 Complete DestroyEntityTool for World context

## 5. Proof of Concept
- [x] 5.1 Refactor TorusFeatureBuilder to use SetTerrainTool in key paths (underground terrain, with direct-SetTerrain fallback)
- [x] 5.2 Test TorusFeatureBuilder with tool execution (integration test)

## 6. Testing
- [x] 6.1 Add unit tests for tools with WorldBuildingToolContext (SetTerrain, Spawn, Move, Modify, Destroy)
- [x] 6.2 Add integration test: Torus builder using tools
- [x] 6.3 Validate AgentToolRegistry resolves world building tools
- [x] 6.4 Validate tool profiles gate access correctly (world_edit capability checks in every tool + context tests)

## 7. Documentation
- [x] 7.1 Update docs/agents/TOOLS.md with world building examples (statuses reconciled with implementation)
- [x] 7.2 Update docs/pcg-tools.md with world building usage
- [x] 7.3 Validate change specs (openspec CLI not installed in this environment; deltas reviewed manually against openspec conventions)
