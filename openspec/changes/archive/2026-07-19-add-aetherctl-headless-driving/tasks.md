## 1. Server: Headless session provisioning
- [x] 1.1 Add `GameSessionManager.CreateHeadlessSession(connectionId, worldId, world, startLocation?)` reusing the multi-world `GameSession` ctor and `InitializePlayer` placement
- [x] 1.2 Tag headless sessions (`GameSession.IsHeadless`) so they can be reaped/filtered
- [x] 1.3 Add `IGameManagementGrain.CreateHeadlessSessionAsync(worldId, startX, startY, startZ, profile)` → returns a result with `sessionId`; generate synthetic `headless:{guid}` connection id and register it
- [x] 1.4 Resolve the target `World` via the in-process `WorldRegistry` (published by `GameMapGrain`) before session construction; return failure if the world does not exist
- [x] 1.5 Add an idle-timeout reaper (`ReapIdleHeadlessSessionsAsync` + grain timer, `AETHERIUM_HEADLESS_IDLE_SECONDS`, default 1800s) that terminates only headless sessions

## 2. Server: Operator perception retrieval
- [x] 2.1 Add an `absoluteCoordinates` option to `PerceptionService.ComputePerception` / `GameSession.GetPerception` so `PlayerLocation` carries true world coordinates instead of (0,0,0) (default unchanged)
- [x] 2.2 Expose it via a `GetPerceptionAsync(sessionId, bool absoluteCoordinates)` grain overload returning JSON `PerceptionDto`

## 3. Server: World state snapshot
- [x] 3.1 Implement the stubbed `GameMapGrain.GetWorldAsync()` to serialize a `WorldSnapshotDto` (tiles + entities) via a shared `WorldSnapshotBuilder`
- [x] 3.2 Add `WorldSnapshotDto` (+ `EntitySnapshotDto`, `TileSnapshotDto`) in `Aetherium.Model`
- [x] 3.3 Add `IGameManagementGrain.GetWorldSnapshotAsync(worldId)` returning the snapshot (JSON); cap output and flag/log truncation (region-bounded snapshots deferred to a follow-up)
- [x] 3.4 Return `null` for unknown `worldId`

## 4. Server: Operator authorization
- [x] 4.1 Add an operator gate (`OperatorAccess`, `AETHERIUM_OPERATOR_DISABLED`) on `CreateHeadlessSessionAsync`, absolute-coordinate perception, and `GetWorldSnapshotAsync`
- [x] 4.2 These are never exposed as player/agent tools, so the `Player`/`Explorer` profiles cannot reach them; the trusted localhost-Orleans path is enabled by default

## 5. CLI (aetherctl)
- [x] 5.1 Wire `aetherctl session create --world <id> [--at x,y,z] [--profile <p>]` to `CreateHeadlessSessionAsync`; print/`--json` the `sessionId` (replaced the stub)
- [x] 5.2 Add `Aetherctl/Commands/PerceptionCommands.cs`: `aetherctl perception get <sessionId> [--absolute] [--json]`
- [x] 5.3 Add `aetherctl world dump <worldId> [--json]` (tiles + entities) in `WorldCommands.cs`
- [x] 5.4 Register new commands in `Program.cs`
- [x] 5.5 Graceful errors + non-zero exit codes when the server lacks support or ids are unknown

## 6. Tests
- [x] 6.1 Server: create headless session in an existing world → session listed, character placed; unknown world → failure; explicit start location honored
- [x] 6.2 Server: drive the headless session (SetDirectionalVision) and assert perception reflects the change
- [x] 6.3 Server: absolute perception returns true coordinates (matching the snapshot); default perception still relativized
- [x] 6.4 Server: `GetWorldSnapshotAsync` returns all entities/tiles regardless of FOV; unknown world → null
- [x] 6.5 Server: operator gate denies headless/snapshot/absolute reads when disabled; permits when enabled
- [x] 6.6 CLI (`Aetherctl.Test`): `session create`, `perception get`, `world dump` structural + option/argument coverage
- [x] 6.7 End-to-end (headless): `session create` → agent runner `AttachAsync` with no game client connected
- [x] 6.8 Server: reaper removes idle headless sessions and spares non-idle ones

## 7. Docs
- [x] 7.1 Update `docs/agents/README.md` with the headless flow (create session → drive/attach) and the new `perception`/`world dump` commands
- [x] 7.2 Note in `TOOL_SYSTEM_STATUS.md` that `session create` is now supported and list the follow-ups left out of scope
