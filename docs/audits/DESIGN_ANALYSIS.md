# Aetherium System Design Analysis

*Date: 2026-07-03. A high-level assessment of Aetherium's architecture, drawing on the ten subsystem audits in this directory. This document is about **design and direction**, not individual bugs — for those see [RECOMMENDATIONS.md](RECOMMENDATIONS.md) and the per-subsystem audits.*

> **Reconciliation note (`develop` @ 2026-07-03).** This analysis was written at baseline `5b7e267`. The 23 commits that landed on `develop` afterward move the themes below in a genuinely encouraging direction — and *in the direction this document recommends*. Theme 1 (breadth over depth) is being actively reversed: the multiplayer, persistence, and travel work is exactly the "start building the bridges" the strategic recommendation called for, and it shipped with real cross-grain integration tests rather than more scaffolding. Theme 4 (aspirational persistence) is largely resolved — durable world state via snapshot + delta-log + SQLite now works. Theme 2 (parallel implementations) got *worse* before better: there are now **three** action paths (obsolete hub methods, `LocalMutationGateway`, `GrainMutationGateway`), so the consolidation argument is more urgent. Theme 3 (authoritative server that doesn't validate) is **unchanged and now more dangerous** — the new default multiplayer path is also unvalidated and silently bypasses the tool layer's advertised distance clamp. Theme 5 (no safety net) is **unchanged** — still no CI, which is precisely why the two hardest blockers survived the sprint: the server still cannot boot with Orleans enabled (verified by boot test), and the whole-solution build still fails. Net read: the team is deepening the right vertical slice (single-world → multiplayer → persistence), but two gates still stand between all of that work and a player reaching it — **fix the boot hang, and stand up CI** — after which the movement/interaction validation gap is the next thing that must close before this is a game rather than an architecture.

## The one-paragraph verdict

Aetherium has an excellent core idea, executed well at its center and thinly at its edges. The server-authoritative, perception-only client-server model is a genuinely strong architectural choice, cleanly implemented, and it is the project's crown jewel. Around that core, though, the codebase has grown far faster in **breadth** (multi-world ecosystems, emergent narrative, dungeon instances, parties/raids, LLM agents, agent training, procedural audio) than in **depth**: most of these subsystems are scaffolded — grains and data models built and unit-tested in isolation — but the *bridges* between them are missing, stubbed, or dead-wired, so they don't function in actual gameplay. The single most important design decision facing the project is not technical but strategic: **stop widening and start deepening** — pick the vertical slice that constitutes "the game" and make it work end-to-end before adding another system.

## What the architecture gets right

- **Server-authoritative perception.** Clients receive only FOV/lighting-gated, viewport-bounded, *relative*-coordinate perception; absolute world coordinates never leave the server. This eliminates a whole class of cheats and — as the perception audit found — eliminated the historical FOV-rotation bug by construction (there is now one coordinate frame on both sides). Tests actively assert the information-hiding boundary. This is the model to protect and build on.
- **A unified action API.** Players and AI agents act through the same `ExecuteTool(toolId, args)` surface, backed by a reflection-discovered tool registry with capability-based access profiles. This is the right abstraction for a game that wants both human and agent players.
- **Clean presentation abstraction.** `IGameRenderer`/`IAudioSystem` on the client and shared `Aetherium.Model` DTOs let new clients (Unity, Unreal) attach without server changes.
- **Deterministic PCG core.** SHA256-derived, namespaced RNG streams and validation that records access-path proof artifacts are genuinely well-engineered.
- **Idiomatic Orleans building blocks.** Named persistent stores, `[GenerateSerializer]` state, a co-hosting DI bridge, and a SignalR backplane over the cluster are all sensible choices.

## The five cross-cutting design problems

### 1. Breadth over depth — the "scaffold and move on" pattern

This is the dominant theme across every audit. The pattern repeats: a subsystem is designed, its grains/classes and data models are built, unit tests are written against those classes in isolation, the OpenSpec change is marked done (or the docs declare it "✅ Complete and tested") — and then the wiring that would make it work in a running game is left as a TODO. Concrete, verified examples:

- **Narrative** consequence engine never runs in gameplay (gated behind a `session.WorldId` that real sessions never set).
- **Multi-world travel** is impossible end-to-end (portal use needs a `WorldId` only a successful portal-use can set — a circular dependency; no client even calls it).
- **Instances / parties / raids** have no controller, hub, tool, or client entry point at all — the entire stack is unreachable code with zero tests.
- **Meta-progression** can't persist (its storage provider isn't registered) and is keyed to per-connection identities.
- **Agent training** curricula on disk can never drive a runtime progression (controller and grain are unconnected).
- **PCG placement** (NPCs, items, prefab entities) and **combat** are console-log stubs or `if(false)`.

