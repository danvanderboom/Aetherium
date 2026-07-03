# Audit: Agents & Tool System

*Audit date: 2026-07-03 · Scope: `Aetherium.Server/Agents` (~50 files: AgentGrain, AgentRunnerGrain, Tools, AgentToolRegistry/Profile, MicrosoftAgentAdapter, Telemetry, BehaviorAnalysis), `Prompts`, `scripts/start-llm-agents.ps1`, `docs/agents`. Findings marked **Verified** or **Suspected**.*

> **Reconciliation — `develop` @ 2026-07-03.** The multiplayer work touched the tool layer. **FIXED:** the High `GameHub.ExecuteTool` authorization bypass — the hub now checks `AgentToolProfile.Player.IsToolAllowed(tool)` before executing, so profile enforcement no longer relies solely on each tool's internal capability check. **IMPROVED/PARTIAL:** `ToolExecutionContext` gained a `Session` property that the hub path now populates (the grain-runner path may still lack full context); `MoveTool` now threads `distance` through to the mutation gateway and a wired `RotateTool` was added. **Also fixed (Phase 1, commit `cd6cf67`):** prompt templates now load at runtime — `Prompts/*.md` are copied to the output directory (`PromptRegistry` was reading an empty bin folder, so every agent prompt lookup returned null). **STANDS:** unbounded telemetry/replay growth, the `AgentRunnerGrain` off-scheduler loop, tool-ID collisions swallowed, and the doc/tool-count drift. The server-side `BehaviorAnalysis` model is unchanged (all four properties present) — confirming the Dashboard build break is a Razor-side collision, not model drift. Detail in the Reconciliation section at the end.

## Summary

The tool system is one of the stronger parts of the server: a clean reflection-discovered registry, two-layer capability enforcement, and a genuinely wired LLM decision loop (hand-rolled OpenAI-compatible adapter targeting LM Studio, with heuristic fallback). The audit **refuted** two of the harshest priors — agents *can* run a real decision loop (via `AgentRunnerGrain`, not the stub `AgentGrain`), and LLM calls *are* wired. The real problems are: a hub-level authorization bypass, unbounded telemetry/replay memory growth, an Orleans threading violation in the runner, prompt templates that never reach the runtime, and pervasive **documentation drift** (docs claim 26/31/17/13 tools; the truth is 23, several of them stubs).

| Severity | Count | Items |
|---|---|---|
| High | 2 | `GameHub.ExecuteTool` skips profile authorization; Dashboard `BehaviorAnalysis.razor` name-collision break |
| Medium | 6 | Unbounded telemetry/replay growth; runner violates Orleans threading; schemas never validated; prompts never loaded at runtime; tool-ID collisions swallowed; grain-path tools lack Session |
| Low | 6 | ScheduleTransport fabricates route; MoveTool drops distance; dashboard hub no auth; sync-over-async; fire-and-forget replay; … |

## High

**`GameHub.ExecuteTool` skips profile authorization entirely.** *Verified.* `GameHub.cs:682-732` fetches any registered tool by ID and executes it without calling `AgentToolProfile.IsToolAllowed` — the only defense is each tool's internal `HasCapability` check plus a hardcoded player-capability set duplicated from `AgentToolProfile.Player` (drift risk). Contrast `GameManagementGrain.ExecuteToolAsync` which *does* check `IsToolAllowed`. **No live exploit today** (world-editing capabilities aren't in the player set, and WorldBuilding tools additionally require `WorldBuildingToolContext`), but the profile layer is bypassed, so the first future tool that forgets its internal check — or checks a capability players happen to have — is immediately player-executable.

**Dashboard compile break is a Razor name collision, not a model change.** *Verified.* `BehaviorAnalysis.razor:205,220` declares `private BehaviorAnalysis? behaviorAnalysis` inside a component whose generated class is *also* named `BehaviorAnalysis`; the simple name binds to the component type (which lacks `ActionPatterns/StrugglePatterns/SuccessPatterns/ExplorationPatterns`). The server model still has all four properties (`BehaviorAnalyzer.cs:530-539`). Fix = fully-qualify `Aetherium.Server.Agents.Analysis.BehaviorAnalysis` or rename the page. (This is the root cause of 8 of the 11 build errors; see [unity-and-dashboard.md](unity-and-dashboard.md).)

## Medium (verified)

- **Unbounded in-memory telemetry/replay growth** — `AgentTelemetryGrain._snapshots` grows one entry per agent step forever, never trimmed/persisted; `ReplayStorage._replaysJson` (static) is never evicted (each blob holds full per-step perception JSON); `DeleteReplay` misses `_replaysJson`. Also silo-local static → broken in multi-silo.
- **`AgentRunnerGrain` violates Orleans single-threaded execution** — `RunAsync` spawns `Task.Run` calling `StepAsync()` on the grain instance from a thread-pool thread, mutating grain state outside the scheduler and racing `GetStatusAsync`/`DetachAsync`; the loop keeps running after deactivation. Should use grain timers/reminders.
- **Declared parameter schemas are never validated** — `ToolParameterSchema` is used only for LLM prompt text; no pre-Execute validation. Each tool hand-parses args; `CreateTradeRouteTool.cs:86-89` throws `KeyNotFoundException` on a missing arg *outside* its try — via the runner (no try/catch at the call) this kills the run loop.
- **Prompt templates never reach the runtime** — no `CopyToOutputDirectory` for `Prompts/*.md`, so `PromptRegistry` loads from an empty bin dir; and nothing consumes templates for decisions anyway (`MicrosoftAgentAdapter` hardcodes the system prompt; `AgentGrain.UpdatePromptAsync` is a stub). `combat.md`/`explorer.md` describe behaviors no tool supports.
- **Tool-ID collisions silently swallowed** — `AgentToolRegistry` overwrites on duplicate ID (last wins, no warning); `assembly.GetTypes()` is unguarded (a `ReflectionTypeLoadException` would crash DI singleton construction at startup).
- **Grain-path execution lacks Session** — the runner builds contexts with `ManagementGrain` but no `Session`, so `ChangeLevelTool`, `JumpToLocationTool`, and the vision tools' session branches return "No execution context available" (graceful, but LLM agents cannot change level or use several advertised tools).

