## MODIFIED Requirements

### Requirement: Worked Example Reproduces Current Monster Behavior
The engine SHALL ship at least one worked-example tree that reproduces the engine's existing inline monster decision (attack an adjacent target if one exists, else wander) using the live combat and movement systems, demonstrating the engine can express real, already-shipped game logic. Adjacency-based target selection SHALL be scoped to a caller-supplied target list (e.g. joined players) when one is provided via the tree's `Blackboard`, so that live wiring can prevent monsters from targeting each other; when no target list is supplied, the tree SHALL fall back to matching any `Health`-bearing entity in range.

**Verified by:** `Aetherium.Test.Ai.MonsterBehaviorsTests.Tick_PlayerAdjacent_Attacks_NotWander`, `.Tick_NoPlayerAdjacent_Wanders`, `Aetherium.Test.Combat.GameMapGrainCombatTests.Tick_TwoAdjacentMonsters_NoPlayerNearby_DoNotAttackEachOther`

#### Scenario: Adjacent target is attacked instead of wandered past
- **WHEN** the worked-example tree ticks for a monster with a `Health`-bearing entity within Manhattan distance 1 and no target list is supplied
- **THEN** the tree resolves an attack against that entity and does not move the monster that tick

#### Scenario: No adjacent target falls through to wandering
- **WHEN** the worked-example tree ticks for a monster with no `Health`-bearing entity within Manhattan distance 1
- **THEN** the tree attempts the monster's wander movement

#### Scenario: A scoped target list excludes non-target entities from adjacency matching
- **WHEN** the worked-example tree ticks for a monster with another monster (but no scoped-in target) within Manhattan distance 1, and a target list has been supplied via the blackboard that does not include that monster
- **THEN** the tree does not attack the out-of-scope monster and falls through to wandering instead

## ADDED Requirements

### Requirement: Live NPC Tick Delegates to Behavior Tree
`GameMapGrain.StepNpcsAsync` SHALL tick one `BehaviorTree` instance per live monster (built via `MonsterBehaviors.BuildWanderAndMeleeTree` and cached for the monster's lifetime on the map) instead of an inline attack/wander decision, satisfying the "Per-NPC Behavior Tree Instance" requirement in the live path. The tree's target list SHALL be scoped to the map's currently-joined players each tick. Cached tree instances for monsters no longer present in the world SHALL be discarded.

**Verified by:** `Aetherium.Test.Combat.GameMapGrainCombatTests.Tick_MonsterAdjacentToPlayer_Retaliates_DamagingButNotRemovingPlayer`, `.Tick_TwoAdjacentMonsters_NoPlayerNearby_DoNotAttackEachOther`, `Aetherium.Test.MultiWorld.EndToEndSharedMutationTests.Tick_Moves_Monsters_And_Fans_Out_Perception`

#### Scenario: A monster adjacent to a joined player attacks via its behavior tree
- **WHEN** `GameMapGrain.TickAsync` runs and a monster has a joined player within melee reach
- **THEN** the monster's cached behavior tree resolves an attack against that player through the existing `CombatSystem`, and the grain emits the same health-changed delta shape it emitted before this change

#### Scenario: A monster with no adjacent player wanders via its behavior tree
- **WHEN** `GameMapGrain.TickAsync` runs and a monster has no joined player within melee reach
- **THEN** the monster's cached behavior tree attempts a wander step, and the grain emits the same entity-moved delta shape it emitted before this change

#### Scenario: A monster's tree instance persists across ticks
- **WHEN** the same monster is ticked on two consecutive `TickAsync` calls
- **THEN** `GameMapGrain` reuses the same `BehaviorTree` instance (and therefore the same `Blackboard`) for that monster rather than constructing a new one each tick
