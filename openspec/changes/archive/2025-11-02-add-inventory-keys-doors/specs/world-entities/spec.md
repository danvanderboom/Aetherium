## ADDED Requirements

### Requirement: Inventory
Characters and monsters SHALL have an inventory with a fixed capacity of 10 slots.

#### Scenario: Add item to inventory
- **WHEN** a carriable item is picked up
- **THEN** it occupies one slot if capacity available

#### Scenario: Capacity enforcement
- **WHEN** inventory is full
- **THEN** pickup fails with a capacity error

### Requirement: Items and Keys
Items SHALL be entities with `Carriable` metadata. Keys SHALL include a `keyId` string used to match lockable doors.

#### Scenario: Carriable items
- **WHEN** an item exists in the world
- **THEN** it has label/icon metadata and can be carried if carriable

#### Scenario: Keys match doors
- **WHEN** a key with `keyId` equals a door's `KeyShape`
- **THEN** the key can unlock/lock that door

### Requirement: Doors
Doors SHALL have open/closed/locked states and affect visibility and movement accordingly.

#### Scenario: Door open
- **WHEN** door is open
- **THEN** it does not obstruct view

#### Scenario: Door closed or locked
- **WHEN** door is closed or locked
- **THEN** it obstructs view

