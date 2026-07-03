# Audit: Client-Server Communication

*Audit date: 2026-07-03 · Scope: `GameHub`, `Hubs/`, `GameSession(Manager)`, `Middleware/ApiKeyMiddleware`, `Controllers/`, `Aetherium.Model` DTOs, `Aetherium.Console/Client/GameClient`. Findings marked **Verified** (code-confirmed with `file:line`) or **Suspected**.*

> **Reconciliation — `develop` @ 2026-07-03.** The grain-authoritative multiplayer work landed a delta protocol and grain-hosted worlds. **FIXED:** reconnect/resume now works (the `GameClient` reconnect state machine re-raises `Connected` via the `Reconnected` path, and grain-bound sessions survive because the grain holds canonical state); `JoinWorld` now binds a session to a grain world (resolves grains, `JoinPlayerAsync`, snapshot hydration, gateway swap) instead of erroring and zombie-ing the client; and the `GameHub.ExecuteTool` authorization bypass is closed (the hub now checks `AgentToolProfile.Player.IsToolAllowed`). **STILL STANDS:** the Critical no-server-side-movement-validation finding — and it now spans a **third** action path: the new `GameMapGrain.MoveAsync` also applies deltas with no passability check, and because `MoveTool` routes distance straight through the gateway, the tool's advertised 1–100 clamp is silently bypassed on the grain path (arguably *worse* — a reader of `MoveTool` would assume validation holds). Map-wide door/use (grain `ToggleDoorAsync` inherited the no-adjacency bug), anonymous REST control-plane, ManagementHub-without-B2C 500s, absent wire versioning, and the `AmbientTint` tuple loss all STAND. **Path drift worsened:** obsolete hub methods + `LocalMutationGateway` + `GrainMutationGateway` now coexist (three paths). **Improved:** cross-path concurrency is now safe for *grain-bound* sessions (Orleans single-threading) though legacy sessions remain racy; the delta protocol is cleanly perception-pure (clients still receive only filtered `PerceptionDto`s, ordered by a monotonic per-map `Sequence`). Detail in the Reconciliation section at the end.

## Summary

The perception/information-hiding design is genuinely good — clients receive only FOV/lighting-gated, viewport-bounded, relative-coordinate perception, and absolute location never leaves the server (tests enforce this). But the **action side of the protocol is under-defended and internally inconsistent**. There is no server-side movement validation (walls and distance are unchecked), door/use interactions have no range check, the REST control-plane is anonymous, disconnect destroys all player state with no resume, and two parallel action paths (13 `[Obsolete]` hub methods vs. `ExecuteTool`) have already drifted in behavior. Most of this is invisible today because the shipped console client compiles against the same `Aetherium.Model` and uses only the legacy methods — the risks bite the moment a second, out-of-band client (Unity/Unreal) or the REST surface is exercised in earnest.

| Severity | Count | Headline items |
|---|---|---|
| Critical | 1 | No server-side movement validation (walk through walls / teleport) |
| High | 5 | Map-wide door/use interactions; anonymous REST control-plane; no reconnect/resume; JoinWorld zombies the session; ManagementHub 500s without B2C |
| Medium | 6 | Path drift (obsolete vs ExecuteTool); AmbientTint tuple lost in JSON; cross-path concurrency on unsynchronized state; ExecuteTool auth not centrally enforced; inconsistent error propagation; Unity JsonUtility contract friction |
| Low | 4 | Diagnostic world built per connect; empty GameStateDto.TileTypes; test-mode file I/O in hot path; volatile grain session index |

## Critical

**No server-side movement validation — clients can walk through walls and teleport.** *Verified.* `GameSession.MoveView` (`GameSession.cs:220-255`) applies the delta with no passability/`ObstructsMovement` check; `World.MoveEntity` (`World.cs:225-251`) has none either. `MovePlayer(direction, distance)` accepts any int (`GameHub.cs:171`) — `distance = 1_000_000` (or negative) executes as-is. A validated `World.TryMove` exists (`World.cs:293+`) but is dead in this path — and is itself buggy (West/East map to the same deltas as North/South, `World.cs:276-283`). Any client can invoke `MovePlayer(Forward, 100000)` and appear anywhere, through walls. The spec requires the server to "validate and execute the action" (`client-server-communication/spec.md:15-18`).

