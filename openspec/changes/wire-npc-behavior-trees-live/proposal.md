## Why

`add-npc-behavior-trees` (Wave 0, §4.5) shipped a fully-tested, generic behavior-tree engine and a
`MonsterBehaviors.BuildWanderAndMeleeTree` worked example proven to reproduce
`GameMapGrain.StepNpcsAsync`'s inline attack-if-adjacent-else-wander decision — but explicitly left
it unwired, tracked as an open "Phase 2" task. The engine gap-analysis roadmap's second wave is now
implemented and pushed to `develop`; this change is the first slice of the follow-up live-wiring
pass (action-pipeline → combat → abilities → NPC-AI spine), starting with NPC-AI because it is the
lowest-risk of the four: the worked example already targets the exact same live `CombatSystem`,
`World.TryMoveSteps`, and `Monster.NextWanderDirection` the inline code used, so swapping one for the
other should be — and, per the tests added here, is — externally behavior-identical for a solo
monster near a player.

## What Changes

- `GameMapGrain.StepNpcsAsync` now ticks one cached `BehaviorTree` per live monster (built via
  `MonsterBehaviors.BuildWanderAndMeleeTree`) instead of an inline `if/else`, satisfying the
  "Per-NPC Behavior Tree Instance" requirement in the live path, not just in unit tests.
- Fixes a latent scoping gap in the Wave 0 worked example: `MonsterBehaviors.FindAdjacentTarget`
  previously matched *any* `Health`-bearing entity, including other monsters. The live wiring
  supplies the tree a `Targets` blackboard list scoped to joined players only (mirroring the grain's
  retired `FindAdjacentPlayer` helper), so monsters can no longer target each other. A new
  integration test (`Tick_TwoAdjacentMonsters_NoPlayerNearby_DoNotAttackEachOther`) pins this.
- The tree still calls the existing `CombatSystem.TryAttack`, not `DamagePipeline` — deferred per
  task 2.3, kept out of scope here (see Design).
- `MonsterBehaviors` gained an outcome-reporting mechanism (`AttackOutcome`/`WanderOutcome` written
  to the tree's `Blackboard`) so `GameMapGrain` can build its existing `EntityMovedDelta`/
  `ComponentFieldChangedDelta` broadcasts without re-deriving what the tree just did.

## Impact

- Affected code: `Aetherium.Server/MultiWorld/GameMapGrain.cs` (`StepNpcsAsync`, removes the private
  `FindAdjacentPlayer` helper), `Aetherium.Server/Ai/MonsterBehaviors.cs`.
- Affected specs: `npc-behavior-trees` (MODIFIED: "Worked Example Reproduces Current Monster
  Behavior" scoping; ADDED: "Live NPC Tick Delegates to Behavior Tree").
- No client-visible contract change: `TickAsync`'s delta shapes are unchanged, only how they're
  computed internally.
- `LocalMutationGateway` (the legacy in-process session path) is **not** touched by this change —
  it has no NPC tick loop today, so there is nothing to wire there.
