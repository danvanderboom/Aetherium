## 1. Live wiring (this change)

- [x] 1.1 `ActionSpeed.TrySpend(cost)`: refill → afford → deduct (or accrue-and-defer), returning whether the action was afforded
- [x] 1.2 Default `ActionSpeed` on `Monster` (`Speed == MaxBudget == 1.0`), inherited by `Zombie`
- [x] 1.3 `GameMapGrain.StepNpcsAsync` gates each monster's tree tick on `ActionSpeed.TrySpend(NpcActionCost)`; monsters without an `ActionSpeed` always act
- [x] 1.4 Unit tests for `TrySpend`: spends when affordable, retains refilled AP when not, and a half-speed actor affords every other tick
- [x] 1.5 Cross-link the updated/added requirement(s) with `**Verified by:**` lines
- [ ] 1.6 Full regression suite green (existing `GameMapGrainCombatTests`, `EndToEndSharedMutationTests` must be unaffected — default monsters keep acting every tick)

## 2. Still open (tracked here, not resolved by this change)

- [ ] 2.1 Wire `ActionQueue` + `ActionSystem` into the **player-command** path (`MoveAsync`/`AttackAsync`), including the deferred-action-reports-to-client RPC-contract decision the pipeline's design doc flagged
- [ ] 2.2 Grain-level test that a genuinely slow monster acts on a sub-cadence — needs a spawn path that produces a non-default-speed creature (lands with `add-npc-behavior-trees` task 2.4, per-creature trees/speeds)
- [ ] 2.3 Consider refactoring `ActionSystem.Tick` to call `ActionSpeed.TrySpend` so the refill/afford/deduct logic has a single source of truth
