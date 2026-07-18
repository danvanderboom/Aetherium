## ADDED Requirements
### Requirement: Held-Input Repeat (Unity)
While a movement input is held, the Unity client SHALL re-issue the action every cadence interval — for `move`, `rotate`, and `changelevel` — rather than once per press. Repeat SHALL be suppressed while the client is in option-selection mode.

#### Scenario: Holding forward moves repeatedly at the character rate
- **WHEN** the player holds a movement input (e.g., forward) without releasing it
- **THEN** the client SHALL re-issue the corresponding `move` action once per cadence interval
- **AND** SHALL pace repeats to the character's cadence surfaced by the server, falling back to a sane default until the first perception arrives

#### Scenario: Releasing stops the repeat
- **WHEN** the player releases the held movement input
- **THEN** the client SHALL stop re-issuing the action

#### Scenario: Rotate and change-level also repeat while held
- **WHEN** the player holds a rotate or change-level input without releasing it
- **THEN** the client SHALL re-issue that `rotate` or `changelevel` action once per cadence interval until the input is released

#### Scenario: Disabled while choosing an option
- **WHEN** the client is in option-selection mode
- **AND** a movement, rotate, or change-level input is held
- **THEN** the client SHALL NOT re-issue any of those actions until option-selection mode exits
