## Why

`add-continuous-action-pipeline` (Wave 0, §4.1) shipped the `ActionSpeed`/`ActionQueue`/
`ActionSystem` primitives — a speed-based, no-global-turn-order action model — but left them entirely
test-only, dispatched only through injected delegates in `ActionSystemTests`. This change is the
second slice of the Phase 2 live-wiring pass (following `wire-npc-behavior-trees-live`), bringing the
pipeline's headline capability — **each actor acts at its own cadence, driven by an accruing AP
budget, with no global turn order** — into the live NPC tick.

It is deliberately scoped to **NPCs and to `ActionSpeed` only**. The behavior tree wired live in the
previous slice already *acts inline* (per the `npc-behavior-trees` spec), so the natural integration
for NPCs is to gate that inline action on an accruing budget — not to restructure the tree into an
enqueue-then-dispatch model. The `ActionQueue`/`ActionSystem` dispatch half of the pipeline fits the
**player-command** path instead (a command arrives asynchronously and resolves on a tick — exactly
the enqueue-when-received, dispatch-when-affordable shape), so those two primitives are wired in the
subsequent player-command slice, not forced awkwardly onto NPCs here. See Design.

## What Changes

- `ActionSpeed` gains a `TrySpend(cost)` convenience: refill this tick, then spend-and-return-true if
  affordable, else retain the accrued AP and return false. This is the single-actor equivalent of one
  `ActionSystem` dispatch step, for callers that gate an inline action on cadence rather than routing
  through an `ActionQueue`.
- `Monster` (and therefore `Zombie : Monster`) is constructed with a default `ActionSpeed`
  (`Speed == MaxBudget == 1.0`), so it affords an action every eligible tick — **parity with
  pre-pipeline behavior**.
- `GameMapGrain.StepNpcsAsync` gates each monster's behavior-tree tick on
  `ActionSpeed.TrySpend(NpcActionCost)`: a monster acts only on ticks where it can afford the cost,
  otherwise it accrues budget and skips. A monster without an `ActionSpeed` (none today) always acts.

## Impact

- Affected code: `Aetherium.Server/Components/ActionSpeed.cs` (adds `TrySpend`),
  `Aetherium.Server/Entities/Monster.cs` (default `ActionSpeed`),
  `Aetherium.Server/MultiWorld/GameMapGrain.cs` (`StepNpcsAsync` gate + `NpcActionCost`).
- Affected specs: `engine-core` (MODIFIED "Action Budget" for `TrySpend`; ADDED "Live NPC Action
  Cadence").
- No behavior change for any currently-spawnable monster: all default to parity speed, so the change
  installs the cadence mechanism without altering how often today's monsters act. Differentiating
  specific creatures' speeds (e.g. a slow zombie) is a follow-up that lands with per-creature trees
  (`add-npc-behavior-trees` task 2.4).
- `ActionQueue` and `ActionSystem` remain unwired by this change — deferred to the player-command
  slice by design, not oversight.
