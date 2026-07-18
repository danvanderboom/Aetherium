## ADDED Requirements

### Requirement: Asset Action Command Channel
The server SHALL deliver asset-action commands to clients alongside perception, where each command carries an entity id, an action name, and a parameter set. A client that receives an action name it does not support SHALL ignore it gracefully as a no-op, without raising an error.

#### Scenario: Server pushes an action to clients in range
- **WHEN** the server emits an asset action for an entity
- **THEN** every client whose player is in range of that entity SHALL receive an asset-action command containing the entity id, the action name, and its parameters, delivered alongside the perception update

#### Scenario: Unsupported action is a no-op
- **WHEN** a client receives an asset-action command whose action name it does not recognize
- **THEN** the client SHALL ignore the command
- **AND** SHALL NOT raise an error or interrupt rendering

### Requirement: Asset Action Vocabulary
The system SHALL define an extensible set of asset actions comprising at least `scale`/`grow`/`shrink`, `playAnimation`, `tint`/`flash`, `attachEffect`, `playSound`, `show`/`hide`, and `setSprite`. New action names MAY be added without breaking existing clients, because unrecognized actions are ignored per the command-channel contract.

#### Scenario: A scale action animates an entity's size
- **WHEN** a client receives a `scale` action carrying a target size and a duration for an entity
- **THEN** the client SHALL animate that entity's rendered size toward the target size over the given duration

### Requirement: ECA Runtime Rules
The system SHALL evaluate data-authored Event-Condition-Action rules of the form `WHEN <event> IF <condition> THEN <actions>` against runtime gameplay events — including band entry, takeoff/landing, boarding, station dwell, interaction, collision, and timers — and SHALL emit the resulting asset actions through the asset-action command channel.

#### Scenario: A landing event triggers a grow and animation
- **WHEN** a vehicle raises a landing event and a rule's condition on its flight state is satisfied
- **THEN** the engine SHALL emit a `scale` (grow) action and a `playAnimation` action targeting that vehicle

#### Scenario: A hack triggers a tint pulse
- **WHEN** an interaction event indicates an entity is being hacked and the matching rule's condition holds
- **THEN** the engine SHALL emit a `tint`/`flash` action that pulses that entity

### Requirement: Server-Authoritative Actions
Asset actions that change authoritative state — most importantly a `scale` that changes an entity's footprint — SHALL be applied on the server, which SHALL re-index occupancy before broadcasting the action to clients. Purely cosmetic actions SHALL remain client-side and MUST NOT alter authoritative state.

#### Scenario: A footprint-changing scale re-indexes occupancy before broadcast
- **WHEN** the engine emits a `scale` action that enlarges an entity's footprint
- **THEN** the server SHALL apply the new footprint and re-index occupancy for the affected tiles
- **AND** SHALL broadcast the action to clients in range only after occupancy has been re-indexed

#### Scenario: A cosmetic action stays client-side
- **WHEN** the engine emits a purely cosmetic action such as `tint` or `playSound`
- **THEN** the server SHALL NOT modify authoritative state
- **AND** the action SHALL be executed only on the clients that receive it
