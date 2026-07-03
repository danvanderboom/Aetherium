# Aetherium Recommendations Register

*Date: 2026-07-03. A consolidated, de-duplicated, prioritized list of the actionable findings from the ten subsystem audits. Each item links to its source audit for `file:line` evidence. Severity reflects impact if the affected path is exercised; **Effort** is a rough order of magnitude (S ≈ hours, M ≈ a day or two, L ≈ a week+). "Quick win" = high value / low effort.*

Priority bands:
- **P0 — Correctness & security**: wrong behavior, exploits, or data-loss on paths that are (or should be) live.
- **P1 — Consistency & debt**: drift, dead code, and half-wired features that mislead or will bite soon.
- **P2 — Test & CI foundation**: the safety net that would have caught most P0/P1 items.
- **P3 — Feature completion**: finishing the scaffolded-but-unwired subsystems.

See [IMPROVEMENT_PLAN.md](IMPROVEMENT_PLAN.md) for the sequenced execution plan and [DESIGN_ANALYSIS.md](DESIGN_ANALYSIS.md) for the strategic framing.

> **Reconciliation status (`develop` @ 2026-07-03).** Since this register was written (baseline `5b7e267`), work on `develop` closed several items. **🟢 Done / superseded:** P1-19 (Aetherctl Orleans pin → 9.2.1), P0-6 (ExecuteTool hub auth now enforced), the console half of P0-11 (reconnect soft-lock fixed in `GameClient`), P3-3 (multi-world travel works end-to-end), the observer half of P3-1 (co-op perception on grain-hosted maps), most of P3-8 (durable persistence via snapshot+delta+SQLite), the `WorldTickService` half of P1-12 (tick pipeline now driven), and the Unity-setup half of P2-10 (project imports/tests). **⬜ Still open (verified on develop):** P0-0 (**server still hangs at boot** — only the `IGrainFactory` self-ref was removed), P0-1 (movement validation — *now unvalidated in the new grain path too*), P0-3 (map-wide door/use), P0-4 (`metaStore` still unregistered), P0-5 (`Memory` discards), P0-7 (anonymous REST), P0-8 (infrared black), P1-1 (Dashboard build), P1-2 (runtime/`global.json`), P1-3 (Aetherctl in `.sln`), P1-5 (server generator registry), P1-7 (prefab loading), P2-1 (**no CI** — why all of the above stayed hidden). Items below are tagged inline: 🟢 done · 🟡 partial · ⬜ open.

---

## P0 — Correctness & security

