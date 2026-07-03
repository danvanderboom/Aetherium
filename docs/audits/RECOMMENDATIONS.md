# Aetherium Recommendations Register

*Date: 2026-07-03. A consolidated, de-duplicated, prioritized list of the actionable findings from the ten subsystem audits. Each item links to its source audit for `file:line` evidence. Severity reflects impact if the affected path is exercised; **Effort** is a rough order of magnitude (S ≈ hours, M ≈ a day or two, L ≈ a week+). "Quick win" = high value / low effort.*

Priority bands:
- **P0 — Correctness & security**: wrong behavior, exploits, or data-loss on paths that are (or should be) live.
- **P1 — Consistency & debt**: drift, dead code, and half-wired features that mislead or will bite soon.
- **P2 — Test & CI foundation**: the safety net that would have caught most P0/P1 items.
- **P3 — Feature completion**: finishing the scaffolded-but-unwired subsystems.

See [IMPROVEMENT_PLAN.md](IMPROVEMENT_PLAN.md) for the sequenced execution plan and [DESIGN_ANALYSIS.md](DESIGN_ANALYSIS.md) for the strategic framing.

---

## P0 — Correctness & security

| # | Item | Sev | Effort | Source |
|---|---|---|---|---|
| P0-0 | **Delete the self-referential co-hosting DI bridge** (`Program.cs:276-348`) — **the server cannot boot with Orleans enabled** (verified by live boot test: hangs before the startup banner; boots fine with `DISABLE_ORLEANS=1`). Co-hosted grains already share the host container, so the bridge is unnecessary; also reconcile `launchSettings.json` URLs (50309/50310) with the documented 5000. | Critical | S (quick win) | [orleans](orleans-and-hosting.md) |
| P0-1 | **Validate player movement server-side** — `GameSession.MoveView`/`ChangeLevel` apply deltas with no wall/passability/distance/level checks; consolidate onto a single validated path and delete the bypass. | Critical | M | [protocol](client-server-protocol.md), [simulation](simulation-core.md) |
| P0-2 | **Fix `World.TryMove` West/East axis swap** — West/East use North/South deltas (`World.cs:280-283`). Fix before P0-1 makes `TryMove` live. | Critical | S (quick win) | [simulation](simulation-core.md) |
| P0-3 | **Range-check door/use interactions** — `ToggleDoor` and `TryUseWithMode(usageId)` act on any entity by ID map-wide; add adjacency/context checks (including Z for `TryActivate`). | High | S | [protocol](client-server-protocol.md), [simulation](simulation-core.md) |
| P0-4 | **Register `metaStore` (and audit all `[PersistentState]` store names)** — `MetaProgressionGrain` throws on every production activation; verify every referenced store is registered in `Program.cs`. | Critical | S (quick win) | [narrative](narrative-and-multiworld.md) |
| P0-5 | **Fix `Memory.AddMemory`** — writes to a discarded list copy; the entire NPC/agent memory feature is inert. | Critical | S (quick win) | [simulation](simulation-core.md) |
| P0-6 | **Centralize tool authorization at the hub** — `GameHub.ExecuteTool` never calls `AgentToolProfile.IsToolAllowed`; add the check (as `GameManagementGrain` already does) so a single forgetful tool can't be player-executed. | High | S | [agents](agents-and-tools.md), [protocol](client-server-protocol.md) |
| P0-7 | **Add auth to the REST control-plane** — cluster economy, meta-progression, adaptation-reload, benchmark endpoints are anonymous; require the existing API key (or `[Authorize]`) on mutating controllers. | High | M | [protocol](client-server-protocol.md) |
| P0-8 | **Fix infrared rendering** — heat level is dropped in DTO conversion (`LightLevel=0`), so infrared shows a black map; carry heat through or map it to a visible channel. | High | S | [perception](perception-fov-lighting.md) |
| P0-9 | **Close inventory-capacity exploit** — `TryEquip` adds capacity every call with no equipped-state tracking; track equipped items. | High | S | [simulation](simulation-core.md) |
| P0-10 | **Harden `RemoveEntity`/`MoveEntity`** — `RemoveEntity` throws `KeyNotFoundException` before its null-check; concurrent pickup dupes items; `MoveEntity` can drop entities from the location index. Guard and make atomic. | High | M | [simulation](simulation-core.md) |
| P0-11 | **Fix the console reconnect soft-lock** — after a full reconnect, the `Closed` handler restarts but never re-raises `Connected`, so input is ignored forever and the UI never shows "Disconnected". | High | S | [console](console-client.md) |
| P0-12 | **Serialize the caller sequence issue: cross-path state races** — `GameManagementGrain` mutates the same `GameSession`/`World` from silo threads concurrently with hub calls; add synchronization or route all mutation through one owner. | Medium | M | [protocol](client-server-protocol.md), [simulation](simulation-core.md) |

