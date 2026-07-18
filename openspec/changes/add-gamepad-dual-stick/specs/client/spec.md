## ADDED Requirements

### Requirement: Dual-Stick Gamepad Controls
The Unity client SHALL provide a dual-thumbstick Xbox control scheme in which the left stick performs
heading-relative movement, the right stick performs turning and level changes, and face buttons perform
context actions. Keyboard bindings SHALL remain available.

#### Scenario: Left stick moves forward/backward relative to heading
- **WHEN** the player pushes the left stick up (or down)
- **THEN** the client SHALL execute the `move` tool with a relative direction of Forward (`F`) for up and
  Backward (`B`) for down
- **AND** movement SHALL be relative to the character's current heading, not an absolute compass direction

#### Scenario: Left stick strafes
- **WHEN** the player pushes the left stick left (or right)
- **THEN** the client SHALL execute the `move` tool with a relative direction of Left (`L`) for left and
  Right (`R`) for right (strafe)
- **AND** the client SHALL suppress diagonals by selecting the dominant relative axis

#### Scenario: Right stick turns
- **WHEN** the player pushes the right stick left (or right)
- **THEN** the client SHALL execute the `rotate` tool counter-clockwise for left and clockwise for right
- **AND** the turn input SHALL be treated as an analog axis (magnitude expresses intent, capped by the
  character's action cadence)

#### Scenario: Right stick changes level
- **WHEN** the player pushes the right stick up (or down)
- **THEN** the client SHALL execute the `changelevel` tool with up=true for up and up=false for down

#### Scenario: X performs a context get-or-use
- **WHEN** the player presses the X button (buttonWest)
- **AND** a carriable item is present on the player's current tile
- **THEN** the client SHALL execute the `pickup` tool
- **WHEN** the player presses X and no carriable item is present
- **THEN** the client SHALL execute the `use` tool (which MAY enter the multi-option selection flow)

#### Scenario: Supporting face buttons
- **WHEN** the player presses A (buttonSouth)
- **THEN** the client SHALL perform the context interact/confirm action (e.g. open, board, or confirm an option)
- **WHEN** the player presses B (buttonEast)
- **THEN** the client SHALL perform cancel/back (including exiting option-selection mode)
- **WHEN** the player presses Y (buttonNorth)
- **THEN** the client SHALL perform the configured secondary action (e.g. drop or inspect)

### Requirement: Piloting Control Context
The Unity client SHALL track a control context of Avatar or Piloting. In the Piloting context, movement input
SHALL drive the controlled vehicle in three dimensions rather than the avatar, and an altitude gauge SHALL be
shown.

#### Scenario: Entering a pilot seat switches context
- **WHEN** the player takes a vehicle's pilot seat
- **THEN** the client SHALL switch the control context to Piloting
- **AND** movement tools SHALL target the controlled vehicle entity rather than the avatar
- **AND** the client SHALL display an altitude gauge for the vehicle

#### Scenario: Piloting drives the vehicle in 3D
- **WHEN** the control context is Piloting
- **THEN** the left stick SHALL apply thrust (forward/back) and strafe, the right stick X SHALL yaw (turn),
  and the right stick Y SHALL climb/descend the vehicle through altitude bands

#### Scenario: Leaving the seat restores avatar control
- **WHEN** the player exits the pilot seat (e.g. presses B)
- **THEN** the client SHALL switch the control context back to Avatar
- **AND** movement input SHALL again drive the avatar
