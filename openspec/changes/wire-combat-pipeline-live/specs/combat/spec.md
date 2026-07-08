## ADDED Requirements

### Requirement: Player Attacks Route Through DamagePipeline
`GameMapGrain.AttackAsync` SHALL resolve a player's attack against a target through `DamagePipeline.Resolve`, using an `AlwaysHitResolver` and `DeathPolicy.Default`, after independently validating target existence, self-attack, and melee reach (Manhattan distance ≤ 1). The damage amount SHALL be computed identically to the pre-pipeline melee MVP (`CombatSystem.ComputeAttackDamage`: base `AttackPower` plus the single best carried `Weapon` bonus). A lethal hit SHALL NOT remove the target from the world; the target SHALL enter the `Dying` state as `DamagePipeline` defines. A target already `Dying` or a `Corpse` SHALL reject a further attack.

**Verified by:** `Aetherium.Test.Combat.GameMapGrainCombatTests.Attack_DamagesThenDefeats_MonsterOnLiveMap`, `.Attack_KillingMonster_DropsLoot_AndRecordsStats`, `.Attack_KillingMonster_MonsterPersistsAsDying_NotRemovedFromWorld`

#### Scenario: Damage numbers are unchanged from the pre-pipeline melee MVP
- **WHEN** a player with the default `AttackPower` attacks an adjacent monster with no `Resistances`
- **THEN** the damage dealt equals `CombatSystem.ComputeAttackDamage`'s result, identical to the pre-pipeline value

#### Scenario: A lethal hit enters Dying instead of removing the target
- **WHEN** a hit reduces a monster's `Health` to `0`
- **THEN** the monster receives a `Dying` component, remains present in `_world.Entities`, and `AttackResultDto.TargetDefeated` reports `true`

#### Scenario: A Dying or Corpse target rejects further attacks
- **WHEN** `AttackAsync` targets an entity that already carries `Dying` or `Corpse`
- **THEN** the attack fails without mutating `Health` again

### Requirement: Live Death Lifecycle Bookkeeping
`GameMapGrain.TickAsync` SHALL tick `DeathSystem` (advancing every `Dying` entity's countdown, converting to `Corpse` on expiry) and `CorpseExpirySystem` (aging and removing `Corpse` entities per the active `DeathPolicy`) every world tick, independent of NPC-behavior gating.

**Verified by:** `Aetherium.Test.Combat.DeathSystemTests.Tick_CountdownReachesZero_TransitionsToCorpse` (mechanism, unit-level), `Aetherium.Test.Combat.CorpseExpirySystemTests` (mechanism, unit-level), `Aetherium.Test.Combat.GameMapGrainCombatTests.Attack_KillingMonster_MonsterPersistsAsDying_NotRemovedFromWorld` (live entry into the lifecycle)

#### Scenario: Death lifecycle ticks run every world tick
- **WHEN** `GameMapGrain.TickAsync` runs
- **THEN** it calls `DeathSystem.Tick(_world)` and `CorpseExpirySystem.Tick(_world, DeathPolicy)` unconditionally, without emitting any `MapDelta` for the transition itself

### Requirement: Live NPC Tick Skips Dying/Corpse Monsters
`GameMapGrain.StepNpcsAsync` SHALL NOT tick the behavior tree, spend the action budget, or otherwise act for a monster carrying `Dying` or `Corpse` — a killed monster that persists in the world (per `DamagePipeline`'s death-state transition) SHALL neither wander nor retaliate.

**Verified by:** `Aetherium.Test.Combat.GameMapGrainCombatTests.Tick_KilledMonster_NoLongerActs`

#### Scenario: A Dying monster does not wander
- **WHEN** `StepNpcsAsync` ticks a monster carrying `Dying`
- **THEN** the monster's location is unchanged after the tick

#### Scenario: A Dying monster does not retaliate against an adjacent player
- **WHEN** `StepNpcsAsync` ticks a monster carrying `Dying` that is adjacent to a joined player
- **THEN** the player takes no damage on that tick