## P1 — Consistency & technical debt

| # | Item | Sev | Effort | Source |
|---|---|---|---|---|
| P1-1 | **Fix the Dashboard build (2 root causes)** — rename `BehaviorAnalysis.razor` (or alias the server type) to kill the type-shadowing collision; delete `OrleansClientConnectionService` and rely on the existing `UseOrleansClient`. Restores full-solution compile. | High | S (quick win) | [unity+dashboard](unity-and-dashboard.md), [agents](agents-and-tools.md) |
| P1-2 | **Resolve the .NET-9 runtime mismatch** — add a `global.json` and/or `RollForward` so dev scripts run on machines with only newer runtimes. | High | S (quick win) | [tooling](tooling-testing-devex.md) |
| P1-3 | **Add Aetherctl to `Aetherium.sln`** — currently builds only transitively; invisible in IDEs and to a solution build. | Medium | S (quick win) | [tooling](tooling-testing-devex.md) |
| P1-4 | **Converge the two action paths** — retire the 13 `[Obsolete]` hub methods in favor of `ExecuteTool`, after making `ExecuteTool` emit the item narrative events the obsolete path did and honoring `distance`. Update `client-server-communication/spec.md` to bless `ExecuteTool`. | Medium | M | [protocol](client-server-protocol.md), [agents](agents-and-tools.md) |
| P1-5 | **Populate the server's `MapGeneratorRegistry`** — call `DiscoverTypes` at startup so the game server honors the requested generator instead of always falling back to `AdvancedDungeonGenerator`. | High | S (quick win) | [worldgen](worldgen-and-pcg.md) |
| P1-6 | **Unify the worldgen pass lists** — the server runs a shorter list than the CLI/dashboard, producing emptier worlds; share one pass-list builder. | High | M | [worldgen](worldgen-and-pcg.md) |
| P1-7 | **Wire prefab loading** — call the already-implemented `PrefabLibrary.LoadFromDirectory` (the `Program.cs:207` TODO is stale); `Data/Prefabs/*.json` are currently unreachable. | Medium | S (quick win) | [worldgen](worldgen-and-pcg.md) |
| P1-8 | **Delete dead legacy client code** — `ConsoleDungeonGame`, `ClientConsoleDungeonGame`, and their duplicate engine (~6k lines / ~45 files) are unreferenced; removing them halves the console project and eliminates a duplicate-fix burden. Keep the shared base classes. | Medium | M | [console](console-client.md) |
| P1-9 | **Fix the dead console input block** — a lost `case` label after a `break;` makes Shift+M (next track) and M (compass mode) unreachable though the help panel advertises them. | Medium | S (quick win) | [console](console-client.md) |
| P1-10 | **Synchronize console rendering** — perception-thread and input-thread both render with no lock, causing torn frames; serialize rendering. | Medium | S | [console](console-client.md) |
| P1-11 | **Render `StatusMessage` and fix inventory markup** — `GameViewState.StatusMessage` is never drawn (all feedback invisible); inventory labels are fed raw into Spectre `Markup` (injection/crash). | Medium | S | [console](console-client.md) |
| P1-12 | **Decide `WorldTickService`'s fate** — registered but never started; either start it with a world registry (enables NPC/AI ticking) or delete it and document manual-tick-only. | Medium | M | [simulation](simulation-core.md), orleans-and-hosting |
| P1-13 | **Make `WorldClock`/`WeatherSystem`/`EventScheduler` thread-safe** — unsynchronized singletons mutated from concurrent region ticks; also `WorldClock` reads corrupt `Tick` elapsed time, and `WeatherSystem` transition is per-call not per-time. | Medium | M | [simulation](simulation-core.md) |
| P1-14 | **Remove/implement `NotImplementedException` & stub repos** — audio persistence repos throw; `SpawnEntityTool`/`ModifyEntityTool` are hard stubs; `ScheduleTransportTool` fabricates its route. Either implement or return honest "not supported". | Medium | M | [agents](agents-and-tools.md), [console](console-client.md) |
| P1-15 | **Cap unbounded growth** — telemetry snapshots, replay JSON, generated-quest lists, lockout ledgers, invite lists grow without limit; add caps/eviction/persistence. | Medium | M | [agents](agents-and-tools.md), [narrative](narrative-and-multiworld.md) |
| P1-16 | **Fix the A/B worldgen seed bug** — all auto-seeded candidates share one seed, so the comparison compares identical maps. | Medium | S (quick win) | [worldgen](worldgen-and-pcg.md) |
| P1-17 | **Deterministic narrative seeds** — replace `string.GetHashCode()` (randomized per process) with a stable hash. | Medium | S (quick win) | [narrative](narrative-and-multiworld.md) |
| P1-18 | **Copy `Prompts/*.md` to output** — `PromptRegistry` loads from an empty bin dir at runtime; add a `CopyToOutputDirectory` item. | Low | S (quick win) | [agents](agents-and-tools.md) |
| P1-19 | **Align Aetherctl's Orleans package pin** — drop the dead `Microsoft.Orleans.Client 8.0.0` pin (uplifted to 9.2.1 transitively) to remove misleading version skew and warnings. | Low | S (quick win) | [tooling](tooling-testing-devex.md) |
| P1-20 | **Reconcile OpenSpec `tasks.md` with reality** — several changes are all-checked but broken (Dashboard, travel) or all-unchecked but done (Unity, worldbuilding tools); update to reflect verified state and split implemented/planned. | Medium | M | all audits |
| P1-21 | **Fix script bugs** — `monitor-lite.ps1` decodes the wrong byte count (frames never parse); `start-llm-agents.ps1` references the nonexistent `agentcli`; Ctrl+C handlers use `$using:` incorrectly and track wrapper PIDs. | Low | S | [tooling](tooling-testing-devex.md) |
| P1-22 | **Enforce or drop `MapValidator` at runtime** — fully implemented and tested but never called on live session worlds; wire it into world construction. Also fix the "Argentinaable" typo and mojibake comments. | Low | S | [worldgen](worldgen-and-pcg.md) |

