## Context

The Explore-agent research done ahead of this pass (see conversation history / gap-analysis
follow-up) mapped the live path in detail: `GameMapGrain.TickAsync` → `StepNpcsAsync`, driven by
`WorldTickService` at `SimulationOptions.TickHz` (default 1 Hz). It also surfaced ten impedance
mismatches between the four Wave-0/1 primitives (action pipeline, combat model, abilities, NPC
behavior trees) and the live grain/RPC path — most of them concentrated in delta-emission gaps and a
synchronous-RPC-vs-deferred-action contract question that only affects the *other* three primitives.
NPC-AI wiring alone sidesteps nearly all of them, which is why it goes first.

## Goals / Non-Goals

**Goals:** replace `StepNpcsAsync`'s inline decision with the shipped behavior-tree engine, with
zero observable behavior change for the existing test suite, plus close the monster-vs-monster
targeting gap found while designing the wiring.

**Non-goals (explicitly deferred, tracked in tasks.md):**
- Switching monster attacks from `CombatSystem.TryAttack` to `DamagePipeline` — that requires
  deciding the Dying/Corpse client-visibility and delta-vocabulary questions the combat-model wiring
  slice owns, not this one.
- Wiring `ActionSystem`/`ActionQueue` for monsters or players — a separate slice; the synchronous
  RPC contract (`MoveAsync`/`AttackAsync` return the resolved outcome in the same call) is preserved
  untouched here.
- Distinct trees per creature type (`Snake`/`Zombie`) — still task 2.4, open.
- Perception-sourced blackboard population — still task 2.5, open.
- `ThreatTable`-driven target selection — still task 2.6, open.

## Decisions

### One BehaviorTree instance per monster, cached on the grain

`GameMapGrain._monsterTrees: Dictionary<string, BehaviorTree>`, keyed by `EntityId`, built lazily the
first tick a monster is seen and reused every tick after. This is what makes the "Per-NPC Behavior
Tree Instance" requirement real in the live path (a fresh tree per tick would make `Blackboard`/
composite-node state pointless). Stale entries (monster no longer in `_world.Entities`, e.g. it died)
are pruned at the top of each `StepNpcsAsync` call — cheap (`O(monsters)` set diff) and keeps the
dictionary from growing unbounded over a long-lived map.

### Target scoping via a `Targets` blackboard key, not a `MonsterBehaviors` signature change

`MonsterBehaviors.FindAdjacentTarget` now prefers a caller-supplied `IReadOnlyList<Entity>` stored
under `MonsterBehaviors.TargetsKey` in the blackboard, falling back to the old any-`Health`-entity
scan when absent. `GameMapGrain` populates it with the same joined-player list `FindAdjacentPlayer`
used to search — same source of truth, same semantics, just relocated. Alternative considered: change
`BuildWanderAndMeleeTree`'s signature to take a target-provider delegate. Rejected — the blackboard
already exists for exactly this kind of per-tick, per-NPC context injection, and a delegate captured
at tree-construction time would need to close over a mutable "current players" reference anyway
(monsters are built lazily across many ticks, and the player roster changes tick to tick), which is
just the blackboard with extra ceremony.

### Outcome reporting via `AttackOutcome`/`WanderOutcome` blackboard writes

`BehaviorTree.Tick` returns only a `BehaviorStatus` — not enough for `GameMapGrain` to build its
`EntityMovedDelta`/`ComponentFieldChangedDelta` broadcasts, which need the actual before/after values.
Rather than growing `BehaviorTree`'s return type (which would leak grain-specific concerns into a
game-agnostic engine type), the two action nodes in `BuildWanderAndMeleeTree` write a small outcome
record to the blackboard on success; `GameMapGrain` reads and clears both keys immediately after each
`Tick` call. This keeps `BehaviorNode`/`BehaviorTree` themselves untouched — the reporting convention
lives entirely in `MonsterBehaviors`, the one file that already knows both what the tree does and
what a caller might need back.

### Renamed the new `MoveOutcome` record to `WanderOutcome`

`Aetherium.Core.MoveOutcome` already exists (the result type of `World.TryMoveSteps`). Reusing the
name inside `Aetherium.Server.Ai` caused a `CS0104` ambiguous-reference error at the `GameMapGrain`
call site, which sits in a namespace that can see both. Renamed rather than qualifying every call
site, since `WanderOutcome` is also a more precise name for what it actually reports (a completed
wander step, not a generic move).

## Risks

- **Behavior-tree overhead per monster per tick** is higher than the old direct `if/else` (one
  `SelectorNode`/`SequenceNode`/`ConditionNode` allocation graph walked per monster, vs. two method
  calls). Not measured here; acceptable for now given `TickHz` defaults to 1 Hz and existing tests
  show no timeout regressions, but worth profiling before raising `NpcMoveIntervalTicks` down to
  sub-second cadences.
- **`_monsterTrees` is in-memory only**, not part of `MapState`'s persisted snapshot. A grain
  reactivation (silo restart, deactivation due to idleness) resets every monster's tree — including
  mid-`Running` composite-node state, if any future tree uses `Running` nodes (the shipped
  attack-or-wander tree never returns `Running`, so this is currently a no-op risk, but will matter
  once task 2.4/2.6 add richer trees).
