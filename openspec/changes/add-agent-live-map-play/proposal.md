## Why
The agent system already has a real decision loop (`AgentRunnerGrain`: perception → LLM-or-heuristic policy → tool execution → telemetry) and a strong tool registry, but an agent could never actually *play the shared world*. `AttachAsync` bound the runner to a pre-existing human `GameSession` and its tools mutated that session's **local** world copy — the agent was invisible to other players and required a human to be present first. Worse, the run loop ran on an off-scheduler `Task.Run`, mutating grain state outside the Orleans scheduler (a threading violation the audit flagged). With the Phase 5 NPC slice making monsters live on the shared map, the natural next step (P3-5) is to let an agent be a first-class participant on that same map.

## What Changes
- **Grain-timer run loop.** `AgentRunnerGrain.RunAsync` now schedules `StepAsync` on an Orleans grain timer (`RegisterGrainTimer`, non-interleaving) instead of `Task.Run`, so every step runs on the grain's activation turn — serialized, no races. The timer self-disposes after the `maxSteps` budget and on Stop/Detach.
- **Agent joins a live map.** `AgentRunnerGrain.AttachToWorldAsync(worldId, mapId, agentId)` joins the map as a Character (id == agentId, exactly like a player, via `GameMapGrain.JoinPlayerAsync`) and routes the agent's tool verbs through a `GrainMutationGateway` — the same gateway a human gets after `JoinWorld`. The agent's moves mutate canonical state and fan out to every human player: it plays the *shared* world, visibly, not a private copy. No human session is required.
- **Agent perception.** `GameMapGrain.ComputeAgentPerceptionAsync(entityId)` computes perception from the canonical world for an in-world entity (reuses `PerceptionService`), so a connectionless agent can perceive each step.
- **Cleanup + reachability.** `DetachAsync` calls `LeavePlayerAsync` so the agent doesn't linger. A new `aetherctl agent attach-world <worldId> <mapId>` starts an agent against a live map. The deterministic heuristic policy needs no LLM, so an agent plays (and is tested) without an external model.

## Impact
- Affected specs: `simulation` (autonomous agents act on the shared map; grain-timer run loop)
- Affected code: `Aetherium.Server/Agents/{AgentRunnerGrain,IAgentRunnerGrain}.cs`, `Aetherium.Server/MultiWorld/{GameMapGrain,IGameMapGrain}.cs`, `Aetherctl/Commands/AgentCommands.cs`

## Status
Implemented in this slice on `develop`. Verified: solution build 0 errors; `Aetherium.Test` 939 passed / 0 failed (+3, 1 seed-tolerant ignore); server boots with Orleans, 0 exceptions. Out of scope (later work): LLM decision quality and prompt-template feeding into the system prompt (the heuristic path is what's proven here), multi-agent orchestration, agent combat (needs P3-7), and the `NarrativeDesigner` profile (no tools exist for it yet).