## High

**Open/Close/Use have no range check — act on any entity map-wide.** *Verified.* `InteractionSystem.ToggleDoor` (`InteractionSystem.cs:309-356`) resolves the target by ID only, with no adjacency/distance test — unlike `TryPickup` (co-location required, `:52`) and `TryActivate` (distance ≤ 1, `:367-371`). `TryUse` with an explicit `usageId` jumps to `TryUseWithMode` (`:132-135`), bypassing the `adjacent-target` context filter (`:204,236-240`), so `unlock-door`/`lockpick`/`force-open` work at unlimited range. A client can read a door's entity ID from an earlier perception frame and unlock/open it from across the map.

**Anonymous control-plane REST surface.** *Verified.* Only `/api/management` write verbs are protected (`ApiKeyMiddleware.cs:36,43`); no controller carries `[Authorize]`. `POST api/Cluster/{id}/economy/tick` (`ClusterController.cs:552`), `POST api/MetaProgression/{playerId}/discoveries` (`MetaProgressionController.cs:98`), `POST api/Adaptation/rules/reload` (`AdaptationController.cs:130`), `POST api/Benchmark` (`BenchmarkController.cs:52`) are fully anonymous in all environments. Any network peer can forge another player's meta-progression or tick the cluster economy.

**No reconnect/resume — a disconnect destroys all player state.** *Verified.* Each connection gets a fresh session and a freshly built world (`GameHub.cs:104`, `GameSessionManager.cs:22-27`); `OnDisconnectedAsync` removes it (`GameHub.cs:163`). SignalR auto-reconnect issues a new ConnectionId, so any blip yields a new random world, empty inventory, and a lost meta-progression identity (keyed by the per-connection `session.SessionId`, `GameHub.cs:523-525`). Cleanup itself is leak-free, but resume is impossible.

**JoinWorld destroys the session then fails, leaving a zombie client.** *Verified.* `GameHub.cs:623-672` removes the existing session (`:628-632`) then unconditionally returns `OperationResult.Error("Joining worlds via GameHub is not yet supported.")` (`:665`). Afterward every obsolete method silently no-ops (`session == null → return`, e.g. `:173-175`) and `ExecuteTool` returns "No active session" — the client renders a frozen perception forever.

**ManagementHub is unusable (500s) when Azure AD B2C is not configured.** *Suspected (static analysis; not runtime-reproduced).* `[Authorize]` on the hub (`ManagementHub.cs:19`) plus conditional registration of auth + the "Admin" policy only when B2C config exists (`Program.cs:55-79`), and `UseAuthentication/UseAuthorization` added only conditionally (`Program.cs:372-377`). Without B2C, `/managementHub` hits auth metadata with no registered scheme → runtime failure. Fails closed (good) but silently breaks `aetherctl` in the default dev setup, with nothing logging why.

## Medium

**Two action paths have already drifted (obsolete hub methods vs `ExecuteTool`).** *Verified.* 13 `[Obsolete]` methods remain on `GameHub` (`MovePlayer:170`, `RotatePlayer:187`, `RotatePlayerDegrees:205`, `ToggleDirectionalVision:223`, `ChangeLevel:238`, `JumpToRandomLocation:252`, `Pickup:266`, `Drop:289`, `Use:302`, `Open:342`, `Close:365`, `SetLightingMode:547`, `SetVisionMode:561`). They do not share code with the tools and have diverged:
- *Move distance:* `MovePlayer(dir,distance)` → `session.MoveView(dir,distance)` (unbounded). `ExecuteTool("move")` validates 1–100 (`MoveTool.cs:64`) but, when Orleans is on (always, `GameHub.cs:722`), calls `ManagementGrain.MoveAsync` which **ignores distance and moves exactly 1 tile** (`GameManagementGrain.cs:426`). One command, three behaviors.
- *Narrative side-effects:* obsolete `Pickup`/`Use` emit `item_collected`/`item_used` events (`GameHub.cs:278,330`); `ExecuteTool` emits only `door_opened`/`door_closed` (`:737-751`). Migrating a client to `ExecuteTool` silently stops item-narrative consequences.
- *Disambiguation shape:* obsolete `Use` returns `Success=false` + `Options`; `ExecuteTool("use")` returns `Success=true` with options in `Data["options"]` (`UseTool.cs:80-93`) — opposite contracts.
- *Dispatch order:* Move/Pickup/Open/Drop tools are grain-first; UseTool is session-first (`UseTool.cs:75`); RotateTool is session-only.

