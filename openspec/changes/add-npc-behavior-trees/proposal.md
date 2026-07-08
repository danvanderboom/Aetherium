## Why

The last Wave 0 item: monsters have no real brains. `Monster.NextWanderDirection()` is undirected random-walk-with-momentum, and `GameMapGrain.StepNpcsAsync` hardcodes a single global rule ("attack if a player is adjacent, else wander") inline for every monster — no chase, no per-monster personality, no shared decision-making abstraction. Verified (`Aetherium.Server/Entities/Monster.cs`, `Snake.cs` has zero AI, `Zombie.cs` is an empty override): the [2026-07-06 engine gap-analysis](../../docs/audits/2026-07-06-engine-gap-analysis/design-next-steps.md) §4.5 calls this out as the last **P0**, needed because "combat needs opponents" (§4.2, already deepened in [deepen-combat-model](../deepen-combat-model/proposal.md)).

## What Changes

- Add a generic behavior-tree engine (`Aetherium.Server/Ai/`): `BehaviorNode`/`BehaviorStatus` (Success/Failure/Running), leaves (`ConditionNode`, `ActionNode`, `WaitNode`), composites (`SequenceNode`, `SelectorNode`, `ParallelNode`, `RandomSelectorNode`, `UtilitySelectorNode`), and a per-NPC `Blackboard`.
- Add `MonsterBehaviors.BuildWanderAndMeleeTree`: a worked example reproducing `StepNpcsAsync`'s exact current decision (attack an adjacent target via the live, unmodified `CombatSystem`, else wander via `Monster.NextWanderDirection`) — proof the engine can express what's hardcoded today, and a concrete candidate to replace that inline logic.
- **Phase 1 (this change): the engine + one worked example, fully unit-tested (17 tests) in isolation.** `GameMapGrain.StepNpcsAsync`, `Monster`, `Snake`, and `Zombie` are **unchanged** — the live tick loop keeps using its inline wander/attack rule exactly as it does today.
- Phase 2 (follow-up change): give `StepNpcsAsync` a per-monster `BehaviorTree` instance (one tree per NPC per the design's "cheap-brain" per-instance state), replace the inline decision with `tree.Tick(world, monster)`, and give `Zombie`/`Snake` their own distinct trees instead of inheriting/lacking `Monster`'s generic one.

## Impact

- Affected specs: new capability `npc-behavior-trees` (behavior-tree engine requirements)
- Affected code: new `Aetherium.Server/Ai/*.cs`, new tests under `Aetherium.Test/Ai/`. No changes to `GameMapGrain.cs`, `Monster.cs`, `Snake.cs`, or `Zombie.cs` in this change.
