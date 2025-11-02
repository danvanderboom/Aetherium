# game-management-grain Specification

## Purpose
TBD - created by archiving change add-game-management-grain. Update Purpose after archive.
## Requirements
### Requirement: Session Registration and Lifecycle
The grain SHALL maintain an index of active game sessions registered by GameHub during connection and disconnection events.

#### Scenario: Register new session
- **WHEN** GameHub.OnConnectedAsync creates a new session
- **THEN** GameHub SHALL call grain.RegisterSessionAsync with sessionId and connectionId
- **AND** grain SHALL store session metadata in its index
- **AND** grain SHALL maintain bidirectional sessionId ↔ connectionId mapping

#### Scenario: Unregister disconnected session
- **WHEN** GameHub.OnDisconnectedAsync removes a session
- **THEN** GameHub SHALL call grain.UnregisterSessionAsync with sessionId
- **AND** grain SHALL remove session metadata from its index
- **AND** grain SHALL remove both sessionId and connectionId mappings

#### Scenario: Handle duplicate registration
- **WHEN** grain.RegisterSessionAsync is called with existing sessionId
- **THEN** grain SHALL update the existing entry with new connectionId and timestamp
- **AND** grain SHALL not create duplicate entries

#### Scenario: Handle orphaned session metadata
- **WHEN** operation is requested on session not in grain index but disconnected
- **THEN** grain SHALL return OperationResult with Success=false
- **AND** Reason SHALL indicate "Session not found"

### Requirement: Session Query Operations
The grain SHALL provide methods to list and query active game sessions.

#### Scenario: List all active sessions
- **WHEN** ListSessionsAsync is called
- **THEN** grain SHALL return list of SessionInfo for all registered sessions
- **AND** list SHALL include sessionId, connectionId, vision settings, and timestamp for each

#### Scenario: Get session by session ID
- **WHEN** GetSessionInfoAsync is called with valid sessionId
- **THEN** grain SHALL return SessionInfo with current session state
- **AND** SessionInfo SHALL include DirectionalVisionMode, HeadingDegrees, FieldOfViewDegrees

#### Scenario: Get session by connection ID
- **WHEN** GetSessionByConnectionIdAsync is called with valid connectionId
- **THEN** grain SHALL lookup sessionId from connectionId mapping
- **AND** grain SHALL return SessionInfo for that session
- **AND** SHALL return null if connectionId not found

#### Scenario: Query non-existent session
- **WHEN** GetSessionInfoAsync is called with invalid sessionId
- **THEN** grain SHALL return null
- **AND** SHALL not throw exception

#### Scenario: Get session count
- **WHEN** GetSessionCountAsync is called
- **THEN** grain SHALL return integer count of registered sessions
- **AND** count SHALL match number of entries in session index

### Requirement: Directional Vision Control
The grain SHALL provide methods to control directional vision mode for specific sessions.

#### Scenario: Enable directional vision
- **WHEN** SetDirectionalVisionAsync is called with sessionId and enabled=true
- **THEN** grain SHALL invoke hub method to set DirectionalVisionMode=true
- **AND** session SHALL compute perception with directional FOV cone
- **AND** client SHALL receive updated perception with limited visibility
- **AND** grain SHALL return OperationResult with Success=true

#### Scenario: Disable directional vision
- **WHEN** SetDirectionalVisionAsync is called with sessionId and enabled=false
- **THEN** grain SHALL invoke hub method to set DirectionalVisionMode=false
- **AND** session SHALL compute perception with omnidirectional visibility
- **AND** client SHALL receive full 360-degree perception
- **AND** grain SHALL return OperationResult with Success=true

#### Scenario: Toggle vision for non-existent session
- **WHEN** SetDirectionalVisionAsync is called with invalid sessionId
- **THEN** grain SHALL return OperationResult with Success=false
- **AND** Reason SHALL indicate "Session not found"
- **AND** no hub method SHALL be invoked

#### Scenario: Get current vision status
- **WHEN** GetVisionStatusAsync is called with valid sessionId
- **THEN** grain SHALL return VisionStatus DTO
- **AND** VisionStatus SHALL include DirectionalVisionMode boolean
- **AND** VisionStatus SHALL include HeadingDegrees (0-359)
- **AND** VisionStatus SHALL include FieldOfViewDegrees
- **AND** VisionStatus SHALL include LightingMode and VisionMode enums

### Requirement: Field of View Configuration
The grain SHALL enable setting field of view degrees for directional vision.