**`PerceptionDto.AmbientTint` (a ValueTuple) does not survive JSON serialization.** *Verified.* `PerceptionDto.cs:45` declares `(double r,double g,double b)`; ValueTuple members are fields, which SignalR's System.Text.Json protocol does not serialize by default → wire value `{}` → client sees `(0,0,0)`. The console client's sunrise/sunset tint (`ClientConsoleMapView.cs:283-392`) never activates over the network. The round-trip test doesn't assert it, so it passes.

**Cross-path concurrency on unsynchronized `GameSession`/`World`.** *Verified.* SignalR serializes per-connection invocations (default `MaximumParallelInvocationsPerClient=1`, unset in `Program.cs:36-39`), but `GameManagementGrain` mutates the *same* `GameSession` objects from silo threads (`GameManagementGrain.cs:407-443`) concurrently with hub calls — reachable via anonymous REST `POST api/management/sessions/{id}/attach` (`ManagementController.cs:288`) or agent runners. `ViewLocation`/`HeadingDegrees` are plain properties; `World.MoveEntity`'s remove→set→add is non-atomic (`World.cs:232-248`) and can drop the entity from the location index. Multi-world mode shares one `World` across sessions (`GameSessionManager.cs:32-37`), widening the race. (`GameSessionManager` itself is a correct `ConcurrentDictionary`.)

**`ExecuteTool` authorization is per-tool convention, not enforced centrally.** *Verified.* `GameHub.ExecuteTool` fetches any registered tool by ID (`GameHub.cs:705`) without consulting `AgentToolProfile.Player`; only `ListAvailableTools` filters by profile (`:781-785`). Today all 22 tools self-check capabilities, so players can't reach admin/world-edit tools — but the first tool that forgets its `HasCapability` check becomes immediately player-invokable. Defense-in-depth gap.

**Error propagation is inconsistent; some documented client events are never sent.** *Verified.* Obsolete void methods fail silently on missing session (`GameHub.cs:173-175`); interaction methods return DTOs; `ExecuteTool` catches into `{Success=false}` (`:759-767`); `ListWorlds`/`GetWorldInfo` swallow errors into empty results (`:592-596`). The client registers no error handler (`GameClient.cs`). (Note: this audit prompted a correction — an earlier draft of `docs/architecture/server.md` listed `ReceiveInteractionResult`/`ReceiveError` as active server→client events; the server sends only `ReceiveGameState`/`ReceivePerceptionUpdate`. The doc has been corrected.)

**Unity contract friction: JsonUtility + Dictionaries + properties.** *Verified.* `PerceptionLite` uses `{get;set;}` properties and `Dictionary<string,…>` (`PerceptionLite.cs:12-17`) but is parsed with `JsonUtility.FromJson` (`PerceptionMockProvider.cs:55`), which supports neither → `Visuals`/`TileTypes` are always empty. The SignalR client is a stub (`PerceptionSignalRClient.cs:38-48`). The server's string-keyed `Visuals` ("x,y,z" keys) and enum-keyed `ThingsSeen` are inherently JsonUtility-hostile. (See also [unity-and-dashboard.md](unity-and-dashboard.md).)

## Low

