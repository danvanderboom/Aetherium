## ADDED Requirements
### Requirement: Action Cadence
Each character SHALL have a maximum action rate (moves per second), authored per-world with a per-entity override. The server SHALL rate-limit or coalesce `move`, `rotate`, and `changelevel` actions to that rate, and SHALL surface the current cadence to the client. The same cadence SHALL pace autonomous flight-plan stepping.

#### Scenario: Early action is coalesced, not doubly applied
- **WHEN** a character's `move`, `rotate`, or `changelevel` action arrives before its cadence interval (`1 / MovesPerSecond`) has elapsed since the last accepted action
- **THEN** the server MUST coalesce or defer the action to the next eligible tick
- **AND** MUST NOT apply the action twice within a single interval

#### Scenario: Cadence is included in the client-facing payload
- **WHEN** the server builds the client-facing perception/HUD payload for a character
- **THEN** the payload MUST include that character's current cadence (or equivalent interval)

#### Scenario: Per-entity override of the per-world default
- **WHEN** a character is authored with a per-entity action-rate override
- **THEN** the server MUST enforce that character's overriding rate instead of the per-world default

#### Scenario: The same cadence paces autonomous flight-plan stepping
- **WHEN** an entity is advancing an autonomous flight plan
- **THEN** its flight-plan follower MUST advance at most one leg-step per cadence interval
- **AND** MUST use the same cadence that gates that entity's player-issued actions
