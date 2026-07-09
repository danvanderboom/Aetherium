## 1. Live wiring (this change — resolves `add-npc-behavior-trees` tasks 2.1–2.2)

- [x] 1.1 `GameMapGrain._monsterTrees`: one cached `BehaviorTree` instance per live monster, pruned when a monster leaves `_world.Entities`
- [x] 1.2 `StepNpcsAsync` ticks each monster's tree instead of the inline attack/wander `if/else`
- [x] 1.3 Fix monster-vs-monster targeting: `MonsterBehaviors.FindAdjacentTarget` scoped to a blackboard-supplied target list (joined players), falling back to the old any-`Health`-entity scan when the caller doesn't supply one
- [x] 1.4 Outcome reporting (`AttackOutcome`/`WanderOutcome` blackboard writes) so `GameMapGrain` can still emit its existing delta shapes
- [x] 1.5 Remove `GameMapGrain.FindAdjacentPlayer` (superseded by the tree's own target scoping)
- [x] 1.6 Integration test: two adjacent monsters with no player nearby never attack each other
- [x] 1.7 Cross-link the updated/added requirement(s) with `**Verified by:**` lines
- [x] 1.8 Full regression suite green (existing `EndToEndSharedMutationTests`, `GameMapGrainCombatTests`, `NpcBehaviorAndPerceptionTests` must be unaffected) — confirmed at commit `301254e` (1178 passed, 0 failed)

## 2. Still open (tracked here, not resolved by this change)

- [x] 2.1 Switch monster attacks from `CombatSystem.TryAttack` to `DamagePipeline` (`add-npc-behavior-trees` task 2.3) — **partially resolved** by `wire-combat-pipeline-live`: player→monster attacks now route through `DamagePipeline`. Monster→player retaliation (`MonsterBehaviors.BuildWanderAndMeleeTree`'s attack node, still called from here) deliberately stays on `CombatSystem.TryAttack` — a downed player entering the Dying/Corpse lifecycle needs its own design pass tied to `add-death-respawn-policy` (see `wire-combat-pipeline-live/design.md`).
- [ ] 2.2 Distinct trees per creature type (`Snake`/`Zombie`) instead of `Monster`'s one generic tree (`add-npc-behavior-trees` task 2.4)
- [ ] 2.3 Populate `Blackboard` from real Perception data (`add-npc-behavior-trees` task 2.5)
- [ ] 2.4 Wire `ThreatTable.GetTopThreat()` into target selection (`add-npc-behavior-trees` task 2.6)
- [ ] 2.5 Persist `_monsterTrees`' state (or accept reset-on-reactivation) once a tree can meaningfully be `Running` across ticks
