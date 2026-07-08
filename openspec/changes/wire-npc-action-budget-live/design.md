## Context

Second slice of the Phase 2 live-wiring pass (action-pipeline → combat → abilities → NPC-AI spine).
The first slice (`wire-npc-behavior-trees-live`) put a behavior tree in charge of each monster's live
tick, with the tree *acting inline* — resolving attacks through `CombatSystem` and moves through
`World.TryMoveSteps` directly, as its `npc-behavior-trees` spec requires. This slice layers the
continuous-action cadence on top.

## Goals / Non-Goals

**Goals:** give live monsters a speed-based action cadence (the §4.1 headline) with zero behavior
change for today's parity-speed monsters, and expose a reusable `TrySpend` primitive so the cadence
logic isn't reimplemented ad hoc.

**Non-goals (deferred, tracked in tasks.md):**
- Wiring `ActionQueue`/`ActionSystem` for NPCs — see the decision below on why the queue/dispatch
  model belongs to the player-command path.
- Differentiating specific creatures' speeds (slow zombie, fast wolf) — a gameplay change that lands
  with per-creature trees (`add-npc-behavior-trees` task 2.4); this slice keeps every monster at
  parity speed so nothing rebalances unprompted.
- Player-command action gating — the next slice, and the one with the real RPC-contract design
  question (does a queued player action resolve synchronously within the RPC or defer to a tick).

## Decisions

### `ActionSpeed` cadence-gating for NPCs, not `ActionQueue`/`ActionSystem` dispatch

The action pipeline has two separable halves: the **accrual half** (`ActionSpeed`: a budget that
refills by `Speed` each tick, act when affordable) and the **buffering half** (`ActionQueue` +
`ActionSystem`: hold an intended action, dispatch it when the budget covers it). An NPC whose brain
(the behavior tree) already decides *and executes* in one tick only needs the accrual half — the tree
runs when the monster can afford to act, and does its thing. Introducing a queue would mean
restructuring the tree from "decide and act" into "decide and enqueue," which (a) contradicts the
`npc-behavior-trees` spec shipped one slice ago ("the tree resolves an attack … does not move the
monster that tick"), and (b) forces a `WorldDirection` ⇄ `(dx, dy)` round-trip between the tree's
direction-based wander and `ActionQueue.Move`'s offset-based payload. Both are friction with no
payoff for an inline brain.

The buffering half instead fits the **player-command** path precisely: a `MoveAsync`/`AttackAsync`
RPC arrives asynchronously, is *enqueued*, and is *dispatched* on a later tick when the player's
budget covers it — which is exactly what `ActionQueue`/`ActionSystem` model, and is where the
"deferred action reports back to the client" question the pipeline's own design doc flagged actually
lives. So those two primitives are wired there, in the next slice, rather than shoehorned into NPCs.

### `TrySpend` as a component method, not inline grain code

`StepNpcsAsync`'s gate is four lines (refill, afford, deduct, or accrue-and-skip) — the same four
lines `ActionSystem.Tick` runs per dispatch. Rather than inline them in the grain (untestable without
a live grain) or reach for `ActionSystem` (which needs an `ActionQueue` this path doesn't use),
`ActionSpeed.TrySpend(cost)` captures them as a directly unit-testable method on the component that
owns the state. `ActionSystem` is left untouched (its own tests stay green); a future refactor could
have it call `TrySpend`, but that's out of scope.

### Default `ActionSpeed` on `Monster`, at parity

`Monster`'s constructor sets `ActionSpeed(speed: 1.0, maxBudget: 1.0)` — budget starts full, refills
to full, costs exactly `NpcActionCost (1.0)` per act — so a default monster affords every eligible
tick, identical to pre-pipeline behavior. This is what keeps `GameMapGrainCombatTests`/
`EndToEndSharedMutationTests` green. `Zombie : Monster` inherits it via `: base(world)`; `Snake :
Character` is not a `Monster` and is not ticked by `StepNpcsAsync`, so it needs nothing here.

## Risks / Coverage gaps

- **The gate is a no-op for every currently-spawnable monster** (all parity speed), so its live effect
  is latent until per-creature speeds exist. The differential-cadence *mechanism* is covered by unit
  tests (`ActionSystemTests.TrySpend_*`), and *parity* by the existing grain tests, but there is no
  grain-level test that a genuinely-slow monster acts on a sub-cadence — `SpawnEntityRequest` has no
  speed field and adding one (a serialized Orleans contract change) purely for a test isn't
  warranted. This grain-level assertion should be added alongside `add-npc-behavior-trees` task 2.4,
  when a spawn path first produces a non-default-speed creature.
- **`TrySpend` refills only on ticks `StepNpcsAsync` actually runs.** With `NpcMoveIntervalTicks`
  (default 1) as an outer gate, a monster skipped by the interval doesn't refill that tick, so a
  non-1 interval scales accrual. Acceptable — the interval is a legacy coarse gate; `ActionSpeed` is
  the intended finer-grained mechanism going forward.
