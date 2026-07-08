## MODIFIED Requirements

### Requirement: Action Budget
Any entity MAY carry an `ActionSpeed` component defining an action-point (AP) budget that refills by a fixed rate each world tick, capped at a maximum. The component SHALL provide a `TrySpend(cost)` operation that refills the budget for the tick and then, if the post-refill budget covers `cost`, deducts it and reports success; if the budget is insufficient the refilled AP SHALL be retained (accruing toward a later tick) and the operation SHALL report failure without deducting.

**Verified by:** `Aetherium.Test.Core.ActionSystemTests.ActionSpeed_Refill_AddsSpeed_CappedAtMax`, `.TrySpend_RefillsThenSpends_WhenAffordable`, `.TrySpend_Defers_WhenUnaffordable_ButRetainsRefilledAp`, `.TrySpend_HalfSpeedActor_AffordsEveryOtherTick`

#### Scenario: Budget refills each tick, capped at maximum
- **WHEN** an entity's `ActionSpeed` has `Budget = 0.3`, `Speed = 0.5`, `MaxBudget = 1.0`, and the world ticks once
- **THEN** `Budget` becomes `0.8`

#### Scenario: Refill does not exceed the maximum
- **WHEN** an entity's `ActionSpeed` has `Budget = 0.9`, `Speed = 0.5`, `MaxBudget = 1.0`, and the world ticks once
- **THEN** `Budget` becomes `1.0`, not `1.4`

#### Scenario: TrySpend spends when the refilled budget is affordable
- **WHEN** `TrySpend(1.0)` is called on an `ActionSpeed` with `Speed = 1.0`, `MaxBudget = 1.0`, `Budget = 0.0`
- **THEN** the budget refills to `1.0`, the cost is deducted leaving `0.0`, and the call reports success

#### Scenario: TrySpend defers when unaffordable but keeps the accrued AP
- **WHEN** `TrySpend(1.0)` is called on an `ActionSpeed` with `Speed = 0.5`, `MaxBudget = 1.0`, `Budget = 0.0`
- **THEN** the budget refills to `0.5`, no cost is deducted, `Budget` stays `0.5`, and the call reports failure

## ADDED Requirements

### Requirement: Live NPC Action Cadence
`GameMapGrain.StepNpcsAsync` SHALL gate each monster's per-tick behavior-tree action on that monster's `ActionSpeed` budget: the monster acts only on ticks where it can afford the per-action cost (spending it via `ActionSpeed.TrySpend`), and otherwise accrues budget and skips that tick, so an actor's `Speed` determines how often it acts with no global turn order. A monster with a default `ActionSpeed` (`Speed`, `MaxBudget`, and the per-action cost all equal) SHALL act every eligible tick, and a monster carrying no `ActionSpeed` component SHALL always act.

**Verified by:** `Aetherium.Test.Combat.GameMapGrainCombatTests.Tick_MonsterAdjacentToPlayer_Retaliates_DamagingButNotRemovingPlayer`, `.Tick_TwoAdjacentMonsters_NoPlayerNearby_DoNotAttackEachOther`, `Aetherium.Test.MultiWorld.EndToEndSharedMutationTests.Tick_Moves_Monsters_And_Fans_Out_Perception`, `Aetherium.Test.Core.ActionSystemTests.TrySpend_HalfSpeedActor_AffordsEveryOtherTick`

#### Scenario: A default-speed monster acts every eligible tick
- **WHEN** `GameMapGrain.TickAsync` runs and a monster carries the default `ActionSpeed` (`Speed == MaxBudget == per-action cost`)
- **THEN** the monster affords its action every eligible tick, so its behavior tree ticks and acts exactly as it would without a budget gate

#### Scenario: A slower monster acts on a sub-cadence
- **WHEN** a monster's `ActionSpeed.Speed` is a fraction of the per-action cost (e.g. half)
- **THEN** the monster affords an action only on a fraction of eligible ticks (e.g. every other one), accruing budget on the ticks it skips

#### Scenario: A monster without an ActionSpeed always acts
- **WHEN** `GameMapGrain.StepNpcsAsync` ticks a monster that carries no `ActionSpeed` component
- **THEN** the monster is never gated and its behavior tree ticks every eligible tick