## Low (verified)

`ScheduleTransportTool` fabricates its route (zero TravelTime → instant arrival, no capacity/resource validation); `MoveTool` ignores `distance` on the grain path (moves 1, reports success); `AgentDashboardHub` has no auth and re-sends a full analysis payload per step; sync-over-async in `WorldFeatureBuilder` (`.GetAwaiter().GetResult()`); `DetachAsync` fire-and-forgets the replay write; `JumpToLocationTool` direct-coordinate jump unimplemented.

## Verified leads (from the brief)

1. **Confirmed** — `AgentGrain.JoinGameAsync/LeaveGameAsync/UpdatePromptAsync` are TODO stubs; `AgentGrain` is a dead-end shell. But agents join games via `AgentRunnerGrain.AttachAsync` instead.
2. **Refuted** — `AgentRunnerGrain` runs a real step loop (`RunAsync`/`StepAsync`) with perception pull, LLM-or-heuristic policy, tool execution, telemetry, and failure replays.
3. **Refuted** — LLM calls *are* wired: `MicrosoftAgentAdapter` POSTs to `{OPENAI_API_BASE}/chat/completions` (default LM Studio, phi-4), parsing tool_calls or simple-JSON with a `move F` fallback; invoked from the runner (gated on `AGENT_LLM_ENABLED=1`). "Microsoft Agent Framework" is a misnomer — it's a hand-rolled REST adapter. `start-llm-agents.ps1` works against current code, but its printed instructions reference a nonexistent `agentcli` with `policy`/`debug` subcommands that don't exist.
4. **Partial** — of 5 MultiWorld tools, 4 do real grain work and 1 (`ScheduleTransport`) is semantically stubbed; `CreateTradeRouteTool` has a missing-arg crash bug. WorldBuilding `SpawnEntityTool`/`ModifyEntityTool` are hard stubs.
5. **Confirmed (fix good, weaker layer found)** — the JumpToLocation capability fix is present and correct at both the profile filter and the in-tool check; all 23 tools self-enforce capabilities. The weakness is the hub-level bypass (High finding), not the profile logic.

## Strengths

- Two-layer capability enforcement: all 23 tools self-check in `ExecuteAsync`, independent of profile filtering — the JumpToLocation bug class is closed at both layers.
- Clean tool abstraction (`IAgentTool` + `[AgentTool]` + reflection discovery) mirroring `MapGeneratorRegistry`, with DI-aware instantiation and per-instance caching.
- Dual LLM format support with graceful degradation (function calling for GPT models, prompt-injected schema for phi-4, `move F` fallback), rate limiting, concurrency caps.
- Telemetry pipeline (snapshot → analysis → SignalR broadcast → replay capture on 3+ consecutive failures) is wired end-to-end with analysis caching.
- Legacy SignalR methods are `[Obsolete]` and delegate to the unified tool path.
- Good unit hygiene where it exists: mock-HttpClient adapter tests; 14 profile tests including deny precedence.

## Docs & spec alignment (actual tool count: 23)

Movement 4, Interaction 5, Vision 4, WorldBuilding 5, MultiWorld 5 — of which `spawnentity`/`modifyentity` error out, `jumptolocation` coordinate mode is unimplemented, and `scheduletransport` is semantically incomplete. Doc claims are all wrong in different directions: `docs/agents/README.md` says "26+" and "13 World-Building tools"; `FINAL_SUMMARY.md` says "26 (13+13)"; `IMPLEMENTATION_SUMMARY.md` says "17"; `docs/architecture/server.md` says "31 … and compound tools." `TOOL_PROFILES.md` is the most accurate. `docs/agents/TOOLS.md` lists 18 (omits MultiWorld) and references the nonexistent `agentcli`. The `NarrativeDesigner` profile grants categories (map_generation/narrative/quest) for which **zero tools exist**.

OpenSpec: `add-worldbuilding-tool-integration/tasks.md` is stale in the *opposite* direction — all boxes unchecked though nearly everything shipped (except the genuinely-incomplete SpawnEntity/ModifyEntity, and feature-builders only run the tool path in tests). `add-agent-training-pcg/tasks.md` is all-checked but its "Integration & Fixes complete" claim is contradicted by the Dashboard build break.

## Test coverage

~171 tests touch the agent system (profile 14, registry 12, tool-system integration 29, telemetry/analysis suites, per-tool suites for Move/Pickup/SetTerrain/etc.). **Gaps:** zero tests for all 5 MultiWorld tools (the least-finished; a missing-arg test would catch the `CreateTradeRouteTool` crash); no tests for `GameHub.ExecuteTool` (the authorization-bypass finding); no security regression test for the JumpToLocation fix specifically; no tests for `AgentGrain`, `PromptRegistry` file-loading, `ReplayStorage` eviction, or the `RunAsync` loop lifecycle/threading.
