## ADDED Requirements

### Requirement: Behavior Tree Node Vocabulary
The system SHALL provide a behavior-tree engine with, at minimum, `Sequence`, `Selector`, `Parallel`, `Condition`, `Action`, `Wait`, `Random`, and `Utility` node kinds, each returning `Success`, `Failure`, or `Running` when ticked.

**Verified by:** `Aetherium.Test.Ai.BehaviorNodeTests.SequenceNode_OneFails_ReturnsFailure_AndResets`, `.SelectorNode_FirstSuccessWins_RemainingSkipped`, `.SequenceNode_RunningChild_ResumesAtSameIndexNextTick`, `.SequenceNode_AllSucceed_ReturnsSuccess`, `.SelectorNode_AllFail_ReturnsFailure`, `.ParallelNode_DefaultRequiresAllChildren_Success`, `.ParallelNode_OneFailure_MakesRequireAllImpossible_Fails`, `.ParallelNode_PartialRequiredSuccesses_SucceedsEvenWithOneFailure`, `.ConditionNode_MapsPredicateToSuccessOrFailure`, `.ActionNode_ReturnsWhateverTheDelegateReturns`, `.WaitNode_RunsForNTicks_ThenSucceeds`, `.WaitNode_ZeroTicks_SucceedsImmediately`, `.WaitNode_CanRunAgainAfterCompleting`, `.RandomSelectorNode_PicksOneChild_AndSticksWithItWhileRunning`, `.UtilitySelectorNode_PicksHighestScoringChild`

#### Scenario: Sequence fails fast and resets
- **WHEN** a `Sequence` node's first child returns `Failure`
- **THEN** the `Sequence` returns `Failure` without ticking any later child, and resumes from its first child on the next tick

#### Scenario: Selector stops at the first success
- **WHEN** a `Selector` node's first child returns `Success`
- **THEN** the `Selector` returns `Success` without ticking any later child

#### Scenario: A Running child is resumed, not restarted
- **WHEN** a composite node's active child returns `Running`
- **THEN** the composite node returns `Running`, and the next tick resumes at that same child rather than re-evaluating from the start

### Requirement: Per-NPC Behavior Tree Instance
Each NPC SHALL own its own `BehaviorTree` instance (including its own `Blackboard` and any composite nodes' execution-progress state), so multiple NPCs running the same tree structure do not interfere with each other's in-progress state.

**Verified by:** `Aetherium.Test.Ai.BehaviorNodeTests.TwoTreeInstances_FromTheSameStructure_DoNotShareRunningState`

#### Scenario: Two NPCs running the same tree structure do not share progress
- **WHEN** two separate `BehaviorTree` instances built from the same node structure are ticked independently, and one instance's `Sequence` node is mid-way through its children (`Running`)
- **THEN** the other instance's equivalent `Sequence` node is unaffected and starts from its own first child

### Requirement: Worked Example Reproduces Current Monster Behavior
The engine SHALL ship at least one worked-example tree that reproduces the engine's existing inline monster decision (attack an adjacent target if one exists, else wander) using the live combat and movement systems, demonstrating the engine can express real, already-shipped game logic.

**Verified by:** `Aetherium.Test.Ai.MonsterBehaviorsTests.Tick_PlayerAdjacent_Attacks_NotWander`, `.Tick_NoPlayerAdjacent_Wanders`

#### Scenario: Adjacent target is attacked instead of wandered past
- **WHEN** the worked-example tree ticks for a monster with a `Health`-bearing entity within Manhattan distance 1
- **THEN** the tree resolves an attack against that entity and does not move the monster that tick

#### Scenario: No adjacent target falls through to wandering
- **WHEN** the worked-example tree ticks for a monster with no `Health`-bearing entity within Manhattan distance 1
- **THEN** the tree attempts the monster's wander movement
