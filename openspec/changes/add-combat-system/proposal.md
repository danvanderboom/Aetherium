## Why
Combat is advertised across the codebase but cannot happen. `ContextEvaluator` gates its combat tag behind `if (false)`, no attack action/tool exists on any path, and `combat.md`, `combat-survival.json`, `Health`, and several analytics layers all reference combat that never runs. This is Phase 5 item **P3-7**. It is also the missing emitter for the **already-built `kill` quest objective** (P3-2): the grain completes `kill` objectives on an `enemy_defeated` event, but nothing ever emits one.

## What Changes
**Slice 1 — attack → damage → death, grain-authoritative (this pass):**
- Give characters real HP: `Character` defaults to `Health(100,100)`; `Monster` overrides to `Health(30,30)` (previously every character was born with `Health(0,0)`, i.e. already dead under any "dead at 0" rule).
- Add a pure `CombatSystem.TryAttack(world, attacker, targetId)`: validates the target exists, is in reach (adjacent incl. Z), is not the attacker, and has `Health`; applies a fixed damage; on lethal damage removes the target from the world. Deterministic (no RNG) so it is testable.
- Route attacks through the existing mutation path: `IMapMutationGateway.AttackAsync` (local + grain implementations) and `IGameMapGrain.AttackAsync`, which apply the hit to canonical state and fan out the standard deltas — `ComponentFieldChangedDelta` for the health change, `EntityRemovedDelta` on death. New `AttackResultDto` carries damage / remaining HP / defeated / target type.
- Add an `attack` agent tool (category `combat`, added to the Player profile) that routes through the gateway.
- **Wire the kill-quest loop:** `GameHub.ExecuteTool` emits an `enemy_defeated` narrative event when an attack defeats its target, so `kill` objectives now complete in real gameplay.
- Replace `ContextEvaluator`'s `if (false)` combat stub with real detection (an adjacent hostile with health → `in-combat`).
- Tests (combat was entirely inert): `CombatSystem` unit tests; a `GameMapGrain` attack integration test (health delta, then lethal → entity removed); tool reachability.

**Slice 2 — depth (follow-up):** monster retaliation/aggro on the tick, weapon/attack-power components, death loot, combat analytics.

## Impact
- Affected specs: `combat` (ADDED)
- Affected code: `Entities/Character.cs`, `Entities/Monster.cs`, new `Core/CombatSystem.cs`, new `Aetherium.Model/CombatDtos.cs`, `MultiWorld/{IMapMutationGateway,LocalMutationGateway,GrainMutationGateway,IGameMapGrain,GameMapGrain}.cs`, new `Agents/Tools/Combat/AttackTool.cs`, `Agents/Tools/AgentToolProfile.cs`, `GameHub.cs`, `Core/ContextEvaluator.cs`; new tests.
- Build impact: additive; no breaking changes. The only behavior change to existing systems is that `Character`/`Monster` now spawn with non-zero HP.

## Status
Slice 1 implemented on `feat/phase5-combat` (branched from `develop`). Verified: full solution build 0 errors; new `CombatSystemTests` (9) + `GameMapGrainCombatTests` (1, live-map attack → damage → death via the grain) + tool-reachability green. The kill-quest loop is now closed: a lethal attack emits `enemy_defeated`, which the narrative grain's `kill` handler (P3-2) consumes. **Full suite: 1012 passed / 0 failed / 2 seed-tolerant skips** (combat tests themselves skip nothing). A subtle engine gotcha surfaced and was handled: `Entity.Get<T>()` throws on a missing component, so all combat component access is guarded by `Has<T>()`. Slice 2 (monster retaliation/aggro, weapon/attack-power components, death loot, combat analytics) tracked but out of scope for this pass.