#### Scenario: Set valid FOV degrees
- **WHEN** SetFieldOfViewAsync is called with degrees in range 1-360
- **THEN** grain SHALL invoke hub method to update player entity's FOV
- **AND** grain SHALL return OperationResult with Success=true
- **AND** next perception update SHALL use new FOV cone

#### Scenario: Validate FOV range
- **WHEN** SetFieldOfViewAsync is called with degrees < 1 or > 360
- **THEN** grain SHALL return OperationResult with Success=false
- **AND** Reason SHALL indicate "FOV must be between 1 and 360 degrees"
- **AND** no hub method SHALL be invoked

#### Scenario: Set FOV for non-existent session
- **WHEN** SetFieldOfViewAsync is called with invalid sessionId
- **THEN** grain SHALL return OperationResult with Success=false
- **AND** Reason SHALL indicate "Session not found"

#### Scenario: FOV applies to directional vision only
- **WHEN** DirectionalVisionMode is false
- **THEN** SetFieldOfViewAsync SHALL succeed but FOV SHALL not affect perception
- **AND** omnidirectional vision SHALL continue to show 360-degree view
- **WHEN** DirectionalVisionMode is toggled to true
- **THEN** perception SHALL use the configured FOV value

### Requirement: Lighting Mode Control
The grain SHALL enable setting lighting mode for sessions.

#### Scenario: Set torch lighting mode
- **WHEN** SetLightingModeAsync is called with LightingMode.Torch
- **THEN** grain SHALL invoke hub method to set session.CurrentLightingMode
- **AND** perception SHALL compute with torch-based light propagation
- **AND** grain SHALL return OperationResult with Success=true

#### Scenario: Set sunlight lighting mode
- **WHEN** SetLightingModeAsync is called with LightingMode.Sunlight
- **THEN** grain SHALL invoke hub method to set session.CurrentLightingMode
- **AND** perception SHALL compute with daylight illumination
- **AND** grain SHALL return OperationResult with Success=true

#### Scenario: Set infrared lighting mode
- **WHEN** SetLightingModeAsync is called with LightingMode.Infrared
- **THEN** grain SHALL invoke hub method to set session.CurrentLightingMode
- **AND** perception SHALL compute using heat signatures
- **AND** grain SHALL return OperationResult with Success=true

#### Scenario: Invalid lighting mode
- **WHEN** SetLightingModeAsync is called with invalid enum value
- **THEN** grain SHALL return OperationResult with Success=false
- **AND** Reason SHALL indicate "Invalid lighting mode"

### Requirement: Vision Type Configuration
The grain SHALL enable setting vision type (Normal, Infrared) for sessions.

#### Scenario: Set normal vision mode
- **WHEN** SetVisionModeAsync is called with VisionMode.Normal
- **THEN** grain SHALL invoke hub method to set session.CurrentVisionMode
- **AND** perception SHALL use normal color rendering
- **AND** grain SHALL return OperationResult with Success=true

#### Scenario: Set infrared vision mode
- **WHEN** SetVisionModeAsync is called with VisionMode.Infrared
- **THEN** grain SHALL invoke hub method to set session.CurrentVisionMode
- **AND** perception SHALL render heat trails and signatures
- **AND** grain SHALL return OperationResult with Success=true

#### Scenario: Invalid vision mode
- **WHEN** SetVisionModeAsync is called with invalid enum value
- **THEN** grain SHALL return OperationResult with Success=false
- **AND** Reason SHALL indicate "Invalid vision mode"

### Requirement: Session Control Operations
The grain SHALL provide administrative control over session behavior.

#### Scenario: Terminate active session
- **WHEN** TerminateSessionAsync is called with valid sessionId
- **THEN** grain SHALL disconnect the SignalR connection
- **AND** GameHub.OnDisconnectedAsync SHALL fire
- **AND** session SHALL be removed from GameSessionManager
- **AND** grain SHALL unregister session from its index
- **AND** grain SHALL return OperationResult with Success=true

#### Scenario: Set session time scale
- **WHEN** SetTimeScaleAsync is called with positive double value
- **THEN** grain SHALL invoke hub method to set session.TimeScale
- **AND** game time SHALL advance at new rate relative to real time
- **AND** grain SHALL return OperationResult with Success=true

#### Scenario: Invalid time scale
- **WHEN** SetTimeScaleAsync is called with value ≤ 0
- **THEN** grain SHALL return OperationResult with Success=false
- **AND** Reason SHALL indicate "Time scale must be positive"

#### Scenario: Operations on terminated session
- **WHEN** any control operation is attempted after TerminateSessionAsync
- **THEN** operation SHALL return OperationResult with Success=false
- **AND** Reason SHALL indicate "Session not found"