## P2 — Test & CI foundation

| # | Item | Sev | Effort | Source |
|---|---|---|---|---|
| P2-1 | **Stand up CI** — a `dotnet build Aetherium.sln && dotnet test` workflow on `main` (the current one targets a nonexistent `master` and runs no tests). Single highest-leverage change; would have caught the Dashboard break and runtime mismatch. | High | S (quick win) | [tooling](tooling-testing-devex.md) |
| P2-2 | **Integration tests across grain boundaries** — connect → act → observe perception; travel between worlds; complete a cross-world quest; enter an instance. Every broken seam is at an untested bridge. | High | L | [narrative](narrative-and-multiworld.md), [protocol](client-server-protocol.md) |
| P2-3 | **Cover the real player-movement path** — `MoveView`/`ChangeLevel` (and West/East) have zero tests; add them (would have caught P0-1, P0-2). | High | M | [simulation](simulation-core.md) |
| P2-4 | **FOV rotation-invariance regression test** — nothing asserts the visible set is stable across rotation; add one so the fixed bug can't silently return. | Medium | S | [perception](perception-fov-lighting.md) |
| P2-5 | **Real CLI tests** — the 31 Aetherctl tests invoke no handler; add tests that actually run commands and assert output/exit codes; cover `Auth/SignalR/Orleans/Config`. | Medium | M | [tooling](tooling-testing-devex.md) |
| P2-6 | **Client-side unit tests** — create the long-promised project (or reuse the extern-alias approach) to test `GameClient` reconnection, renderer, widgets, monitor; delete the stale `CLIENT_TESTS_README` templates. | Medium | M | [console](console-client.md), [tooling](tooling-testing-devex.md) |
| P2-7 | **Speed up the suite** — 16 un-shared Orleans TestClusters drive the 6-minute run; share clusters per collection for same-config fixtures. | Low | M | [tooling](tooling-testing-devex.md) |
| P2-8 | **Un-skip / delete the 2 stale skipped tests** — `PromptRegistryGrainTests` would likely pass now; `LightingRenderingTests` is a commented-out corpse. | Low | S (quick win) | [tooling](tooling-testing-devex.md) |
| P2-9 | **Byte/tile-level determinism test for PCG** — the "determinism" test compares 4 metrics; add a golden/tile-level comparison per the pcg-core requirement. | Low | M | [worldgen](worldgen-and-pcg.md) |
| P2-10 | **Fix the Unity project so it imports & tests** — add `Packages/manifest.json` + `ProjectSettings`, split the asmdefs, fix test references and private-field access, commit `Main.unity` (or rebase onto `develop`, which fixed most of this). | Medium | M | [unity+dashboard](unity-and-dashboard.md) |

