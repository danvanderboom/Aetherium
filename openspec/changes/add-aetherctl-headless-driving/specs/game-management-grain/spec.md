## ADDED Requirements

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
