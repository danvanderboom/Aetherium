## Why
The world-building tools (`spawnentity`, `setterrain`, `moveentity`, `modifyentity`, `destroyentity`) execute only during world *generation* via `WorldFeatureBuilder`; there is no path to run them against a live, running world, and `SpawnEntityTool` is a stub that always errors ("requires entity factory/prefab system"). Operators therefore cannot place a key/creature for a test, repair terrain, or clear an entity without regenerating the world. The `WorldRegistry` bridge (change `add-aetherctl-headless-driving`) now gives the management grain direct access to live worlds, making runtime world-editing a small addition.

## What Changes
- Add `IGameManagementGrain.ExecuteWorldToolAsync(worldId, toolId, args)` that resolves the live world from the registry, builds a `WorldBuildingToolContext`, and executes a world-building tool against the running world. Only tools requiring the `world_edit` capability are eligible; gated behind operator access.
- Implement `SpawnEntityTool` using a new `EntityFactory` (creature types mirroring `GameMapGrain.SpawnEntityAsync`: monster/wolf/bear/bandit/snake/zombie), with passability and occupancy checks.
- Add CLI commands: `aetherctl world edit <worldId> <toolId> --args '<json>'` (generic) and `aetherctl world spawn <worldId> --type <t> --at x,y,z` (convenience wrapper).
- Overlap note: the pending change `add-worldbuilding-tool-integration` covers completing these tools for the *generation* path; this change adds the *runtime* execution path and the minimal factory `SpawnEntityTool` needs. The factory satisfies that change's SpawnEntityTool requirement too.

## Impact
- Affected specs:
  - `game-management-grain` (ADDED: Runtime World Tool Execution)
  - `aetherctl` (ADDED: World Edit Commands)
- Affected code:
  - `Aetherium.Server/Management/IGameManagementGrain.cs`, `GameManagementGrain.cs` — new grain method
  - `Aetherium.Server/Entities/EntityFactory.cs` (new) — creature creation shared logic
  - `Aetherium.Server/Agents/Tools/WorldBuilding/SpawnEntityTool.cs` — real implementation
  - `Aetherctl/Commands/WorldCommands.cs`, tests in `Aetherium.Test`/`Aetherctl.Test`
- Non-Goals: prefab/structure building (`BuildStructureAsync` stays stubbed); item-type spawning beyond creatures (needs an item prefab catalog — follow-up); persistence of runtime edits (worlds are in-memory; persistence is the existing `SaveMapAsync` path).
