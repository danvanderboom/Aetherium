# Gameplay Telemetry Pipeline — Design Vision

**Status:** Living design. Agent telemetry ships today (in-memory, training-focused); gameplay telemetry for designers does not exist — see §8.
**Scope:** What telemetry-driven studios actually do with data, Aetherium's layered pipeline from event records to feedback loops, the synthetic-playtesting and LLM-analyst leaps, and the maturity ladder.
**Audience:** Engine maintainers, OpenSpec proposal authors, and game/campaign designers building on Aetherium.

---

## 1. Framing

Aetherium is an engine, not a game. What counts as a balance question — TTK distributions in a combat game, route popularity in a trading game, puzzle abandonment in a mystery game — is per-game; the machinery of *recording, aggregating, and feeding back* is the engine's job. Everything below is **per-world declarative configuration** (`TelemetryConfig`: what to record, sampling rates, retention, sink) with pluggable transport.

Telemetry is distinct from two neighbors it must never be confused with. **Dev observability** (logs, traces, silo health) serves operators debugging the system — a separate concern with its own industry stack. **Game state** (standing, XP, inventories) is what the simulation *is*. Telemetry is the third thing: an out-of-band, drop-tolerant record of *what happened*, for people (and systems) tuning what the game *should be*. It must never sit in the hot path, and losing it must never affect gameplay.

The unifying design move: Aetherium's subsystems already emit their meaningful moments as **namespaced tags** — `kill:`, `quest:`, `market:`, `event:`, death/respawn transitions. The telemetry pipeline is a *subscriber to that same vocabulary*, not a new instrumentation layer scattered through gameplay code. One emission discipline serves doctrines, quests, the event director, and the analyst alike.

## 2. What the telemetry-driven studios teach

| Practice | What it does | The lesson |
|---|---|---|
| **Bungie's Halo 3 death heatmaps** | Every death plotted on the map; posters of red clouds drove level redesign before launch | **Spatial legibility.** Designers act on *pictures of where*, not tables of counts. Position belongs in every event record |
| **Valve's playtest telemetry (TF2/L4D)** | Instrumented playtests measured stress, deaths, and pacing; the AI Director was tuned against real intensity curves | **Telemetry closes the loop with direction.** The same signals that inform designers can drive runtime systems |
| **Riot / League balance pipeline** | Win/pick/ban rates across millions of matches, segmented by skill cohort | **Distributions over anecdotes, cohorts over averages.** A change that helps novices may break experts; segmentation is not optional at scale |
| **CCP's EVE economists** | A staff economist publishes monthly reports on faucets, sinks, and money supply from full economic telemetry | **Economies need instruments.** Faucet/sink accounting is the difference between managing inflation and discovering it (pairs with [economy-simulation.md](economy-simulation.md) §7) |
| **Destiny / live-service funnels** | Quest-step completion funnels expose exactly where players stall or quit | **Funnels find friction.** Content is a pipeline; abandonment points are its bug reports |
| **SC2 / replay culture** | Full replays as the forensic record behind every balance argument | **Keep the forensic tier.** Aggregates say *that*; replays say *why*. (Agent telemetry already stores failure replays — the pattern generalizes) |

Distilled, the five properties the pipeline needs — each mapped to the Aetherium asset it builds on:

| Property | In play | Engine asset |
|---|---|---|
| **Cheap, uniform emission** | Structured records from existing chokepoints and tag emissions | Kill/death/quest/market/event chokepoints (shipped) |
| **Out-of-band transport** | Pluggable sinks; gameplay never blocks or breaks on telemetry | Orleans streams / DI seam (to build) |
| **Legible rollups** | Heatmaps, funnels, distributions, faucet/sink reports | Dashboard (shipped, Blazor) |
| **Feedback loops** | Rollups feed adaptive PCG and event-director pressure | `WorldGen/Adaptation` (shipped), [live-events.md](live-events.md) signals |
| **Forensics** | Sampled event streams and failure replays | `ReplayStorage` pattern (shipped for agents) |

## 3. The layered, composable model

```
1. EVENTS      GameplayEvent{kind, actor, world, map, location, ts, data}
               emitted at existing chokepoints — kill:/quest:/market:/event:
               tags, death/respawn, session start/end, ability casts
                    │
2. TRANSPORT   out-of-band, drop-tolerant, pluggable sink:
               in-memory ring (dev) → JSONL file → Event Hub/Kafka/App Insights
                    │
3. ROLLUPS     aggregations computed from the stream:
               deaths-by-location heatmaps · TTK distributions · drop rates ·
               quest funnels · faucet/sink ledgers · route popularity
                    │
4. SURFACES    dashboard pages (cohort filters), REST queries,
               designer exports — reusing AgentDashboardHub patterns
                    │
5. FEEDBACK    rollups feed adaptive PCG difficulty and the live-event
               director's WorldPressure signals
```

Design rules that keep this composable:

- **One record shape, namespaced kinds.** `GameplayEvent.kind` reuses the tag vocabulary (`kill:wolf`, `quest:abandoned:rescue`, `market:shortage:grain`). No per-subsystem record schemas; `data` carries kind-specific payloads.
- **Emission is fire-and-forget at chokepoints that already exist.** The kill branches, death transitions, quest state changes, and market events are single call sites; telemetry is one line at each, behind `TelemetryConfig` sampling. No instrumentation sprawl.
- **Sinks are a DI seam, not a dependency.** Dev default is an in-memory ring + optional JSONL; production deploys bind Event Hub/Kafka/App Insights. The engine never takes a cloud dependency.
- **The director and the dashboard drink from the same stream.** [live-events.md](live-events.md)'s `WorldPressure` signals are telemetry rollups by another name — computed once, consumed by both designers and the runtime director. One aggregation tier, two audiences.
- **Analysis output is data, not prose.** Rollups emit structured findings (metric, segment, value, threshold); rendering them as sentences is a surface concern — in the viewer's locale ([localization.md](localization.md)). The shipped `PerformanceAnalyzer`'s hardcoded English recommendation strings are the anti-pattern to retire.
- **Privacy by construction.** Records carry session/actor ids, never client identity; retention and sampling are per-world config; worlds can disable recording entirely. Telemetry is for tuning games, not profiling people.