**Design implication:** the project's effective scope is a fraction of its apparent scope, and the gap is invisible from the outside because the docs and task checkboxes describe intent, not reality. The remedy is a deliberate **"definition of done" that includes an end-to-end path** (a player or agent can reach the feature through a client and observe its effect), plus retiring or clearly quarantining subsystems that won't be finished soon.

### 2. Parallel, drifting implementations of the same concept

The codebase repeatedly grows a second implementation of something rather than evolving the first, then lets the two drift:

- **Two action paths:** 13 `[Obsolete]` hub methods vs. `ExecuteTool` — already behaviorally divergent (move-distance handling, which narrative events fire, success/options contract shape).
- **Two time systems:** per-session `TimeScale` (drives day/night in perception) vs. global `WorldClock` (drives weather/spawns/events) — each session sees a different time-of-day than the simulation.
- **Two world-building paths:** hand-authored `WorldBuilders` (used for live sessions, skip validation) vs. the `WorldGen` pipeline (used by tooling, validated).
- **Three divergent worldgen pass lists** — the game server produces materially emptier worlds than the CLI/dashboard preview of the same request.
- **Duplicated engine code** between `Aetherium.Console` and `Aetherium.Server` (perception, lighting, components, geometry — byte-identical copies that have begun to diverge), plus the Unity "Lite" DTO shims with no drift detection.

**Design implication:** every duplicate is a place where a fix must be made twice and where behavior silently diverges. The remedy is consolidation: one canonical action path, one clock, one world-construction pipeline that both live sessions and tooling share, and a single source of truth for DTOs (extract the shared engine into a library both Console and Server reference, rather than copying it).

### 3. "Authoritative" server that doesn't fully validate

The perception *output* is properly server-authoritative, but the action *input* side under-validates, which undermines the security premise:

- Player movement applies deltas with **no** wall/passability/distance checks — walk through walls, teleport arbitrarily far (the validated `TryMove` is dead code, and itself has a wrong-axis bug for West/East).
- Door/use interactions have **no range check** — act on any entity by ID, map-wide.
- The **REST control-plane is anonymous** — cluster economy, meta-progression, adaptation reload are unauthenticated.
- **Tool authorization is bypassed at the hub** — `GameHub.ExecuteTool` never consults the access profile; only each tool's internal check stands between a player and a world-editing tool.

**Design implication:** for a server whose whole point is authority, validation must be a first-class, centralized concern, not per-call convention. The remedy is a single validated movement/interaction path (delete the bypasses), centralized tool authorization at the hub, and an auth posture for the REST surface (even a shared API key in dev).

### 4. Persistence is aspirational

Nothing survives a restart today. Grain state is memory-only; the Azure Table Storage path is commented out; `GameMapGrain` never serializes its `World` (reactivation yields an empty placeholder); several grains reference storage providers that aren't registered (`metaStore` throws on every activation). The system is architected *as if* it persists (persistent-state attributes, snapshot stores, save/load methods) but the implementations are stubs.

**Design implication:** the multiplayer, multi-world, meta-progression ambitions all assume durable state. Either commit to a real persistence layer (finish the Azure path or add a file/SQLite backend, serialize the `World`, register every referenced store) or explicitly scope the product to ephemeral single-session play and remove the persistence scaffolding that implies otherwise.

