## 1. Behavior-tree engine (Phase 1 — this change)

- [x] 1.1 `BehaviorStatus`, `BehaviorContext`, `Blackboard`, `BehaviorNode` base
- [x] 1.2 Leaf nodes: `ConditionNode`, `ActionNode`, `WaitNode`
- [x] 1.3 Composite nodes: `SequenceNode`, `SelectorNode`, `ParallelNode` (configurable required-successes), `RandomSelectorNode`, `UtilitySelectorNode`
- [x] 1.4 `BehaviorTree` wrapper (root node + blackboard, `Tick(world, self)`)
- [x] 1.5 `MonsterBehaviors.BuildWanderAndMeleeTree`: worked example reproducing `StepNpcsAsync`'s current attack-if-adjacent-else-wander rule against the live `CombatSystem`/`Monster.NextWanderDirection`
- [x] 1.6 Unit tests for every node type's Success/Failure/Running/resume semantics (17 tests) + integration tests for the worked example
- [x] 1.7 `openspec/specs/npc-behavior-trees/spec.md` delta: ADDED requirements
- [x] 1.8 Cross-link every requirement with a `**Verified by:**` line naming the test(s) that cover it (added `TwoTreeInstances_FromTheSameStructure_DoNotShareRunningState` to close a coverage gap found during this pass)

## 2. Live wiring (Phase 2 — separate follow-up change, not started here)

- [ ] 2.1 Give each `Monster` (and eventually `Snake`/`Zombie`) a `BehaviorTree` instance, likely stored alongside the entity or in a parallel per-monster dictionary on `GameMapGrain`
- [ ] 2.2 Replace `StepNpcsAsync`'s inline attack/wander decision with `tree.Tick(world, monster)`
- [ ] 2.3 Decide whether the worked example keeps calling `CombatSystem.TryAttack` or switches to `DamagePipeline` (from `deepen-combat-model`) once that's wired live
- [ ] 2.4 Give `Snake`/`Zombie` distinct trees instead of inheriting/lacking `Monster`'s generic one
- [ ] 2.5 Populate `Blackboard` from real Perception data once a tree needs more than direct `World`/`Entity` queries
- [ ] 2.6 Wire `ThreatTable.GetTopThreat()` (from `deepen-combat-model`) into a monster's target-selection `ConditionNode`/`ActionNode`
