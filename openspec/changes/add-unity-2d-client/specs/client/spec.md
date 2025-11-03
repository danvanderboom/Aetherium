## ADDED Requirements
### Requirement: Unity 2D Client
The system SHALL provide a Unity 2D client for PC and iOS platforms that renders game state as a tilemap with Z-level support.

#### Scenario: Unity client renders tilemap
- **WHEN** Unity client receives PerceptionDto data
- **THEN** it SHALL render visible tiles to a Unity 2D Tilemap
- **AND** SHALL display a player marker sprite at PlayerLocation
- **AND** SHALL render only tiles on the current Z-level
- **AND** SHALL allow cycling through Z-levels via keyboard input

#### Scenario: Unity client handles input
- **WHEN** player presses WASD or Arrow keys
- **THEN** client SHALL execute "move" tool with appropriate direction
- **WHEN** player presses Z or X
- **THEN** client SHALL execute "rotate" tool (clockwise/counter-clockwise)
- **WHEN** player presses PageUp/PageDown or U/D
- **THEN** client SHALL execute "changelevel" tool to move up/down Z-levels

#### Scenario: Unity client displays HUD
- **WHEN** Unity client is running
- **THEN** it SHALL display current Z-level, player heading, and connection status
- **AND** SHALL show FPS or status message on screen

### Requirement: Unity Client Offline Mock Mode
The Unity client SHALL support an Offline Mock mode that replays Perception JSON sequences from local files.

#### Scenario: Offline mode loads Perception frames
- **WHEN** Unity client starts in Offline Mock mode (default)
- **THEN** it SHALL load Perception JSON files from `Assets/StreamingAssets/PerceptionFrames/`
- **AND** SHALL replay frames in sequence to simulate game state updates
- **AND** SHALL allow movement/rotation commands that mutate local mock state

#### Scenario: Offline mode provides tool execution
- **WHEN** player executes a tool (move, rotate, changelevel) in Offline mode
- **THEN** client SHALL update local mock Perception state accordingly
- **AND** SHALL trigger a perception update event to render the new state
- **AND** SHALL maintain server-authoritative behavior in mock logic

### Requirement: Unity Client Live Mode
The Unity client SHALL support an optional Live mode that connects to the server via SignalR.

#### Scenario: Live mode connects to SignalR
- **WHEN** Unity client is configured for Live mode with `USE_SIGNALR` scripting define enabled
- **THEN** it SHALL connect to GameHub at configured URL (default `http://localhost:5000/gamehub`)
- **AND** SHALL receive perception updates via `ReceivePerceptionUpdate`
- **AND** SHALL send tool execution commands via `ExecuteTool` hub method

#### Scenario: Live mode falls back gracefully
- **WHEN** Live mode connection fails or is unavailable
- **THEN** client SHALL fall back to Offline Mock mode
- **AND** SHALL display connection status in HUD
- **AND** SHALL continue to function in offline mode

#### Scenario: Live mode uses unified tool API
- **WHEN** player executes a tool in Live mode
- **THEN** client SHALL call `GameHub.ExecuteTool(toolId, args)` with appropriate parameters
- **AND** SHALL wait for perception update from server
- **AND** SHALL render updated perception when received

### Requirement: Unity Client Testing
The Unity client SHALL include EditMode and PlayMode tests for validation.

#### Scenario: EditMode test parses Perception JSON
- **WHEN** EditMode test loads a sample Perception JSON file
- **THEN** it SHALL parse the JSON to Unity-friendly DTO shims
- **AND** SHALL assert that grid dimensions match VisibleBounds
- **AND** SHALL assert that PlayerLocation and PlayerHeading are correctly deserialized

#### Scenario: PlayMode test renders tilemap
- **WHEN** PlayMode test loads Main.unity scene
- **THEN** it SHALL inject a mock Perception frame
- **AND** SHALL call TilemapRenderer2D to render tiles
- **AND** SHALL assert that expected number of tiles are rendered
- **AND** SHALL assert that player GameObject is positioned at expected cell

#### Scenario: UI automation test simulates input
- **WHEN** UI automation test simulates a movement key press
- **THEN** player marker SHALL move one cell in the expected direction
- **AND** test SHALL verify player GameObject position updated correctly

