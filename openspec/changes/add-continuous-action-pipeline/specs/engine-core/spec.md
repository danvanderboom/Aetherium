## ADDED Requirements

### Requirement: Action Budget
Any entity MAY carry an `ActionSpeed` component defining an action-point (AP) budget that refills by a fixed rate each world tick, capped at a maximum.

**Verified by:** `Aetherium.Test.Core.ActionSystemTests.ActionSpeed_Refill_AddsSpeed_CappedAtMax`

#### Scenario: Budget refills each tick, capped at maximum
- **WHEN** an entity's `ActionSpeed` has `Budget = 0.3`, `Speed = 0.5`, `MaxBudget = 1.0`, and the world ticks once
- **THEN** `Budget` becomes `0.8`

#### Scenario: Refill does not exceed the maximum
- **WHEN** an entity's `ActionSpeed` has `Budget = 0.9`, `Speed = 0.5`, `MaxBudget = 1.0`, and the world ticks once
- **THEN** `Budget` becomes `1.0`, not `1.4`

### Requirement: Action Queue
Any entity MAY carry an `ActionQueue` component holding at most `MaxDepth` pending `QueuedAction`s (default `MaxDepth = 1`); enqueuing beyond the depth cap is rejected rather than silently dropping an existing entry.

**Verified by:** `Aetherium.Test.Core.ActionSystemTests.ActionQueue_Enqueue_RejectsBeyondMaxDepth`

#### Scenario: Enqueue succeeds under the depth cap
- **WHEN** an `ActionQueue` with `MaxDepth = 1` is empty and a caller enqueues one `QueuedAction`
- **THEN** the queue holds that action and reports it via `TryPeek`

#### Scenario: Enqueue is rejected at the depth cap
- **WHEN** an `ActionQueue` with `MaxDepth = 1` already holds one `QueuedAction` and a caller attempts to enqueue a second
- **THEN** the enqueue returns failure and the original queued action is unchanged

### Requirement: Action Tick Scheduling
Each world tick, the `ActionSystem` SHALL refill every entity's `ActionSpeed` budget, then for every entity with a non-empty `ActionQueue` whose head action's AP cost is covered by the (post-refill) budget, dispatch that action (subtracting its cost from the budget and removing it from the queue); an action whose cost exceeds the available budget SHALL remain queued, unchanged, for a later tick.

**Verified by:** `Aetherium.Test.Core.ActionSystemTests.Tick_DispatchesAffordableAction_AndRemovesFromQueue`, `.Tick_DefersUnaffordableAction_QueueAndBudgetUnchanged`, `.Tick_RefillsBudget_BeforeCheckingAffordability`, `.Tick_AttackAction_DispatchesThroughCombatDelegate_WithCorrectTarget`, `.Tick_EntityWithoutActionSpeed_IsIgnored`

#### Scenario: Affordable action is dispatched and removed from the queue
- **WHEN** an entity has `ActionSpeed.Budget = 1.0` and an `ActionQueue` head action costing `0.5` AP, and the system ticks
- **THEN** the action is dispatched, the queue is empty afterward, and `Budget` is `0.5` (after refill, before the cost is subtracted)

#### Scenario: Unaffordable action is deferred, not dropped
- **WHEN** an entity has `ActionSpeed.Budget = 0.2` (post-refill) and an `ActionQueue` head action costing `1.0` AP, and the system ticks
- **THEN** the action remains at the head of the queue, `Budget` is unchanged by the dispatch step, and no dispatch delegate is invoked

#### Scenario: Attack actions dispatch through the combat system
- **WHEN** a queued action of kind `Attack` targeting entity `E` becomes affordable
- **THEN** `ActionSystem` invokes the injected combat-resolution delegate with the acting entity and target id `E`, and returns its `CombatResult` as the dispatch outcome

#### Scenario: Move actions dispatch through the movement delegate
- **WHEN** a queued action of kind `Move` with offset `(dx, dy)` becomes affordable
- **THEN** `ActionSystem` invokes the injected movement delegate with the acting entity and `(dx, dy)`
