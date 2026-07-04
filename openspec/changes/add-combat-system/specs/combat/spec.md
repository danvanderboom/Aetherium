## ADDED Requirements

### Requirement: Melee Attack Resolution
The system SHALL resolve a melee attack from one entity against an adjacent target, applying damage to the target's health and removing the target when its health reaches zero. An attack SHALL be rejected when the target does not exist, is the attacker itself, is out of reach, or has no health.

#### Scenario: A non-lethal attack reduces the target's health
- **WHEN** an entity attacks an adjacent target that has health greater than the attack's damage
- **THEN** the target's health is reduced by the damage
- **AND** the target remains in the world and a health-change delta is broadcast

#### Scenario: A lethal attack defeats and removes the target
- **WHEN** an attack reduces an adjacent target's health to zero or below
- **THEN** the target is removed from the world and an entity-removed delta is broadcast
- **AND** the result reports the target as defeated

#### Scenario: Invalid attacks are rejected
- **WHEN** the target does not exist, is the attacker itself, is not adjacent (including on the Z axis), or has no health component
- **THEN** the attack fails without mutating the world

### Requirement: Combat Drives Kill Objectives
When an attack defeats a target in a world that has a narrative, the system SHALL emit an `enemy_defeated` event so that `kill` quest objectives can progress and complete.

#### Scenario: Defeating an enemy progresses a kill objective
- **WHEN** a player's attack defeats a target and the world has an associated narrative
- **THEN** an `enemy_defeated` event carrying the target's type is emitted to the narrative pipeline

### Requirement: Combat State Detection
The context evaluator SHALL report an `in-combat` state when the actor is adjacent to a hostile entity that has health, rather than never reporting combat.

#### Scenario: Adjacency to a hostile reports in-combat
- **WHEN** the actor is adjacent to a hostile entity (e.g. a monster) that has health
- **THEN** the evaluated context includes the `in-combat` tag
