## ADDED Requirements

### Requirement: Server-Authoritative Game State
The game engine SHALL run on a dedicated server process, maintaining the authoritative World state, entities, and game logic.

#### Scenario: Server hosts complete game world
- **WHEN** server starts
- **THEN** it SHALL initialize a complete World with terrain, entities, and systems
- **AND** no client SHALL have direct access to the World object

#### Scenario: Server processes all game logic
- **WHEN** a player action is received
- **THEN** the server SHALL validate and execute the action
- **AND** the server SHALL compute resulting state changes
- **AND** the server SHALL determine updated perception for affected clients

### Requirement: Perception-Based Client Updates
The server SHALL send clients only perception data representing what their player character can perceive (see, hear, feel).

#### Scenario: Client receives visible tiles only
- **WHEN** server computes perception for a player location
- **THEN** it SHALL include only tiles within field-of-view
- **AND** it SHALL exclude tiles blocked by obstacles or darkness
- **AND** it SHALL apply lighting calculations to determine visibility

#### Scenario: Perception respects FOV and lighting
- **WHEN** a tile is outside FOV range
- **THEN** it SHALL NOT be included in PerceptionDto
- **WHEN** a tile has insufficient lighting
- **THEN** it SHALL NOT be visible beyond a minimum distance

#### Scenario: Perception includes location and heading
- **WHEN** perception is computed
- **THEN** it SHALL include player's current WorldLocation
- **AND** it SHALL include player's current heading direction
- **AND** it SHALL include the visible bounds rectangle

### Requirement: SignalR Real-Time Communication
The system SHALL use SignalR for bidirectional real-time communication between client and server.

#### Scenario: Client connects to server hub
- **WHEN** client application starts
- **THEN** it SHALL establish a SignalR connection to the GameHub
- **AND** it SHALL receive initial game state
- **AND** it SHALL receive initial perception data

#### Scenario: Server sends perception updates
- **WHEN** player state changes (moves, rotates, etc.)
- **THEN** server SHALL invoke ReceivePerceptionUpdate on the client
- **AND** client SHALL render the updated perception

#### Scenario: Client sends player commands
- **WHEN** player presses arrow keys or control keys
- **THEN** client SHALL invoke hub methods (MovePlayer, RotatePlayer, etc.)
- **AND** server SHALL process the command and send updated perception

### Requirement: Session Management
The server SHALL maintain separate game sessions for each connected client.

#### Scenario: New client creates session
- **WHEN** a client connects to GameHub
- **THEN** server SHALL create a new GameSession for that connection
- **AND** SHALL initialize a World for that session
- **AND** SHALL assign a unique session ID

#### Scenario: Disconnected client cleanup
- **WHEN** a client disconnects
- **THEN** server SHALL remove the GameSession
- **AND** SHALL clean up World resources
- **AND** SHALL not affect other active sessions

### Requirement: Command Processing
The server SHALL accept player commands via hub methods and process them in the game engine.

#### Scenario: Move command execution
- **WHEN** client sends MovePlayer(direction, distance)
- **THEN** server SHALL update player's ViewLocation in the game world
- **AND** SHALL compute new perception for that location
- **AND** SHALL send ReceivePerceptionUpdate to the client

#### Scenario: Rotate command execution
- **WHEN** client sends RotatePlayer(clockwise)
- **THEN** server SHALL update player's heading
- **AND** SHALL compute perception from new heading
- **AND** SHALL send updated perception to client

### Requirement: Shared Data Transfer Objects
The system SHALL define serializable DTOs in a shared model library for client-server communication.

#### Scenario: PerceptionDto structure
- **WHEN** perception is serialized
- **THEN** PerceptionDto SHALL include:
  - PlayerLocation (WorldLocationDto)
  - PlayerHeading (WorldDirection enum)
  - Dictionary of visible locations to VisualDto
  - VisibleBounds rectangle
  - Dictionary of TileTypes for rendering

#### Scenario: VisualDto structure
- **WHEN** a visible location is serialized
- **THEN** VisualDto SHALL include:
  - Location (WorldLocationDto)
  - Terrain TileType
  - Light level (0.0 to 1.0)
  - Things seen counts by type

### Requirement: Identical Gameplay Experience
The client-server architecture SHALL provide gameplay identical to the original single-process version.

#### Scenario: All controls work identically
- **WHEN** player uses arrow keys, Z/X rotation, U/D level change
- **THEN** movement and rotation SHALL behave identically to single-process version
- **AND** rendering SHALL match original appearance
- **AND** FOV and lighting SHALL produce identical visibility

#### Scenario: Performance is acceptable
- **WHEN** player moves or rotates
- **THEN** perception update SHALL arrive within 100ms
- **AND** rendering SHALL feel responsive
- **AND** there SHALL be no perceptible lag

### Requirement: Client Rendering from Perception
The client SHALL render the console view using only PerceptionDto data without direct World access.

#### Scenario: Client renders from perception
- **WHEN** client receives PerceptionDto
- **THEN** it SHALL render only the tiles present in Visuals dictionary
- **AND** SHALL apply light levels to tile colors
- **AND** SHALL display player character at PlayerLocation
- **AND** SHALL not attempt to access World object

#### Scenario: Client handles missing perception
- **WHEN** perception has not yet been received
- **THEN** client SHALL display empty/loading view
- **AND** SHALL not crash or render invalid data

