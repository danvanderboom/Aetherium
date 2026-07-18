## ADDED Requirements
### Requirement: Held-Key Repeat (Console)
The console client SHALL re-issue a held movement key at the character's cadence until the key is released.

#### Scenario: Held key repeats at cadence until released
- **WHEN** the player holds a movement key down
- **THEN** the console client SHALL re-issue the corresponding `move` action once per cadence interval
- **AND** SHALL stop re-issuing once the key is released or a key-repeat grace window elapses with no further key event
