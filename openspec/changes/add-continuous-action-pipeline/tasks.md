## 1. ECS primitives (Phase 1 — this change)

- [x] 1.1 `ActionSpeed` component (`Speed`, `Budget`, `MaxBudget`, refill helper)
- [x] 1.2 `QueuedAction` value type + `ActionQueue` component (depth-capped queue, default `MaxDepth = 1`)
- [x] 1.3 `ActionSystem` with `Tick(World)`: refill all `ActionSpeed` budgets, dispatch affordable queued actions (Attack via injected `CombatSystem`, Move via injected movement delegate), defer the rest
- [x] 1.4 Unit tests: refill accumulation and cap at `MaxBudget`; dispatch when budget covers cost; deferral when it doesn't (action stays queued, budget unchanged until it does); queue depth capping; attack dispatch routes to `CombatSystem.TryAttack` with correct args; move dispatch routes to the movement delegate
- [x] 1.5 `openspec/specs/engine-core/spec.md` delta: ADDED requirements for Action Budget, Action Queue, Action Tick Scheduling
- [x] 1.6 Cross-link every requirement with a `**Verified by:**` line naming the test(s) that cover it

## 2. Live wiring (Phase 2 — separate follow-up change, not started here)

- [ ] 2.1 Attach default `ActionSpeed` to `Character`/`Monster` construction
- [ ] 2.2 Route `GameMapGrain.AttackAsync` through `ActionQueue` + `ActionSystem`; design how a deferred attack reports back to the caller
- [ ] 2.3 Route `StepNpcsAsync` monster wander/attack decisions through the same queue/system
- [ ] 2.4 Route player/NPC movement through `ActionQueue` + `ActionSystem` (currently immediate via `World.TryMoveSteps`)
- [ ] 2.5 Retire or fold `SimulationOptions.NpcMoveIntervalTicks` into per-monster `ActionSpeed` once monsters carry the component
