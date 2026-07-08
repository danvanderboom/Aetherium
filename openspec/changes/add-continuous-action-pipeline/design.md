## Context

Every actor today either resolves instantly (player commands, called directly from grain RPC methods) or moves on a *global* cadence (`SimulationOptions.NpcMoveIntervalTicks`). Neither models per-actor speed, and there is no shared place for "this action takes time" (a cast, a channel, a slow status effect) to live. The engine gap-analysis (§4.1) specs a per-actor action budget as the shared substrate for combat, abilities, movement, and NPC AI. This change ships that substrate as isolated, tested ECS primitives — see [proposal.md](proposal.md) for why wiring it into live `GameMapGrain` command paths is deliberately deferred to a Phase 2 change.

## Goals / Non-Goals

- Goals:
  - `ActionSpeed` and `ActionQueue` components following existing `Component` conventions (plain mutable POCO, `Aetherium.Components` namespace).
  - `ActionSystem.Tick(World)` that refills budgets and dispatches queued actions deterministically (same order every tick for a given `World.Entities` enumeration — no hidden RNG or wall-clock reads).
  - Dispatch is by delegation, not inheritance: `ActionSystem` takes a `CombatSystem` (existing) and a movement delegate, so Phase 2 can wire it into `GameMapGrain` without `ActionSystem` needing to know about grains, DTOs, or fan-out.
  - Full unit test coverage of budget refill, deferral-when-insufficient-budget, queue depth capping, and dispatch routing.
- Non-Goals (left to later phases / other roadmap items):
  - Wiring into `GameMapGrain.AttackAsync` / `StepNpcsAsync` / movement (Phase 2, separate change).
  - Interruptible/channeled actions, concentration checks (needs §4.2/§4.3 status-effect model first).
  - Semantic perception events for action execution (needs §4.10 content atlas first — a new `ActionExecutedEvent` would have nowhere well-typed to put its tags yet).

## Decisions

- **Budget and cost are `double`, not `int`.** Fractional speeds (e.g. a "hasted" actor at 1.5x) and fractional AP costs (a quick jab costing 0.5 AP) are common in speed-based systems; using `int` would force awkward scaling factors later. Mirrors no existing convention (this is a new component) so we pick the type that fits the domain.
- **Queue depth defaults to 1 (no buffering).** Matches §4.1's explicit multiplayer note: "queue caps (default depth 1 — no input buffering by default; enable per-game via config)." `ActionQueue` exposes `MaxDepth` so a future game config can raise it.
- **Dispatch via a small `IActionExecutor`-shaped delegate set, not a big interface.** `ActionSystem` is constructed with `Func<World, Entity, string, CombatResult>` for attacks and `Action<World, Entity, int, int>` for moves, rather than reaching into `CombatSystem`/`World` internals or requiring callers to implement an interface. Keeps `ActionSystem` a pure, stateless-service class like `CombatSystem`/`InteractionSystem`, and keeps Phase 2's wiring a one-line constructor call in `GameMapGrain`.
- **A deferred action stays at the head of the queue, not re-enqueued or dropped.** If budget is insufficient this tick, the action simply waits — consistent with §4.1's "if budget goes negative, action is deferred to the next tick that fills it."

## Risks / Trade-offs

- **Two parallel movement/combat paths until Phase 2 lands** (the new `ActionSystem` path, unused; the existing direct-call path, live). Mitigated by keeping `ActionSystem` additive — zero changes to `GameMapGrain`, `CombatSystem`, or any existing test in this change. `dotnet build`/`dotnet test` on the full solution should show no behavior change outside the new files.
- **Double-precision budget arithmetic could drift over long uptimes.** Not a concern at expected AP costs (whole or half-integer) and tick counts; revisit if a game defines exotic fractional speeds.

## Migration Plan

Additive only — no migration. Phase 2 (a separate, later change) will migrate `GameMapGrain.AttackAsync`/`StepNpcsAsync`/movement onto this substrate and will need its own design pass for how a *deferred* attack reports back to the calling client (today `AttackAsync` returns a result synchronously in the same RPC).

## Open Questions

- Should `ActionSpeed.MaxBudget` default to 1 (one action per full refill) or something higher to allow burst queuing once depth > 1 is configured? Deferred to Phase 2, where a real game (combat) picks concrete numbers.
