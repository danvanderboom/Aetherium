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

### Requirement: Recognition Memory Retrieval
The management grain SHALL expose an operator-gated read of a character's individual-recognition memory by world and entity id, resolving the canonical world, so both player characters and NPCs can be inspected.

#### Scenario: Read a character's known individuals
- **WHEN** `GetRecognitionAsync(worldId, entityId)` is called for a character with recognition state and operator access is enabled
- **THEN** it SHALL return JSON listing each known individual with kind, first-met, last-seen, encounter count, stored and effective familiarity, stability, and permanence

#### Scenario: Operator gate
- **WHEN** operator access is disabled
- **THEN** `GetRecognitionAsync` SHALL return null

#### Scenario: Unknown world or entity
- **WHEN** the world cannot be resolved or the entity is not present
- **THEN** `GetRecognitionAsync` SHALL return null

### Requirement: Character Memory Retrieval
The grain SHALL expose a character's accumulated memories to authorized operators as JSON.

#### Scenario: Retrieve memories for a session
- **WHEN** `GetMemoryAsync` is called with a valid `sessionId`
- **THEN** the grain SHALL return the character's memories as JSON, each entry carrying absolute location, content type, content, stored strength, effective (decayed) strength, impression count, and last-seen time

#### Scenario: Unknown session
- **WHEN** `GetMemoryAsync` is called with an unknown `sessionId`
- **THEN** the grain SHALL return null and SHALL NOT throw

#### Scenario: Operator gating
- **WHEN** operator access is disabled
- **THEN** `GetMemoryAsync` SHALL be denied (memories carry absolute world coordinates and are a god-view read)

### Requirement: Headless Session Provisioning
The grain SHALL support creating a game session in an existing world without an interactive SignalR client connection, so that automation, tests, and agent runners can place and drive a character with no game client running.

#### Scenario: Create headless session in an existing world
- **WHEN** `CreateHeadlessSessionAsync` is called with a valid `worldId`
- **THEN** the grain SHALL construct a `GameSession` bound to a synthetic connection id (for example `headless:{guid}`)
- **AND** the session SHALL place a player `Character` in that world using the same placement logic as an interactive join
- **AND** the grain SHALL register the session in its index via the sessionId ↔ connectionId mapping
- **AND** the grain SHALL return the new `sessionId`

#### Scenario: Create headless session at an explicit start location
- **WHEN** `CreateHeadlessSessionAsync` is called with a `worldId` and a start location
- **THEN** the player `Character` SHALL be placed at that location if it is passable
- **AND** the grain SHALL return the new `sessionId`

#### Scenario: Create headless session in a non-existent world
- **WHEN** `CreateHeadlessSessionAsync` is called with an unknown `worldId`
- **THEN** the grain SHALL NOT create a session
- **AND** SHALL return a failure result indicating the world was not found

#### Scenario: Drive a headless session with existing verbs
- **WHEN** a headless session exists and `GetPerceptionAsync`, `MoveAsync`, or `ExecuteToolAsync` is called with its `sessionId`
- **THEN** the grain SHALL resolve the session and execute the operation exactly as for a client-backed session
- **AND** any perception push to the (client-less) connection SHALL be a safe no-op

#### Scenario: Terminate and reap headless sessions
- **WHEN** `TerminateSessionAsync` is called with a headless `sessionId`
- **THEN** the grain SHALL remove the session from `GameSessionManager` and its index
- **WHEN** a headless session remains idle beyond the configured timeout
- **THEN** the grain SHALL terminate it automatically
- **AND** the reaper SHALL only target sessions tagged as headless

### Requirement: Operator Perception Retrieval
The grain SHALL expose a session's current perception to authorized operators as JSON, including an option to return absolute (un-relativized) world coordinates for debugging.

#### Scenario: Retrieve perception for a valid session
- **WHEN** `GetPerceptionAsync` is called with a valid `sessionId`
- **THEN** the grain SHALL return the session's current perception serialized as a `PerceptionDto` JSON string

#### Scenario: Retrieve perception with absolute coordinates
- **WHEN** perception is requested with the absolute-coordinates option enabled
- **THEN** the returned `PlayerLocation` SHALL contain the player's true world coordinates
- **AND** the default behavior (without the option) SHALL remain relativized to (0,0,0)

#### Scenario: Retrieve perception for a non-existent session
- **WHEN** `GetPerceptionAsync` is called with an unknown `sessionId`
- **THEN** the grain SHALL return null
- **AND** SHALL NOT throw an exception

#### Scenario: Perception reflects prior action
- **WHEN** an action changes session state and perception is retrieved afterward
- **THEN** the returned perception SHALL reflect the updated state

