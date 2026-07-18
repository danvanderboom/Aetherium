## Context
Character control, perception, and all gameplay verbs on `IGameManagementGrain` resolve state through `sessionId → connectionId → GameSession` (`GameManagementGrain.cs:940-946`). A `GameSession` bundles a `ConnectionId`, a `World`, and an auto-created `Character Player` (`GameSession.cs:14-20`, placed in `InitializePlayer`, `:95-144`). The only code path that constructs a `GameSession` is `GameHub.OnConnectedAsync` on SignalR connect (`GameHub.cs:104-112`). `aetherctl session create` is therefore a hard-coded stub (`SessionCommands.cs:105-118`), and the agent runner can only `AttachAsync` to a session that already exists (`AgentRunnerGrain.cs:60-84`).

Two facts make a headless path cheap:
1. `sessionId` and `connectionId` are opaque string keys; nothing validates them against a live SignalR transport. The post-action `ReceivePerceptionUpdate` push simply no-ops when the connection id has no connected client (`GameManagementGrain.cs:971-976`).
2. Existing tests already synthesize sessions and register them directly (e.g. `GameManagementGrainTests.cs`, `GameSessionManagerTests.cs`).

## Goals / Non-Goals
- Goals:
  - Provision a world + placed character and drive it entirely from `aetherctl`, with no game client running.
  - Read a character's perception (including true world coordinates) from the CLI.
  - Read an omniscient, FOV-independent snapshot of a world's tiles and entities.
  - Keep god-view capabilities gated so ordinary players cannot reach them.
- Non-Goals (explicit follow-ups):
  - Scripted/batch action sequences or a `ScriptedPolicy` for the runner (a caller can already loop `tools test` / `agent step`).
  - Runtime world-building via CLI / finishing `SpawnEntityTool` (needs an entity factory/prefab system).
  - Activating the dormant ECS `Memory` component or exposing memory.
  - Surfacing `AgentTelemetryGrain` snapshots/replays via the CLI.
  - A multi-character scenario/orchestration harness.

## Decisions
- **Decision: Represent a headless session with a synthetic connection id.** `CreateHeadlessSessionAsync` generates a connection id such as `headless:{guid}`, calls a new `GameSessionManager.CreateHeadlessSession(connId, worldId, world, startLocation?)` (mirroring the existing multi-world `GameSession` ctor at `GameSession.cs:80-90`), then `RegisterSessionAsync(session.SessionId, connId)`. Every existing verb (`GetPerceptionAsync`, `MoveAsync`, `ExecuteToolAsync`, agent attach/run) then works unchanged.
  - Alternatives considered: (a) a brand-new `IHeadlessAgentGrain` that owns its own world/entity — rejected as duplicating `GameSession`; (b) making `AgentRunnerGrain.AttachAsync` create the session — rejected because attach must stay idempotent and session lifetime is broader than any one runner.
- **Decision: Explicit teardown, plus idle reaping.** A headless session has no SignalR disconnect to trigger `OnDisconnectedAsync`, so it must be removed via `TerminateSessionAsync` (already supported) and, defensively, by an idle-timeout reaper so abandoned automation runs don't leak sessions/worlds. Headless sessions are tagged (`IsHeadless`) so the reaper only targets them.
- **Decision: Absolute coordinates are opt-in and gated.** Normal perception stays relativized to `(0,0,0)` (an intentional information-hiding property for real clients, `PerceptionService.cs:221`). Operator reads pass an `absolute:true` flag that returns the un-relativized `PlayerLocation`; this is available only to the operator/dev capability, never to the `Player` profile.
- **Decision: World snapshot is a new DTO, not the client `PerceptionDto`.** `GetWorldSnapshotAsync(worldId)` returns a `WorldSnapshotDto { WorldId, MapId, Width, Height, Depth, Tiles, Entities[] }` where each entity carries absolute location, id, type, and a component summary. It is built by implementing the `IGameMapGrain.GetWorldAsync()` stub (`GameMapGrain.cs:163`) to serialize `World.TileTypes` + `World.Entities`, consumed by the management grain.
- **Decision: Reuse the existing capability model for gating.** Add an `operator`/`dev_tools` capability (in the spirit of the existing `admin` capability on `AgentToolProfile`) required for headless-session creation, absolute-coordinate perception, and world snapshots. In a dev build the CLI's Orleans localhost path is already implicitly trusted; the SignalR/B2C path enforces the capability via role.

## Risks / Trade-offs
- **Leaked headless sessions/worlds** → idle-timeout reaper + `IsHeadless` tagging + `session close` remains authoritative.
- **God-view exposure** (absolute coords, full entity dump) leaking to players → capability gate + keep it off the `Player`/`Explorer` profiles; localhost-Orleans dev path documented as trusted-only.
- **Snapshot size on large worlds** → snapshot supports an optional bounds/region filter; default caps and truncation are logged rather than silently dropped.
- **Divergence from real client behavior** (a headless session skips client-only init) → headless ctor reuses the same `InitializePlayer` placement logic so world/character state matches a real join.

## Migration Plan
Purely additive. New grain methods, a new DTO, a new CLI capability, and one implemented stub (`GetWorldAsync`). No existing signatures change; the relativized `PerceptionDto` default is unchanged. Rollback = remove the new methods/commands; nothing else depends on them.

## Open Questions
- Should `CreateHeadlessSessionAsync` accept a world *builder/template* (to spin up a throwaway world in one call) in addition to an existing `worldId`? Leaning: existing `worldId` only for this change; `world create` already exists to make one first.
- Exact home for the operator capability — extend `AgentToolProfile` with an `Operator` profile vs. a separate management-grain authorization check. Leaning: management-grain authorization check, since these are grain methods, not `IAgentTool`s.
- Idle-reaper default timeout (proposed: 30 min) and whether it is configurable via env var.
