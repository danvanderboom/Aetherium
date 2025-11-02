## 1. OpenSpec Change
- [x] 1.1 Scaffold OpenSpec change add-worldbuilding-tool-integration with deltas

## 2. Context & Wiring
- [ ] 2.1 Add WorldBuildingToolContext extending ToolExecutionContext
- [ ] 2.2 Verify AgentToolRegistry registration in Program.cs (should already exist)

## 3. WorldBuilder Plumbing
- [ ] 3.1 Update WorldFeatureBuilder base class with optional registry/provider support
- [ ] 3.2 Add ExecuteToolAsync helper method to WorldFeatureBuilder
- [ ] 3.3 Update feature builder constructors to accept registry/provider when available

## 4. Tool Implementation
- [ ] 4.1 Implement SetTerrainTool with schema and execution
- [ ] 4.2 Complete SpawnEntityTool for World context
- [ ] 4.3 Complete MoveEntityTool for World context
- [ ] 4.4 Complete ModifyEntityTool for World context
- [ ] 4.5 Complete DestroyEntityTool for World context

## 5. Proof of Concept
- [ ] 5.1 Refactor TorusFeatureBuilder to use SetTerrainTool in key paths
- [ ] 5.2 Test TorusFeatureBuilder with tool execution

## 6. Testing
- [ ] 6.1 Add unit tests for tools with WorldBuildingToolContext
- [ ] 6.2 Add integration test: Torus builder using tools
- [ ] 6.3 Validate AgentToolRegistry resolves world building tools
- [ ] 6.4 Validate tool profiles gate access correctly

## 7. Documentation
- [ ] 7.1 Update docs/agents/TOOLS.md with world building examples
- [ ] 7.2 Update docs/pcg-tools.md with world building usage
- [ ] 7.3 Run openspec validate add-worldbuilding-tool-integration --strict