### Requirement: World State Snapshot
The grain SHALL provide an omniscient, field-of-view-independent snapshot of a world's tiles and entities, independent of any single session's perception.

#### Scenario: Retrieve a world snapshot
- **WHEN** `GetWorldSnapshotAsync` is called with a valid `worldId`
- **THEN** the grain SHALL return a snapshot containing the world's tiles and all entities with absolute coordinates
- **AND** the snapshot SHALL include entities regardless of visibility or lighting

#### Scenario: Cap oversized snapshots
- **WHEN** a world's entity or tile count exceeds the snapshot cap
- **THEN** the snapshot SHALL set a truncation flag rather than silently dropping content
- **AND** the omitted counts SHALL be logged

#### Scenario: Snapshot for a non-existent world
- **WHEN** `GetWorldSnapshotAsync` is called with an unknown `worldId`
- **THEN** the grain SHALL return a failure result or null
- **AND** SHALL NOT throw an exception

### Requirement: Operator Authorization for God-View Operations
The grain SHALL restrict headless-session creation, absolute-coordinate perception, and world snapshots to an operator/developer authorization capability, so that ordinary player profiles cannot reach god-view state.

#### Scenario: Player profile denied god-view operations
- **WHEN** a caller without the operator capability invokes `CreateHeadlessSessionAsync`, absolute-coordinate perception, or `GetWorldSnapshotAsync`
- **THEN** the grain SHALL deny the operation
- **AND** SHALL return a failure result indicating insufficient authorization

#### Scenario: Operator caller permitted
- **WHEN** a caller with the operator capability invokes those operations
- **THEN** the grain SHALL perform them normally

### Requirement: Batch Action Execution
The grain SHALL execute an ordered sequence of tool invocations against a single session in one grain call and return a result for each attempted step, so that callers can drive a character with a deterministic, reproducible action script.

#### Scenario: Execute an ordered batch
- **WHEN** `ExecuteToolBatchAsync` is called with a valid `sessionId` and a list of actions
- **THEN** the grain SHALL execute the actions in the given order against that session
- **AND** SHALL return one result per action containing its index, tool id, success flag, and message
- **AND** the results SHALL be in the same order as the input actions

#### Scenario: Stop on first error
- **WHEN** `ExecuteToolBatchAsync` is called with `stopOnError` = true and a step fails
- **THEN** the grain SHALL stop after the failing step
- **AND** SHALL return the results for the steps attempted so far, ending with the failed step

#### Scenario: Continue past errors
- **WHEN** `ExecuteToolBatchAsync` is called with `stopOnError` = false and a step fails
- **THEN** the grain SHALL continue executing the remaining steps
- **AND** SHALL return a result for every action, each reporting its own success or failure

#### Scenario: Unknown session
- **WHEN** `ExecuteToolBatchAsync` is called with a session id that is not registered
- **THEN** the grain SHALL NOT throw
- **AND** SHALL return a single failure result indicating the session was not found

#### Scenario: Empty and oversized batches
- **WHEN** `ExecuteToolBatchAsync` is called with an empty action list
- **THEN** the grain SHALL return an empty result list
- **WHEN** the action list exceeds the maximum batch size
- **THEN** the grain SHALL reject the batch with a clear error rather than executing a partial sequence

### Requirement: Runtime World Tool Execution
The grain SHALL execute world-building tools against a live, running world (resolved from the in-process world registry), so that operators can modify worlds at runtime without regenerating them.

#### Scenario: Execute a world-building tool at runtime
- **WHEN** `ExecuteWorldToolAsync` is called with a valid `worldId` and a tool that requires the `world_edit` capability
- **THEN** the grain SHALL execute the tool against that world via a world-building context
- **AND** SHALL return the tool's result, including any structured data (e.g. a spawned entity id)

#### Scenario: Spawn a creature into a running world
- **WHEN** `ExecuteWorldToolAsync` runs the `spawnentity` tool with a supported creature type at a passable, unoccupied location
- **THEN** a new entity SHALL be created and added to the world at that location
- **AND** the entity SHALL appear in subsequent world snapshots and, when visible, in character perception

#### Scenario: Reject non-world-building tools
- **WHEN** `ExecuteWorldToolAsync` is called with a tool that does not require the `world_edit` capability
- **THEN** the grain SHALL refuse to execute it and return a failure result

#### Scenario: Unknown world or tool
- **WHEN** `ExecuteWorldToolAsync` is called with an unknown `worldId` or an unregistered tool id
- **THEN** the grain SHALL return a failure result identifying the problem
- **AND** SHALL NOT throw

#### Scenario: Operator gating
- **WHEN** operator access is disabled
- **THEN** `ExecuteWorldToolAsync` SHALL be denied with a failure result

