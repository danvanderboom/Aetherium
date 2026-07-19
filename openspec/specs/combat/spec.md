# combat Specification

## Purpose
TBD - created by archiving change add-combat-system. Update Purpose after archive.
## Requirements
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

### Requirement: Variable Attack Damage
The damage an entity deals per hit SHALL be its base attack power (from an `AttackPower` component, defaulting to the fixed melee damage when absent) plus the single highest weapon bonus among the items it carries. Weapon bonuses SHALL NOT stack — only the strongest carried weapon applies.

#### Scenario: An entity without an AttackPower component deals the default damage
- **WHEN** an attacker with no `AttackPower` component resolves an attack
- **THEN** the damage dealt equals the default melee damage

#### Scenario: A carried weapon adds its bonus to the base damage
- **WHEN** an attacker with base attack power carries one or more weapons
- **THEN** the damage dealt equals the base attack power plus the highest single weapon bonus, not the sum of all weapon bonuses

### Requirement: Monster Retaliation On The Tick
When NPC behavior is enabled, a monster that is within melee reach of a joined player at the start of a simulation tick SHALL attack that player instead of wandering, applying its attack damage and broadcasting the resulting health-change delta. A player reduced to zero health by retaliation SHALL be marked defeated but SHALL NOT be removed from the world (the entity is "downed" so its session and the map's spawn bookkeeping remain consistent).

#### Scenario: An adjacent monster attacks instead of moving
- **WHEN** a simulation tick runs and a monster is adjacent to a player
- **THEN** the monster attacks the player (reducing the player's health) and does not move that tick

#### Scenario: Lethal retaliation downs but does not remove the player
- **WHEN** monster retaliation reduces a player's health to zero
- **THEN** the attack reports the player defeated but the player entity remains in the world at zero health

### Requirement: Death Loot And Combat Analytics
When a player's attack defeats a monster, the system SHALL drop a carriable weapon at the monster's last location (broadcast as an entity-placed delta) and SHALL update the map's rolling combat analytics. The map SHALL track the number of monsters defeated and the total damage players have dealt, persisted with the map and queryable.

#### Scenario: Defeating a monster drops loot where it fell
- **WHEN** a player's attack defeats a monster
- **THEN** a carriable weapon entity is placed at the monster's last location and its id/type are reported in the attack result

#### Scenario: Combat analytics accumulate per map
- **WHEN** attacks are resolved on a map
- **THEN** the map's total-damage counter increases by each hit's damage, and its monsters-defeated counter increases on each monster kill, and both are readable via a combat-stats query