## 4. Creative leaps

1. **Synthetic playtesting as a product.** Aetherium already runs LLM/heuristic agent fleets through the real game loop, and its agent telemetry already feeds an auto-curriculum generator. Point those fleets at a new world with gameplay telemetry on, and designers get death heatmaps, TTK distributions, and quest funnels **before a single human plays** — Halo-3-style insight at authoring time, continuously, per PCG seed. No other engine has the agent substrate to offer this.
2. **The LLM analyst.** CCP employs an economist; Aetherium can ship one. An LLM agent reads rollups through the tool registry and produces periodic balance reports — anomalies, trends, suggested tunings — as auditable artifacts (and, at the ECA tier, as *proposed* rule changes a designer approves). The same pattern as LLM faction leaders and the game-master: intelligence at the edges, auditable data at the core.
3. **Telemetry-steered PCG as a closed loop.** `WorldGen/Adaptation` already adapts to per-run agent telemetry. Feeding it population-level gameplay rollups (deaths cluster in cave chokepoints → widen them next seed; nobody finds the optional wing → strengthen its cues) turns every world generation into a balance iteration.
4. **Seed-comparable heatmaps.** Deterministic PCG means telemetry can be overlaid *per seed and per generator version* — A/B testing level-generation changes with real traffic, a capability handcrafted-level studios pay dearly to approximate.

## 5. Maturity ladder

| Tier | Ships | Depends on |
|---|---|---|
| **T0 — Emission substrate** | `GameplayEvent` record + `TelemetryConfig` per world (enabled kinds, sampling, retention); emission at existing chokepoints (kill, death/respawn, session, quest transitions); in-memory ring + JSONL sink | Chokepoints (shipped) |
| **T1 — First rollups** | Deaths-by-location, kills-by-type/time, session lengths, TTK distributions; a dashboard page with world/time filters | T0; dashboard (shipped) |
| **T2 — Funnels & economy instruments** | Quest funnel rollups; drop-rate reports; faucet/sink ledger (economy T0+); cohort segmentation | T1; economy T0 |
| **T3 — Feedback loops** | Rollups feed `WorldGen/Adaptation` and the live-event director's `WorldPressure`; per-seed heatmap overlays | T1; live-events T1 |
| **T4 — Production pipeline** | Pluggable Event Hub/Kafka/App Insights sinks; cluster-scale aggregation; agent-fleet synthetic playtest runs as an authoring-time service | T2; multiworld (shipped) |
| **T5 — The analyst** | LLM analyst emitting balance reports and proposed tunings as auditable artifacts; ECA rules with telemetry conditions (`when: rollup.deaths_per_hour(room) > x`) | T3; ECA runtime |

## 6. The ECA graduation path

A feedback rule is already a degenerate ECA rule: `WHEN deaths_per_hour(location) > x THEN raise(director_pressure)`. The generalization follows the established path: conditions over rollup values → action lists (adjust PCG parameters, flag content for review, shift event weights) → tuning policies as ECA graphs → the LLM analyst proposing rules that designers ratify. Telemetry conditions become one more vocabulary the shared ECA palette offers every other subsystem.

## 7. Anti-goals

- **Never in the hot path.** No gameplay outcome may ever depend on a telemetry write succeeding; sinks are drop-tolerant by contract.
- **Not observability.** Logs/traces/silo metrics are an operator concern with separate tooling; this pipeline records *game* events only.
- **No player profiling.** Pseudonymous ids, per-world retention limits, world-level opt-out. The unit of analysis is the game, not the person.
- **No English in analysis output.** Structured findings only; prose is a localized surface concern.
- **No mandatory cloud.** The dev-default sink runs entirely local.

## 8. Current state

- **Shipped (agent telemetry):** `AgentRunnerGrain` records a `PerformanceSnapshot` per agent step (action, success, latency, perception complexity) into `AgentTelemetryGrain`; failure replays after 3+ consecutive failures into `ReplayStorage`. Consumed by `/api/agenttelemetry/*`, `AgentDashboardHub`, the Blazor training dashboard, and `AutoCurriculumGenerator` (telemetry → difficulty stages). Spec: `openspec/specs/agent-telemetry/spec.md`.
- **Shipped caveats:** storage is entirely in-memory and non-persistent — `AgentTelemetryGrain` holds a plain list capped at 1000 (despite the spec's persisted-state language) and `ReplayStorage` is a process-lifetime static dictionary capped at 200; everything vanishes on silo restart. `PerformanceAnalyzer` emits hardcoded English recommendation strings.
- **Adjacent:** `WorldGen/GenerationMetrics` (generation-time validation metrics, not gameplay); console-side `MapFrameMonitor` (dev debugging stream, port 5001).
- **Absent:** any gameplay telemetry (kills, deaths, sessions, funnels, economy metrics), any rollup/aggregation tier, any observability plumbing (no OpenTelemetry/metrics; logging is `Console.WriteLine` with prefixes).
- **The gap in one sentence:** the engine records how *agents* perform but nothing about how *games* play; T0 is one record type, one config object, and one-line emissions at chokepoints that already exist.