### Requirement: Batch Operations
The grain SHALL support batch operations affecting multiple sessions.

#### Scenario: Set vision mode for all sessions
- **WHEN** SetAllSessionsVisionModeAsync is called with enabled boolean
- **THEN** grain SHALL iterate all registered sessions
- **AND** SHALL invoke SetDirectionalVisionAsync for each session
- **AND** SHALL return count of successful and failed operations

#### Scenario: Bulk configuration with partial failure
- **WHEN** batch operation encounters error on one session
- **THEN** grain SHALL continue processing remaining sessions
- **AND** SHALL collect all results
- **AND** SHALL return summary with success count and failure details

### Requirement: Error Handling and Resilience
The grain SHALL handle error conditions gracefully and return informative results.

#### Scenario: Hub context invocation failure
- **WHEN** IHubContext call fails or throws exception
- **THEN** grain SHALL catch exception
- **AND** SHALL return OperationResult with Success=false
- **AND** Reason SHALL include exception message
- **AND** grain state SHALL remain consistent

#### Scenario: Concurrent operations on same session
- **WHEN** multiple grain methods are called concurrently for same sessionId
- **THEN** grain SHALL handle concurrent access safely
- **AND** operations SHALL execute in order received
- **AND** no data corruption SHALL occur
- **AND** all operations SHALL return valid results

#### Scenario: Invalid parameter validation
- **WHEN** method is called with null sessionId
- **THEN** grain SHALL return OperationResult with Success=false
- **AND** Reason SHALL indicate "Session ID cannot be null"
- **AND** no hub method SHALL be invoked

#### Scenario: Orleans disabled mode
- **WHEN** server runs with DISABLE_ORLEANS=1 environment variable
- **THEN** grain registration calls SHALL be safe no-ops
- **AND** AgentCLI commands SHALL detect grain unavailability
- **AND** CLI SHALL display "Orleans disabled" message

### Requirement: AgentCLI Integration
AgentCLI SHALL use the grain to implement vision control commands.

#### Scenario: CLI enable directional vision
- **WHEN** user runs `agentcli vision directional <sessionId>`
- **THEN** CLI SHALL connect to Orleans cluster
- **AND** SHALL obtain IGameManagementGrain singleton
- **AND** SHALL call SetDirectionalVisionAsync
- **AND** SHALL display success or error message to user

#### Scenario: CLI set field of view
- **WHEN** user runs `agentcli vision fov <entityId> <degrees>`
- **THEN** CLI SHALL validate degrees range locally (1-360)
- **AND** SHALL call SetFieldOfViewAsync on grain
- **AND** SHALL display result to user

#### Scenario: CLI show vision status
- **WHEN** user runs `agentcli vision status <sessionId>`
- **THEN** CLI SHALL call GetVisionStatusAsync on grain
- **AND** SHALL display formatted status:
  - Directional Vision: ON/OFF
  - Heading: N degrees
  - Field of View: N degrees
  - Lighting Mode: [mode]
  - Vision Mode: [mode]

#### Scenario: CLI graceful error handling
- **WHEN** grain operation returns Success=false
- **THEN** CLI SHALL display error message from Reason field
- **AND** SHALL exit with non-zero status code
- **AND** SHALL not display stack trace to user

### Requirement: Grain Lifecycle and Activation
The grain SHALL follow Orleans grain lifecycle patterns for singleton coordination.

#### Scenario: Grain activation
- **WHEN** first grain method is called after server start
- **THEN** Orleans SHALL activate IGameManagementGrain with key "GLOBAL"
- **AND** grain SHALL initialize empty session index
- **AND** activation SHALL complete within 100ms

#### Scenario: Grain singleton behavior
- **WHEN** multiple clients call grain methods
- **THEN** all SHALL access same grain instance
- **AND** session index SHALL be shared across all callers
- **AND** no duplicate grain instances SHALL exist

#### Scenario: Grain deactivation resilience
- **WHEN** grain is deactivated by Orleans runtime
- **THEN** next grain call SHALL reactivate grain
- **AND** session index SHALL be empty (stateless grain)
- **AND** GameHub registration SHALL repopulate index as sessions connect

#### Scenario: Grain concurrency
- **WHEN** grain receives concurrent method calls
- **THEN** Orleans SHALL serialize calls using grain's single-threaded turn-based model
- **AND** no explicit locking SHALL be required in grain code
- **AND** ConcurrentDictionary SHALL provide additional thread safety for index

