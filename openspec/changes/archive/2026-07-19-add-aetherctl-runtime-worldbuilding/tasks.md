## 1. Server
- [x] 1.1 Add `EntityFactory.TryCreate(entityType, world)` supporting monster/wolf/bear/bandit/snake/zombie (mirrors `GameMapGrain.SpawnEntityAsync`, incl. Monster tile-type registration)
- [x] 1.2 Implement `SpawnEntityTool.ExecuteAsync` using the factory + passability/occupancy checks; return the new entity id in `Data`
- [x] 1.3 Add `IGameManagementGrain.ExecuteWorldToolAsync(worldId, toolId, args)`: operator-gated, resolves the live world via `WorldRegistry`, requires the tool to declare `world_edit`, executes with `WorldBuildingToolContext`
- [x] 1.4 Clear failures for: unknown world, unknown tool, tool not requiring `world_edit`

## 2. CLI
- [x] 2.1 `aetherctl world edit <worldId> <toolId> --args '<json>' [--json]`
- [x] 2.2 `aetherctl world spawn <worldId> --type <t> --at x,y,z [--json]` (wrapper over spawnentity)
- [x] 2.3 Non-zero exit + clear errors on failure

## 3. Tests (linked to spec requirements)
- [x] 3.1 Server: spawn a creature at a passable location → success + entity visible in world snapshot
- [x] 3.2 Server: setterrain via runtime path changes terrain; destroyentity removes a spawned entity
- [x] 3.3 Server: unknown world / unknown tool / non-world_edit tool (e.g. `move`) → failure
- [x] 3.4 Server: operator gate denies when disabled
- [x] 3.5 CLI: structural coverage for `world edit` and `world spawn`

## 4. Docs
- [x] 4.1 Update `docs/agents/README.md` + `TOOL_SYSTEM_STATUS.md`
