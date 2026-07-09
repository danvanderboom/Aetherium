## Why

Aetherium's vision is continuous, speed-based simulation — "no your-turn/my-turn ordering, every actor acts independently against one clock" — but today nothing enforces it. Player attacks (`GameMapGrain.AttackAsync`) resolve synchronously and unthrottled the instant a client calls them; monster attacks/moves are gated only by a *global* `NpcMoveIntervalTicks`, not a per-actor speed. There is no shared substrate an actor's speed, an ability's cast time, or a future "slow" status effect could hook into. The [2026-07-06 engine gap-analysis](../../docs/audits/2026-07-06-engine-gap-analysis/design-next-steps.md) (§4.1) calls this out as the **P0 foundation** — combat depth, abilities, and NPC AI all assume it exists.

## What Changes

- Add an `ActionSpeed` component: per-actor action-point (AP) budget and refill rate.
- Add an `ActionQueue` component: an actor's next intended action (kind, target, AP cost), capped at depth 1 by default (no input buffering unless a game opts in).
- Add an `ActionSystem`: each world tick, refills every `ActionSpeed` budget and dispatches any queued action whose cost the budget covers, deferring the rest to a later tick.
- **Phase 1 (this change): ECS primitives only.** `ActionSpeed`, `ActionQueue`, `ActionSystem` are added and unit-tested in isolation. They are **not yet wired into `GameMapGrain`** — `AttackAsync` and `StepNpcsAsync` keep calling `CombatSystem`/movement directly for now.
- Phase 2 (follow-up change): wire `GameMapGrain.AttackAsync`, monster retaliation, and movement through `ActionQueue`/`ActionSystem` so live play actually budget-gates on `ActionSpeed`. Deferred because `AttackAsync` today bundles combat resolution with loot drops, analytics counters, and delta fan-out in one synchronous RPC — routing that through a queue that may defer to a *later* tick requires deciding how partial/deferred client feedback works, which deserves its own design pass rather than being bolted on here.

## Impact

- Affected specs: `engine-core` (new requirements: Action Budget, Action Queue, Action Tick Scheduling)
- Affected code: new `Aetherium.Server/Components/ActionSpeed.cs`, `Aetherium.Server/Components/ActionQueue.cs`, new `Aetherium.Server/Core/ActionSystem.cs`, new tests under `Aetherium.Test/Core/ActionSystemTests.cs`. No changes to `GameMapGrain`, `CombatSystem`, or any live command path in this change.