| # | Item | Sev | Effort | Source |
|---|---|---|---|---|
| P0-0 ⬜ | **Delete the self-referential co-hosting DI bridge** (`Program.cs:276-348`) — **the server cannot boot with Orleans enabled** (verified by live boot test on develop: still hangs before the startup banner; boots fine with `DISABLE_ORLEANS=1`). Only the `IGrainFactory` self-ref was removed so far; `IClusterClient`/`IWorldHost` still self-resolve. Co-hosted grains already share the host container, so the bridge is unnecessary; also reconcile `launchSettings.json` URLs (50309/50310) with the documented 5000. | Critical | S (quick win) | [orleans](orleans-and-hosting.md) |
| P0-1 ⬜ | **Validate player movement server-side** — `GameSession.MoveView`/`ChangeLevel` apply deltas with no wall/passability/distance/level checks. *On develop this got broader:* the new `GameMapGrain.MoveAsync` (grain path) is **also** unvalidated and silently ignores `MoveTool`'s 1–100 clamp. Consolidate onto a single validated path and delete the bypasses. | Critical | M | [protocol](client-server-protocol.md), [simulation](simulation-core.md) |
| P0-2 🟢 | **Fix `World.TryMove` West/East axis swap** — ~~West/East use North/South deltas~~ **Done on develop** (`World.cs:287/289` now correct); note `TryMove` is still dead code, so this is a landmine removed rather than a behavior change. | Critical | S (quick win) | [simulation](simulation-core.md) |
| P0-3 ⬜ | **Range-check door/use interactions** — `ToggleDoor` and `TryUseWithMode(usageId)` act on any entity by ID map-wide; the new grain `ToggleDoorAsync`/`UseAsync` overloads inherited the gap. Add adjacency/context checks (including Z for `TryActivate`). | High | S | [protocol](client-server-protocol.md), [simulation](simulation-core.md) |
| P0-4 ⬜ | **Register `metaStore` (and audit all `[PersistentState]` store names)** — `MetaProgressionGrain` throws on every production activation; still unregistered on develop. (The related `ORLEANS_STORAGE≠memory` silent-no-storage case *was* fixed via `ResolveStorageConfiguration`.) | Critical | S (quick win) | [narrative](narrative-and-multiworld.md), [orleans](orleans-and-hosting.md) |
| P0-5 ⬜ | **Fix `Memory.AddMemory`** — writes to a discarded list copy; the entire NPC/agent memory feature is inert. Unchanged on develop. | Critical | S (quick win) | [simulation](simulation-core.md) |
| P0-6 🟢 | **Centralize tool authorization at the hub** — ~~`GameHub.ExecuteTool` never calls `IsToolAllowed`~~ **Done on develop**: the hub now checks `AgentToolProfile.Player.IsToolAllowed(tool)` before executing. | High | S | [agents](agents-and-tools.md), [protocol](client-server-protocol.md) |
| P0-7 ⬜ | **Add auth to the REST control-plane** — cluster economy, meta-progression, adaptation-reload, benchmark endpoints are anonymous; require the existing API key (or `[Authorize]`) on mutating controllers. Unchanged on develop. | High | M | [protocol](client-server-protocol.md) |
| P0-8 ⬜ | **Fix infrared rendering** — heat level is dropped in DTO conversion (`LightLevel=0`), so infrared shows a black map; carry heat through or map it to a visible channel. Unchanged on develop (heat is now grain-authoritative but still collapsed to a `ThingsSeen` count in the DTO). | High | S | [perception](perception-fov-lighting.md) |
| P0-9 ⬜ | **Close inventory-capacity exploit** — `TryEquip` adds capacity every call with no equipped-state tracking; track equipped items. | High | S | [simulation](simulation-core.md) |
| P0-10 ⬜ | **Harden `RemoveEntity`/`MoveEntity`** — `RemoveEntity` throws `KeyNotFoundException` before its null-check; concurrent pickup dupes items; `MoveEntity` can drop entities from the location index. Now exercised by the grain path too. Guard and make atomic. | High | M | [simulation](simulation-core.md) |
| P0-11 🟢 | **Fix the console reconnect soft-lock** — ~~after a full reconnect, input is ignored forever~~ **Done on develop**: `GameClient` re-raises `Connected` on the auto-`Reconnected` path. (Minor: the loop still doesn't re-render a "Reconnecting…" state during downtime.) | High | S | [console](console-client.md) |
| P0-12 🟡 | **Cross-path state races** — `GameManagementGrain` mutates the same `GameSession`/`World` from silo threads concurrently with hub calls. On develop, *grain-bound* sessions are now serialized by Orleans single-threading; legacy sessions remain racy. Route all mutation through one owner. | Medium | M | [protocol](client-server-protocol.md), [simulation](simulation-core.md) |

## P1 — Consistency & technical debt

| # | Item | Sev | Effort | Source |
|---|---|---|---|---|
| P1-1 | **Fix the Dashboard build (2 root causes)** — rename `BehaviorAnalysis.razor` (or alias the server type) to kill the type-shadowing collision; delete `OrleansClientConnectionService` and rely on the existing `UseOrleansClient`. Restores full-solution compile. | High | S (quick win) | [unity+dashboard](unity-and-dashboard.md), [agents](agents-and-tools.md) |
| P1-2 | **Resolve the .NET-9 runtime mismatch** — add a `global.json` and/or `RollForward` so dev scripts run on machines with only newer runtimes. | High | S (quick win) | [tooling](tooling-testing-devex.md) |
| P1-3 | **Add Aetherctl to `Aetherium.sln`** — currently builds only transitively; invisible in IDEs and to a solution build. | Medium | S (quick win) | [tooling](tooling-testing-devex.md) |
| P1-4 ⬜ | **Converge the action paths** — *now three:* the 13 `[Obsolete]` hub methods, `LocalMutationGateway`, and the new `GrainMutationGateway`. Retire toward the tool + gateway path, ensure narrative events fire and `distance`/validation are honored consistently, and update `client-server-communication/spec.md`. (Drift got worse on develop, not better.) | Medium | M | [protocol](client-server-protocol.md), [agents](agents-and-tools.md) |
| P1-5 | **Populate the server's `MapGeneratorRegistry`** — call `DiscoverTypes` at startup so the game server honors the requested generator instead of always falling back to `AdvancedDungeonGenerator`. | High | S (quick win) | [worldgen](worldgen-and-pcg.md) |
| P1-6 | **Unify the worldgen pass lists** — the server runs a shorter list than the CLI/dashboard, producing emptier worlds; share one pass-list builder. | High | M | [worldgen](worldgen-and-pcg.md) |
| P1-7 | **Wire prefab loading** — call the already-implemented `PrefabLibrary.LoadFromDirectory` (the `Program.cs:207` TODO is stale); `Data/Prefabs/*.json` are currently unreachable. | Medium | S (quick win) | [worldgen](worldgen-and-pcg.md) |
| P1-8 | **Delete dead legacy client code** — `ConsoleDungeonGame`, `ClientConsoleDungeonGame`, and their duplicate engine (~6k lines / ~45 files) are unreferenced; removing them halves the console project and eliminates a duplicate-fix burden. Keep the shared base classes. | Medium | M | [console](console-client.md) |
| P1-9 | **Fix the dead console input block** — a lost `case` label after a `break;` makes Shift+M (next track) and M (compass mode) unreachable though the help panel advertises them. | Medium | S (quick win) | [console](console-client.md) |
| P1-10 | **Synchronize console rendering** — perception-thread and input-thread both render with no lock, causing torn frames; serialize rendering. | Medium | S | [console](console-client.md) |
| P1-11 | **Render `StatusMessage` and fix inventory markup** — `GameViewState.StatusMessage` is never drawn (all feedback invisible); inventory labels are fed raw into Spectre `Markup` (injection/crash). | Medium | S | [console](console-client.md) |
| P1-12 🟡 | **Decide `WorldTickService`'s fate** — **infrastructure done on develop**: it's now a real hosted service that enumerates worlds and drives `IWorldGrain.TickAsync` (weather/season/region ticks run). Remaining: NPC behavior still doesn't tick (`Monster.Heartbeat` never invoked), so monsters are static. | Medium | M | [simulation](simulation-core.md), [orleans](orleans-and-hosting.md) |
| P1-13 | **Make `WorldClock`/`WeatherSystem`/`EventScheduler` thread-safe** — unsynchronized singletons mutated from concurrent region ticks; also `WorldClock` reads corrupt `Tick` elapsed time, and `WeatherSystem` transition is per-call not per-time. | Medium | M | [simulation](simulation-core.md) |
| P1-14 | **Remove/implement `NotImplementedException` & stub repos** — audio persistence repos throw; `SpawnEntityTool`/`ModifyEntityTool` are hard stubs; `ScheduleTransportTool` fabricates its route. Either implement or return honest "not supported". | Medium | M | [agents](agents-and-tools.md), [console](console-client.md) |
| P1-15 | **Cap unbounded growth** — telemetry snapshots, replay JSON, generated-quest lists, lockout ledgers, invite lists grow without limit; add caps/eviction/persistence. | Medium | M | [agents](agents-and-tools.md), [narrative](narrative-and-multiworld.md) |
| P1-16 | **Fix the A/B worldgen seed bug** — all auto-seeded candidates share one seed, so the comparison compares identical maps. | Medium | S (quick win) | [worldgen](worldgen-and-pcg.md) |
| P1-17 | **Deterministic narrative seeds** — replace `string.GetHashCode()` (randomized per process) with a stable hash. | Medium | S (quick win) | [narrative](narrative-and-multiworld.md) |
| P1-18 | **Copy `Prompts/*.md` to output** — `PromptRegistry` loads from an empty bin dir at runtime; add a `CopyToOutputDirectory` item. | Low | S (quick win) | [agents](agents-and-tools.md) |
| P1-19 🟢 | **Align Aetherctl's Orleans package pin** — **Done on develop**: now references `Microsoft.Orleans.Client 9.2.1`, matching the server. | Low | S (quick win) | [tooling](tooling-testing-devex.md) |
| P1-20 | **Reconcile OpenSpec `tasks.md` with reality** — several changes are all-checked but broken (Dashboard, travel) or all-unchecked but done (Unity, worldbuilding tools); update to reflect verified state and split implemented/planned. | Medium | M | all audits |
| P1-21 | **Fix script bugs** — `monitor-lite.ps1` decodes the wrong byte count (frames never parse); `start-llm-agents.ps1` references the nonexistent `agentcli`; Ctrl+C handlers use `$using:` incorrectly and track wrapper PIDs. | Low | S | [tooling](tooling-testing-devex.md) |
| P1-22 | **Enforce or drop `MapValidator` at runtime** — fully implemented and tested but never called on live session worlds; wire it into world construction. Also fix the "Argentinaable" typo and mojibake comments. | Low | S | [worldgen](worldgen-and-pcg.md) |

## P2 — Test & CI foundation

| # | Item | Sev | Effort | Source |
|---|---|---|---|---|
| P2-1 ⬜ | **Stand up CI** — a `dotnet build Aetherium.sln && dotnet test` workflow on `main` (the current one targets a nonexistent `master` and runs no tests). **Still open on develop — the single highest-leverage change**; it is why the boot hang and Dashboard break persist unnoticed. | High | S (quick win) | [tooling](tooling-testing-devex.md) |
| P2-2 🟡 | **Integration tests across grain boundaries** — **substantially advanced on develop**: new `Aetherium.Test/{Persistence,MultiWorld}/*` suites cover snapshot recovery, delta replay, join, and shared mutation. Remaining: a full connect→act→observe→travel→quest→instance path (much still can't complete — see P3-2/P3-4). | High | L | [narrative](narrative-and-multiworld.md), [protocol](client-server-protocol.md) |
| P2-3 | **Cover the real player-movement path** — `MoveView`/`ChangeLevel` (and West/East) have zero tests; add them (would have caught P0-1, P0-2). | High | M | [simulation](simulation-core.md) |
| P2-4 | **FOV rotation-invariance regression test** — nothing asserts the visible set is stable across rotation; add one so the fixed bug can't silently return. | Medium | S | [perception](perception-fov-lighting.md) |
| P2-5 | **Real CLI tests** — the 31 Aetherctl tests invoke no handler; add tests that actually run commands and assert output/exit codes; cover `Auth/SignalR/Orleans/Config`. | Medium | M | [tooling](tooling-testing-devex.md) |
| P2-6 | **Client-side unit tests** — create the long-promised project (or reuse the extern-alias approach) to test `GameClient` reconnection, renderer, widgets, monitor; delete the stale `CLIENT_TESTS_README` templates. | Medium | M | [console](console-client.md), [tooling](tooling-testing-devex.md) |
| P2-7 | **Speed up the suite** — 16 un-shared Orleans TestClusters drive the 6-minute run; share clusters per collection for same-config fixtures. | Low | M | [tooling](tooling-testing-devex.md) |
| P2-8 | **Un-skip / delete the 2 stale skipped tests** — `PromptRegistryGrainTests` would likely pass now; `LightingRenderingTests` is a commented-out corpse. | Low | S (quick win) | [tooling](tooling-testing-devex.md) |
| P2-9 🟢 | **Byte/tile-level determinism test for PCG** — **Done on develop**: `Aetherium.Test/WorldGen/DeterminismTests.cs` now hashes the full world (same seed → same hash) and verifies `EffectiveSeed` replay. | Low | M | [worldgen](worldgen-and-pcg.md) |
| P2-10 🟢 | **Fix the Unity project so it imports & tests** — **Done on develop** (Unity 6 migration: `Packages/manifest.json`, asmdefs in subfolders, `ProjectSettings`, `Main.unity`, PlayMode tests compile). Remaining is content/behavior (live-mode client, tile sprites), tracked under P3. | Medium | M | [unity+dashboard](unity-and-dashboard.md) |

## P3 — Feature completion (scaffolded but unwired)

Each of these is a subsystem the docs present as built but which does not function end-to-end. Complete or explicitly re-scope. Most warrant an OpenSpec change (or reconciliation of an existing one) before work starts.

| # | Item | Sev | Effort | Source |
|---|---|---|---|---|
| P3-1 🟡 | **Push perception to observers** — **Done for grain-hosted maps on develop** (delta fan-out + per-session FOV recompute; co-located players see each other move). Remaining: legacy single-world sessions still get no observer updates, and NPC movement still doesn't emit (NPCs are static). | High | M | [perception](perception-fov-lighting.md) |
| P3-2 🟡 | **Wire the narrative consequence engine into gameplay** — **partially done**: grain-bound sessions now have `WorldId` set (so the engine runs), and `door_*`/`player_arrived` reach it. Remaining: `item_collected`/`item_used` still not emitted; and **quest activation still missing** (`ActiveQuestIds` never populated, so `travel_to`/objective completion is impossible). | High | L | [narrative](narrative-and-multiworld.md) |
| P3-3 🟢 | **Make multi-world travel work end-to-end** — **Done on develop**: `JoinWorld` binds a session to a grain world (snapshot hydration + gateway swap) and `UsePortal` resolves a target, transports the player, and emits `player_arrived`. (Follow-ups: ACL still unenforced at join; world metadata/tags still unpopulated so tag-portals fall back to first-world; join/fan-out races noted.) | High | L | [narrative](narrative-and-multiworld.md) |
| P3-4 | **Wire the instances/parties/raids stack** — add a controller/hub/tool/client entry point and lockout record-on-reuse; add a cleanup sweeper for abandoned instances (currently leaked and ticked forever). | Medium | L | [narrative](narrative-and-multiworld.md) |
| P3-5 | **Finish agent↔game integration** — connect `AgentGrain` to live sessions (or standardize on `AgentRunnerGrain`), move the runner loop onto grain timers, feed prompt templates into decisions. | High | L | [agents](agents-and-tools.md) |
| P3-6 | **Make PCG placement real** — implement NPC/item/prefab-entity placement (currently console-log stubs) and let generator parameters actually affect output (difficulty system is decorative). | High | L | [worldgen](worldgen-and-pcg.md) |
| P3-7 | **Implement a combat system, or remove combat scaffolding** — `IsInCombat()` is `if(false)`; no attack action exists; `combat.md`, `combat-survival.json`, and analytics reference combat that can't happen. | Medium | L | [simulation](simulation-core.md) |
| P3-8 🟢 | **Durable persistence** — **Largely done on develop**: `GameMapGrain` snapshots the `World` and restores it via `SnapshotWorldBuilder` + `MapDeltaReplayer`; a SQLite grain-storage/snapshot store, periodic + threshold compaction, and delta-version guards landed with solid tests. Follow-ups: heat state not persisted (cold-start loses trails); `PersistDeltaAsync` swallows failures; SQLite writer-concurrency and delta-GC under-tested; `metaStore` still unregistered so meta-progression still can't persist. | High | L | [orleans](orleans-and-hosting.md), [narrative](narrative-and-multiworld.md) |
| P3-9 | **Complete or scope down audio** — ship assets (none exist; the null-fallback path is dead), implement reverb/panning or state them as unsupported. | Low | M | [console](console-client.md) |
| P3-10 | **Complete the Dashboard** — after P1-1, finish the stub pages (BenchmarkComparison, CurriculumProgress, ReplayViewer) and move shared contracts to `Aetherium.Model` so it stops referencing all of `Aetherium.Server`. | Low | M | [unity+dashboard](unity-and-dashboard.md) |

---

## Quick-wins shortlist (high value, ≤ a few hours each)

Still open on `develop` (the recommended first sprint): **P0-0 (boot-blocking DI bridge)** · P0-4 (metaStore) · P0-5 (Memory) · P1-1 (Dashboard build) · P1-2 (runtime mismatch) · P1-3 (Aetherctl in .sln) · P1-5 (generator registry) · P1-7 (prefab loading) · P1-9 (dead input block) · P1-16 (A/B seed) · P1-17 (narrative seed) · P1-18 (prompt copy) · **P2-1 (CI)** · P2-8 (stale skips).

Already landed on `develop`: 🟢 P0-2 (axis swap) · P0-6 (hub auth) · P0-11 (reconnect) · P1-19 (Orleans pin) · P2-9 (determinism test) · P2-10 (Unity imports).

Knocking out the still-open shortlist makes the server bootable with Orleans, restores a green full-solution build, a working CI gate, runnable dev scripts, and fixes several silent correctness bugs — a strong first sprint that also makes every subsequent change safer to verify. **P0-0 and P2-1 are the two that matter most**: without the boot fix nothing multiplayer/persistence that landed on develop is reachable in production, and without CI the still-broken build and boot will keep regressing silently.