## P3 — Feature completion (scaffolded but unwired)

Each of these is a subsystem the docs present as built but which does not function end-to-end. Complete or explicitly re-scope. Most warrant an OpenSpec change (or reconciliation of an existing one) before work starts.

| # | Item | Sev | Effort | Source |
|---|---|---|---|---|
| P3-1 | **Push perception to observers** — only the acting player gets updates; other players/NPCs in a shared world never see each other move. Core to "multiplayer". | High | M | [perception](perception-fov-lighting.md) |
| P3-2 | **Wire the narrative consequence engine into gameplay** — set `session.WorldId` for real sessions; route WorldEvents from all systems (not just GameHub) into narrative; implement quest activation so `travel_to`/`item`/`defeat` objectives can complete. | High | L | [narrative](narrative-and-multiworld.md) |
| P3-3 | **Make multi-world travel work end-to-end** — break the `WorldId`↔`UsePortal` circular dependency, load the target world into the session, expose a client entry point, populate world metadata/tags. | High | L | [narrative](narrative-and-multiworld.md) |
| P3-4 | **Wire the instances/parties/raids stack** — add a controller/hub/tool/client entry point and lockout record-on-reuse; add a cleanup sweeper for abandoned instances (currently leaked and ticked forever). | Medium | L | [narrative](narrative-and-multiworld.md) |
| P3-5 | **Finish agent↔game integration** — connect `AgentGrain` to live sessions (or standardize on `AgentRunnerGrain`), move the runner loop onto grain timers, feed prompt templates into decisions. | High | L | [agents](agents-and-tools.md) |
| P3-6 | **Make PCG placement real** — implement NPC/item/prefab-entity placement (currently console-log stubs) and let generator parameters actually affect output (difficulty system is decorative). | High | L | [worldgen](worldgen-and-pcg.md) |
| P3-7 | **Implement a combat system, or remove combat scaffolding** — `IsInCombat()` is `if(false)`; no attack action exists; `combat.md`, `combat-survival.json`, and analytics reference combat that can't happen. | Medium | L | [simulation](simulation-core.md) |
| P3-8 | **Durable persistence** — serialize `World` in `GameMapGrain`, finish the Azure (or add a file/SQLite) grain-storage path, so world/meta/narrative state survives restart. | High | L | [worldgen](worldgen-and-pcg.md), [narrative](narrative-and-multiworld.md) |
| P3-9 | **Complete or scope down audio** — ship assets (none exist; the null-fallback path is dead), implement reverb/panning or state them as unsupported. | Low | M | [console](console-client.md) |
| P3-10 | **Complete the Dashboard** — after P1-1, finish the stub pages (BenchmarkComparison, CurriculumProgress, ReplayViewer) and move shared contracts to `Aetherium.Model` so it stops referencing all of `Aetherium.Server`. | Low | M | [unity+dashboard](unity-and-dashboard.md) |

---

## Quick-wins shortlist (high value, ≤ a few hours each)

**P0-0 (boot-blocking DI bridge)** · P0-2 (axis swap) · P0-4 (metaStore) · P0-5 (Memory) · P1-1 (Dashboard build) · P1-2 (runtime mismatch) · P1-3 (Aetherctl in .sln) · P1-5 (generator registry) · P1-7 (prefab loading) · P1-9 (dead input block) · P1-16 (A/B seed) · P1-17 (narrative seed) · P1-18 (prompt copy) · P1-19 (Orleans pin) · P2-1 (CI) · P2-8 (stale skips).

Knocking out this shortlist makes the server bootable with Orleans, restores a green full-solution build, a working CI gate, runnable dev scripts, and fixes several silent correctness bugs — a strong first sprint that also makes every subsequent change safer to verify.
