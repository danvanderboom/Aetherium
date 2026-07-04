> Status (2026-07-03): implemented and verified on `develop` (feat: Phase 5 (P3-5) — an agent plays the live shared map). Suite 939 passed / 0 failed (+3); server boots with Orleans. Unchecked items are deliberately out of this slice's scope.

## 1. Run loop
- [x] 1.1 Replace the off-scheduler `Task.Run` loop with an Orleans grain timer (`RegisterGrainTimer`, non-interleaving); self-stop after `maxSteps` and on Stop/Detach

## 2. Live-map participation
- [x] 2.1 `GameMapGrain.ComputeAgentPerceptionAsync(entityId)` — perception from the canonical world (reuses `PerceptionService`)
- [x] 2.2 `AgentRunnerGrain.AttachToWorldAsync(worldId, mapId, agentId)` — join as a Character + `GrainMutationGateway`
- [x] 2.3 `StepAsync` perceives via the map grain and acts via the gateway on the live-map path; legacy session path preserved
- [x] 2.4 `DetachAsync` calls `LeavePlayerAsync` to remove the agent's Character

## 3. Reachability + tests
- [x] 3.1 `aetherctl agent attach-world <worldId> <mapId>` command
- [x] 3.2 Integration tests: agent attaches, plays, and fans out to human sessions; grain-timer loop advances and self-stops; detach removes the agent
- [ ] 3.3 Feed prompt templates (`PromptRegistry`) into the LLM system prompt (out of scope — heuristic path proven; LLM depth is follow-up)
- [ ] 3.4 Per-profile attach (Explorer/WorldBuilder) and multi-agent orchestration (out of scope)