- **Diagnostic world built per connection** — `FovDiagnosticWorldBuilder("open_space")` on every connect (`GameHub.cs:80-86`); a cheap resource-exhaustion vector on an anonymous hub. *Verified.*
- **`GameStateDto.TileTypes` never populated** — only PlayerId/PlayerHeading sent (`GameHub.cs:122-127`); clients rely on the copy inside every PerceptionDto. *Verified.*
- **Test-mode file I/O in the hot perception path** — `UI_SELFTEST_MODE` writes inside `ComputePerception` (`PerceptionService.cs:113-144` etc.), env-var checked per call. *Verified.*
- **`GameManagementGrain` session index is volatile grain state** — in-memory fields (`GameManagementGrain.cs:24-25`); grain deactivation orphans registrations while live sessions persist; co-hosted-only. *Verified.*

## No protocol versioning

*Verified.* No version field, handshake, or capability exchange anywhere; `PerceptionDto.UpdateTimestamp` is a random `Guid` (`PerceptionDto.cs:30`), unusable for ordering/dedupe. Same-repo lockstep deployment makes this low-risk *today*, but `GameStateDto` already carries a silent breaking change ("PlayerLocation removed", `GameStateDto.cs:9`), enums serialize as ints with non-conventional ordering (`SharedEnums.cs:5-13` — any reorder is a silent wire break), and the Unity client hand-copies a DTO subset with no drift detection. Risk escalates to High once any client ships out-of-band.

## Strengths

- **Deliberate information hiding**: relative coordinates, player at origin (0,0,0); absolute location never serialized (`PerceptionService.cs:217-286`; asserted by `ClientServerCommunicationTests.cs:38-41,269-272`).
- **Perception is FOV/lighting-gated and viewport-bounded** (`PerceptionService.cs:101-110,146-182`; verified by tests).
- **All 22 agent tools consistently self-enforce capability checks**; `AgentToolProfile.IsToolAllowed` has a sound category+capability AND rule with deny precedence (`AgentToolProfile.cs:40-79`).
- **`ApiKeyMiddleware` fails closed in production** (503 when unconfigured); key from configuration, not hardcoded.
- **ManagementHub write methods all carry `[Authorize(Policy="Admin")]`** (verified on all 11 mutating methods); reads are authenticated via class-level `[Authorize]`.
- **Consistent System.Text.Json on the server** — no Newtonsoft split-brain.
- **Disconnect cleanup exists** and unregisters from the management grain (`GameHub.cs:138-165`).

## Spec alignment (`client-server-communication/spec.md`)

- **Violated** — "server SHALL validate and execute the action" (`:15-18`): movement is unvalidated (see Critical).
- **Violated** — door "toggles if unlocked and adjacent/accessible" (`:159-161`): no adjacency check.
- **Deliberate deviation** — "SHALL include player's current WorldLocation" (`:35-39`): server sends constant (0,0,0); spec text never updated to reflect the intentional information-hiding.
- **Spec lag** — the spec mandates `MovePlayer`/`RotatePlayer` hub methods (`:55-88`) with no mention of `ExecuteTool`; the implementation has deprecated exactly the methods the spec requires. Removing the obsolete methods (as their attribute promises) would violate the spec as written — the spec needs to be updated to bless `ExecuteTool` as the canonical path.
- **Untested** — "perception update SHALL arrive within 100ms" (`:119-123`): no test or enforcement.

## Test coverage

- `ClientServerCommunicationTests` (24 tests) exercise session math, perception bounds, FOV inclusion/exclusion, and DTO mapping — but as **direct object tests, no SignalR transport**; the single round-trip test asserts only PlayerLocation + Visuals count (misses AmbientTint loss, inventory, affordances, enum encoding).
- `GameHubTests` (despite the name) never instantiates `GameHub` — it exercises grains. `GameHubSmokeTests` does one real connect/disconnect via `WebApplicationFactory` (`DISABLE_ORLEANS=1`) asserting connection state only.
- `GameSessionManagerTests` (20 tests) cover CRUD thoroughly but with no concurrency tests.
- **Gaps**: no test invokes `ExecuteTool` through the hub; no obsolete-vs-tool parity test (would catch the move-distance drift); no connect/disconnect lifecycle assertions; no reconnect test; no auth tests (Admin policy, ApiKeyMiddleware, the B2C-unconfigured 500); no range-check exploit tests; no payload-size or latency test.
