## Why
Today a playable character exists only as an appendage of a `GameSession`, and a `GameSession` is only ever created when an interactive SignalR client connects (`GameHub.OnConnectedAsync` → `GameSessionManager.CreateSession`). As a result `aetherctl` cannot, on its own, place a character in a world, read that character's perception/location, or interrogate a world beyond one player's field of view. This blocks headless automation, deterministic integration testing, and operator debugging. The underlying plumbing already tolerates client-less sessions — `sessionId`/`connectionId` are just addressing keys, and the unit tests synthesize sessions freely — so the gap is a small set of server methods plus CLI shims, not an architectural rework.

## What Changes
- Add `IGameManagementGrain.CreateHeadlessSessionAsync(worldId, startLocation?, profile?)` that provisions a `GameSession` (world + placed `Character`) against a synthetic connection id and registers it, returning the new `sessionId`. No interactive client required.
- Add operator perception retrieval: reaffirm `GetPerceptionAsync(sessionId)` as an operator-facing contract and add an **absolute-coordinates** debug option so the player's true world location is visible (today perception is always relativized to `(0,0,0)`).
- Add `IGameManagementGrain.GetWorldSnapshotAsync(worldId)` returning an omniscient, FOV-independent snapshot of a world's tiles and entities; implement the currently-stubbed `IGameMapGrain.GetWorldAsync()` that backs it.
- Wire the CLI: enable the stubbed `aetherctl session create --world <id> [--at x,y,z] [--profile <p>]`, add `aetherctl perception get <sessionId> [--absolute] [--json]`, and add `aetherctl world dump <worldId> [--json]`.
- Gate headless-session creation and absolute-coordinate/world-snapshot reads behind an operator/dev authorization capability (these expose god-view state and must not be available to ordinary player profiles).

**Scope note:** This is the keystone change. The following are intentionally **out of scope** and left as follow-ups (see Non-Goals): scripted/batch action sequences, runtime world-building (finishing `SpawnEntityTool`), activating the dormant `Memory` component, surfacing `AgentTelemetryGrain` via the CLI, and a multi-character scenario harness. Each becomes straightforward once headless sessions, perception read, and world snapshot exist.

## Impact
- Affected specs:
  - `game-management-grain` (ADDED: headless session provisioning, operator perception retrieval, world state snapshot, operator authorization)
  - `aetherctl` (NEW capability: session-create, perception-inspection, and world-inspection commands)
- Fulfills the pending "Create session" scenario in `changes/add-aetherctl-extensions` that is currently blocked "await server APIs".
- Affected code:
  - `Aetherium.Server/Management/IGameManagementGrain.cs`, `GameManagementGrain.cs` — new grain methods + operator auth
  - `Aetherium.Server/GameSessionManager.cs`, `GameSession.cs` — headless session construction / synthetic connection id, explicit teardown
  - `Aetherium.Server/MultiWorld/IGameMapGrain.cs`, `GameMapGrain.cs` — implement `GetWorldAsync()` (tiles + entities)
  - `Aetherium.Model/PerceptionDto.cs` and a new world-snapshot DTO — absolute-coordinate option / snapshot shape
  - `Aetherctl/Commands/SessionCommands.cs` (wire `create`), new `Aetherctl/Commands/PerceptionCommands.cs`, `Aetherctl/Commands/WorldCommands.cs` (add `dump`), `Aetherctl/Program.cs`, `Aetherctl/Orleans/OrleansClientFactory.cs`
  - `Aetherium.Test/*`, `Aetherctl.Test/*` — new tests