### 5. No safety net — CI, build gate, and honest status

There is no functioning CI (the one workflow triggers on a nonexistent `master` branch and runs no tests), and no full-solution build gate. That is *how* the Dashboard could sit uncompilable for ~8 months inside the solution without anyone noticing, how **the server itself became unbootable with Orleans enabled** (a self-referential DI registration hangs startup — verified by boot test — yet 703 tests stay green because they all bypass `Program.cs`), and how the OpenSpec `tasks.md` checkboxes came to bear no relationship to reality (unchecked-but-done in some changes, checked-but-broken in others). The test suite compounds this: it thoroughly tests each grain/class as an island but exercises almost no cross-grain end-to-end flow — which is exactly where every broken seam lives.

**Design implication:** a minimal `dotnet build Aetherium.sln && dotnet test` gate on `main` would have caught the build break and the runtime mismatch on day one, and is the highest-leverage single change available. Beyond that, the project needs integration tests that cross grain boundaries (connect → act → observe perception; travel between worlds; complete a cross-world quest), and a discipline that docs/task-status reflect what runs, not what was intended. Consider replacing "✅ Complete and tested" doc claims with an explicit **implemented / partial / planned** legend.

## Grain-granularity and concurrency notes (secondary)

- **Mixed concerns:** `WorldGrain` holds both world *state* and cross-map *coordination*; splitting state from orchestration would clarify responsibilities and reduce the reentrancy surface.
- **Reentrancy hazards:** `[Reentrant]` `WorldGrain` performs read-modify-write across `await`s (player-count check then increment), allowing capacity to be exceeded.
- **Orleans threading violations:** `AgentRunnerGrain` drives its loop via `Task.Run` calling grain methods off the scheduler — it should use grain timers/reminders.
- **Unbounded growth:** telemetry snapshots, replay JSON, generated-quest lists, and lockout ledgers grow without caps — fine for a demo, a memory leak for a long-running silo.

These are real but subordinate to the five themes above; address them as the affected subsystems are deepened.

## Deployment reality vs. ambition

The docs and `ORLEANS_IMPLEMENTATION_PLAN` describe Azure-hosted, multi-silo clustering. The running system is localhost-clustering, memory-storage, single-process, with the Azure paths commented out and version/runtime mismatches that break the dev scripts on a clean machine. This isn't wrong for the current stage — but the gap between the documented deployment story and the actual one should be stated plainly so it isn't mistaken for production-readiness.

## Strategic recommendation

Aetherium doesn't need more systems; it needs one complete one. The highest-value strategic move is to **choose a vertical slice and finish it end-to-end** — the obvious candidate is *single-world exploration with validated movement, working interactions, visible NPCs, and either a human or an LLM agent playing through it*, since that path already has the most infrastructure. Get that slice: (1) validated and secure, (2) covered by an integration test that plays it through, (3) gated by CI, and (4) documented as actually-working. Then, and only then, re-open the next system (persistence, multi-world, narrative) — each with the same end-to-end bar. The [IMPROVEMENT_PLAN.md](IMPROVEMENT_PLAN.md) sequences the concrete work; this document is the argument for *why that sequence, in that order*.

## Interaction with in-flight OpenSpec changes

Seven active OpenSpec changes overlap heavily with the audited subsystems (`add-multiworld-ecosystems`, `add-agent-training-pcg`, `add-worldbuilding-tool-integration`, `add-unity-2d-client`, `add-xbox-controller-unity`, `add-aetherctl-extensions`, `add-multi-use-tools`). Several are marked complete but are contradicted by the audits (build break, unreachable travel, hollow tests). Before starting *new* proposals, these should be reconciled: update each `tasks.md` to reflect verified reality, split "implemented vs. planned," and let the correctness/wiring fixes below feed back into the specs they touch. The recommendations here are intended to *complement*, not conflict with, that in-flight work — most are the "make it actually run" other half of changes that already landed their scaffolding.
