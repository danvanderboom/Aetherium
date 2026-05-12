# client-server-communication Specification

## Purpose
Enables client-server separation where the authoritative game engine runs on a server and clients receive only perception data (what they can see/hear/feel). Supports multiple players, remote clients, and server-side game state management through SignalR real-time communication.
## Requirements
### Requirement: Server-Authoritative Game State
The game engine SHALL run on a dedicated server process. For sessions bound to a world via `JoinWorld`, the canonical state lives in `IGameMapGrain._world` and is mutated only through the grain's typed mutation methods. Session-local `World` instances function as mirrors that converge to the grain's state via deltas. For legacy sessions (no `worldId` query parameter), the session's local `World` remains authoritative for that session only.

#### Scenario: Grain owns canonical world state for joined sessions
- **WHEN** a session is bound to a map via `GameHub.JoinWorld`
- **THEN** all subsequent gameplay mutations SHALL be applied first to `IGameMapGrain._world`
- **AND** the session's local `World` SHALL be updated only by `GameSession.ApplyDelta` in response to grain-emitted deltas
- **AND** no client SHALL have direct access to the grain's `_world` object

#### Scenario: Server processes all game logic
- **WHEN** a player action is received via `GameHub.ExecuteTool`
- **THEN** the server SHALL validate and execute the action
- **AND** for grain-bound sessions, the action SHALL be applied to the grain's `_world` via the corresponding `IGameMapGrain` mutation method
- **AND** the grain SHALL emit a `MapDelta` describing the mutation
- **AND** the server SHALL determine updated perception for affected clients

#### Scenario: Legacy session sessions retain local authority
- **WHEN** a session has connected without a `worldId` query parameter (legacy mode)
- **THEN** mutations SHALL apply to that session's local `World` via `LocalMutationGateway`
- **AND** mutations SHALL NOT propagate to any grain or other session
- **AND** the session SHALL behave exactly as it did before phase 2

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
The system SHALL use SignalR for bidirectional real-time communication between client and server. Player commands SHALL be invoked via the unified `ExecuteTool(toolId, args)` hub method routed through the agent tool registry. The legacy per-verb hub methods (`MovePlayer`, `RotatePlayer`, `Pickup`, etc.) are removed.

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
- **THEN** client SHALL invoke `ExecuteTool(toolId, args)` on the GameHub with the appropriate tool ID (`"move"`, `"rotate"`, `"changelevel"`, `"pickup"`, `"drop"`, `"use"`, `"open"`, `"close"`, etc.)
- **AND** server SHALL dispatch through the tool registry to the corresponding tool
- **AND** the tool SHALL invoke the configured `IMapMutationGateway` (local or grain-routed depending on session binding)
- **AND** server SHALL send updated perception to the client

#### Scenario: Server pushes deltas to grain-bound sessions
- **WHEN** a mutation is applied to a grain-bound map
- **THEN** server SHALL invoke `ApplyDelta(delta)` on every SignalR connection in the `map:{mapId}` group
- **AND** clients SHALL reconcile their local mirror via `GameSession.ApplyDelta`

### Requirement: Session Management
The server SHALL maintain separate game sessions for each connected client. A session's `World` SHALL be sourced either from a session-local builder (legacy / ad-hoc mode) or from a grain-served `WorldSnapshot` (multi-world mode).

#### Scenario: New client creates session (legacy single-world mode)
- **WHEN** a client connects to GameHub without a `worldId` query parameter
- **THEN** the server SHALL create a new GameSession for that connection
- **AND** SHALL initialize a local `World` via a built-in `WorldBuilder` (e.g. `FovDiagnosticWorldBuilder`)
- **AND** SHALL assign a unique session ID

#### Scenario: New client creates session (snapshot-bridged mode)
- **WHEN** a client connects to GameHub with `?worldId=<id>` in the connection query string
- **THEN** the server SHALL create a new GameSession with a temporary local world
- **AND** SHALL immediately invoke the `JoinWorld(worldId)` flow on behalf of the client
- **AND** SHALL replace the session's `World` with one hydrated from the grain-served `WorldSnapshot`
- **AND** SHALL set the session's `ViewLocation` to the spawn location assigned by the map grain

#### Scenario: Disconnected client cleanup
- **WHEN** a client disconnects
- **THEN** the server SHALL remove the GameSession
- **AND** SHALL clean up local World resources
- **AND** when the session was joined to a map grain, SHALL notify the map grain to remove the player from its `PlayerIds` set
- **AND** SHALL not affect other active sessions

### Requirement: Command Processing
The server SHALL accept player commands via hub methods and process them in the game engine. Commands that mutate gameplay state SHALL flow through an `IMapMutationGateway` so the mutation site can be transparently relocated (in later phases) without changing the tool code that issued the command.

#### Scenario: Move command execution
- **WHEN** client sends `ExecuteTool("move", { direction, distance })` (or a legacy `MovePlayer` invocation)
- **THEN** server SHALL dispatch the request through the tool registry to `MoveTool`
- **AND** `MoveTool` SHALL invoke `context.MutationGateway.MoveAsync(direction, distance)`
- **AND** the gateway SHALL update the player's `ViewLocation` in the game world
- **AND** server SHALL compute new perception for that location
- **AND** server SHALL send `ReceivePerceptionUpdate` to the client

#### Scenario: Rotate command execution
- **WHEN** client sends `ExecuteTool("rotate", { degrees })` (or a legacy `RotatePlayer` invocation)
- **THEN** server SHALL dispatch through `RotateTool`
- **AND** `RotateTool` SHALL invoke `context.MutationGateway.RotateAsync(degrees)`
- **AND** the gateway SHALL update the player's heading
- **AND** server SHALL compute perception from new heading
- **AND** server SHALL send updated perception to client

#### Scenario: Interaction command execution
- **WHEN** client sends an `ExecuteTool` invocation for `pickup`, `drop`, `use`, `open`, or `close`
- **THEN** the corresponding tool SHALL invoke the equivalent `IMapMutationGateway` method (`PickupAsync`/`DropAsync`/`UseAsync`/`OpenAsync`/`CloseAsync`)
- **AND** the gateway SHALL return an `InteractionResultDto` indicating success or failure
- **AND** if successful, the server SHALL send `ReceivePerceptionUpdate` reflecting the post-mutation state

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
The client-server architecture SHALL provide gameplay equivalent to the original single-process version. Players SHALL be able to perform every action that was available pre-split (movement, rotation, level change, interactions, vision-mode toggles) through the unified `ExecuteTool` invocation surface.

#### Scenario: All gameplay verbs are reachable via ExecuteTool
- **WHEN** a client invokes `ExecuteTool` with a tool ID corresponding to a movement verb (`"move"`, `"rotate"`, `"changelevel"`, `"jumptolocation"`), an interaction verb (`"pickup"`, `"drop"`, `"use"`, `"open"`, `"close"`), or a vision verb (`"setlightingmode"`, `"setvisionmode"`, `"toggledirectionalvision"`)
- **THEN** the registry SHALL resolve the tool and dispatch the call
- **AND** the resulting behavior SHALL be functionally equivalent to the pre-phase-2d hub methods of the same name

#### Scenario: Client console bindings route through ExecuteTool
- **WHEN** the console client maps arrow keys, Z/X rotation, U/D level change, or other gameplay key bindings
- **THEN** each binding SHALL invoke `ExecuteTool` rather than a per-verb hub method
- **AND** rendering and perception SHALL match the pre-phase-2d behavior

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

### Requirement: Interaction Commands
The system SHALL provide server-side actions for item and object interactions, dispatched through the unified `ExecuteTool` surface and routed via `IMapMutationGateway` (local or grain-backed). Tools SHALL return `InteractionResultDto` indicating success or a structured failure reason. The legacy direct hub methods (`Pickup`, `Drop`, `Use`, `Open`, `Close`) are removed.

#### Scenario: Pick up an item
- **WHEN** client sends `ExecuteTool("pickup", { targetEntityId })`
- **THEN** server SHALL dispatch through `PickupTool`
- **AND** `PickupTool` SHALL invoke `context.MutationGateway.PickupAsync(targetEntityId)`
- **AND** the gateway SHALL atomically remove the entity from the world (via `World.TryRemoveEntity`) and add it to the actor's inventory if carriable and co-located
- **AND** the result SHALL include a `Reason` describing any failure

#### Scenario: Drop an inventory item
- **WHEN** client sends `ExecuteTool("drop", { itemEntityId })`
- **THEN** server SHALL dispatch through `DropTool` → `context.MutationGateway.DropAsync`
- **AND** the gateway SHALL place the item at the actor's location if capacity rules allow

#### Scenario: Use an item on a target
- **WHEN** client sends `ExecuteTool("use", { itemEntityId, onEntityId, usageId? })`
- **THEN** server SHALL dispatch through `UseTool` → `context.MutationGateway.UseAsync`
- **AND** the gateway SHALL apply the item's effect to the target (e.g., unlock door with matching key)

#### Scenario: Open or Close a door
- **WHEN** client sends `ExecuteTool("open", { targetEntityId })` or `ExecuteTool("close", { targetEntityId })`
- **THEN** server SHALL dispatch through the corresponding tool → `context.MutationGateway.OpenAsync` or `CloseAsync`
- **AND** the gateway SHALL toggle the door if unlocked and adjacent/accessible
- **AND** an `OpensAndCloses` component change SHALL fire a `DoorStateChangedDelta` for grain-bound sessions

### Requirement: Interaction Events
The system SHALL emit world events after successful interactions.

#### Scenario: Item picked up event
- **WHEN** an item is picked up
- **THEN** `ItemPickedUp` world event is emitted (actorId, itemId)

#### Scenario: Door state change events
- **WHEN** a door is opened/closed/locked/unlocked
- **THEN** a corresponding world event is emitted and obstructions updated

### Requirement: World Joining via Snapshot Bridge
GameHub SHALL provide a `JoinWorld(worldId, mapId?)` method that binds the caller's session to a world owned by `IGameMapGrain`, hydrating the session's `World` from a snapshot served by the grain. The method SHALL return a `JoinWorldResult` indicating success, the resolved map ID, and the assigned spawn location.

#### Scenario: Successful join to an active world
- **WHEN** a client calls `JoinWorld(worldId)` and the world exists and is in `Active` state
- **THEN** GameHub SHALL resolve a map (the supplied `mapId` if present, otherwise the world's first map)
- **AND** SHALL invoke `IGameMapGrain.JoinPlayerAsync(sessionId)` and receive a `JoinMapResult` with `Success=true`, a unique `SpawnLocation`, and a `PlayerEntityId`
- **AND** SHALL invoke `IGameMapGrain.GetWorldSnapshotAsync()` and receive a `WorldSnapshot`
- **AND** SHALL hydrate the session's `World` via `SnapshotWorldBuilder` so that all entity IDs in the session's hydrated world match the entity IDs in the snapshot
- **AND** SHALL set `session.WorldId`, `session.MapId`, and `session.ViewLocation = SpawnLocation`
- **AND** SHALL send a `ReceivePerceptionUpdate` to the client with the initial perception
- **AND** SHALL return `JoinWorldResult` with `Success=true`, the resolved `MapId`, and the `SpawnLocation`

#### Scenario: Join fails when world does not exist
- **WHEN** a client calls `JoinWorld(worldId)` and no world with that ID is registered
- **THEN** GameHub SHALL return `JoinWorldResult` with `Success=false` and a sanitized `Reason` (no internal exception text)
- **AND** SHALL leave the session's existing `World` unchanged

#### Scenario: Join fails when world is not active
- **WHEN** a client calls `JoinWorld(worldId)` and the world exists but is in `Paused`, `Stopped`, `ShuttingDown`, or `Creating` state
- **THEN** GameHub SHALL return `JoinWorldResult` with `Success=false` and a `Reason` describing the world state
- **AND** SHALL leave the session's existing `World` unchanged

#### Scenario: Two clients join the same world
- **WHEN** two distinct clients each call `JoinWorld(worldId)` for the same world
- **THEN** both calls SHALL succeed
- **AND** each client's session SHALL receive a `WorldSnapshot` with identical entity IDs at identical locations
- **AND** each client SHALL be assigned a distinct `SpawnLocation`
- **AND** subsequent mutations in one session SHALL NOT be visible in the other session (phase 1 semantics — independent local worlds; live shared mutation is deferred to phase 2)

### Requirement: Snapshot-Driven World Hydration
The server SHALL provide a `SnapshotWorldBuilder` that constructs a `World` instance equivalent to the grain's canonical world from a `WorldSnapshot`. Terrain SHALL be regenerated by replaying the snapshot's `WorldRecipe` through the worldgen orchestrator; entities SHALL be overlaid with their snapshot-supplied `EntityId` values.

#### Scenario: Terrain regenerates deterministically from recipe
- **WHEN** `SnapshotWorldBuilder.Build()` is invoked with a snapshot whose recipe specifies generator `X`, seed `S`, and parameters `P`
- **THEN** the resulting `World` SHALL have terrain identical to a fresh `WorldGenerationOrchestrator.Generate` call with the same `X`, `S`, and `P`

#### Scenario: Entity placements preserve grain-assigned IDs
- **WHEN** `SnapshotWorldBuilder.Build()` instantiates an entity from a placement record with `EntityId = "abc-123"`
- **THEN** the resulting `Entity` in the hydrated `World` SHALL have `EntityId == "abc-123"`
- **AND** the entity SHALL be located at the placement record's `WorldLocation`

#### Scenario: Hydrated world is independent of the grain's canonical world
- **WHEN** a session's `World` is hydrated via `SnapshotWorldBuilder` and the client subsequently mutates it (e.g. picks up an item)
- **THEN** the grain's canonical `World` SHALL be unaffected
- **AND** other sessions hydrated from the same snapshot SHALL be unaffected

### Requirement: Grain Bridge for Map Membership and Snapshots
`IGameMapGrain` SHALL expose `GetWorldSnapshotAsync()` returning a `WorldSnapshot` and `JoinPlayerAsync(playerId)` returning a `JoinMapResult` with a unique spawn location and the player's authoritative entity ID. The pre-existing `AddPlayerAsync(playerId)` (returning `bool`) SHALL be preserved for cross-map moves where spawn details are not needed. Beginning in phase 2, the joining player's `Character` entity SHALL be added to the grain's `_world` so other joiners see them, and snapshots SHALL include all currently-joined player `Character` entities except the joining player's own.

#### Scenario: Snapshot reflects current entity placements
- **WHEN** `GetWorldSnapshotAsync()` is called on an initialized map grain
- **THEN** the returned `WorldSnapshot` SHALL include the grain's `WorldRecipe` (generator, seed, parameters)
- **AND** SHALL include an `EntityPlacement` record for every non-terrain `Entity` currently in the grain's `_world`, including `Character` entities for other joined players
- **AND** each `EntityPlacement` SHALL carry the entity's current `EntityId`, type name, and `WorldLocation`

#### Scenario: JoinPlayerAsync assigns unique spawn locations and adds the player to the world
- **WHEN** `JoinPlayerAsync` is called twice in succession with distinct player IDs on the same map grain
- **THEN** both calls SHALL return `JoinMapResult` with `Success=true`
- **AND** the two `SpawnLocation` values SHALL be distinct
- **AND** both locations SHALL satisfy `World.PassableTerrain`
- **AND** the grain SHALL add a `Character` entity to `_world` for each joiner with `EntityId == playerId` at the assigned spawn

#### Scenario: JoinPlayerAsync rejects duplicate player IDs
- **WHEN** `JoinPlayerAsync` is called with a `playerId` already present in `PlayerIds`
- **THEN** the grain SHALL return `JoinMapResult` with `Success=false` and a `Reason` describing the duplicate
- **AND** SHALL NOT allocate a new spawn slot

#### Scenario: JoinPlayerAsync rejects an uninitialized grain
- **WHEN** `JoinPlayerAsync` is called before `InitializeAsync` has completed
- **THEN** the grain SHALL return `JoinMapResult` with `Success=false` and a `Reason` indicating the map is not initialized

#### Scenario: Player Character is removed on map leave
- **WHEN** `IGameMapGrain.RemovePlayerAsync(playerId)` is called (e.g. on client disconnect or explicit `LeaveWorld`)
- **THEN** the player's `Character` entity SHALL be removed from `_world` via `World.TryRemoveEntity`
- **AND** an `EntityRemovedDelta` SHALL be emitted to other sessions in the map

### Requirement: Map Mutation Gateway Abstraction
The server SHALL provide an `IMapMutationGateway` interface that defines the contract for applying gameplay mutations to a session's world. Tools and other gameplay code paths SHALL invoke mutations through the gateway rather than reaching directly into `GameSession.World` or calling `InteractionSystem`/`GameSession` mutation methods. The gateway implementation in use for a given session determines whether mutations apply to a session-local `World` (legacy path) or are routed through a grain (later phases).

#### Scenario: Gateway is the only mutation entry point for gameplay tools
- **WHEN** a gameplay tool (movement, pickup, drop, use, open, close, change-level) executes via `GameHub.ExecuteTool`
- **THEN** the tool SHALL call the corresponding `IMapMutationGateway` method
- **AND** the tool SHALL NOT directly invoke `GameSession.MoveView`/`RotateView`/`ChangeLevel`
- **AND** the tool SHALL NOT directly invoke `InteractionSystem.Try*` methods

#### Scenario: Gateway carries typed result DTOs
- **WHEN** a tool calls a gateway method
- **THEN** the method SHALL return a typed result DTO (`MoveResult`, `RotateResult`, `ChangeLevelResult`, or `InteractionResultDto`)
- **AND** the result DTO SHALL be `[GenerateSerializer]`-compatible so it can later cross grain boundaries

#### Scenario: LocalMutationGateway is the phase 2a default
- **WHEN** `GameHub.ExecuteTool` constructs a `ToolExecutionContext` for a player session
- **THEN** the context's `MutationGateway` SHALL be a `LocalMutationGateway` bound to the session
- **AND** `LocalMutationGateway` SHALL delegate to today's `GameSession` and `InteractionSystem` methods so behavior is unchanged from before phase 2a

### Requirement: Grain Mutation Methods
`IGameMapGrain` SHALL expose typed mutation methods that apply gameplay actions to the grain's `_world`: `MoveAsync(sessionId, direction, distance)`, `RotateAsync(sessionId, degrees)`, `ChangeLevelAsync(sessionId, deltaZ)`, `PickupAsync(sessionId, targetEntityId)`, `DropAsync(sessionId, itemEntityId)`, `UseAsync(sessionId, itemEntityId, onEntityId, usageId)`, `OpenAsync(sessionId, targetEntityId)`, `CloseAsync(sessionId, targetEntityId)`. Orleans's single-threaded grain contract SHALL serialize concurrent invocations. Where an equivalent verb exists on `InteractionSystem`, the grain method SHALL delegate to the stateless `InteractionSystem.Try*(ActionContext, ...)` overload rather than reimplementing the verb's logic.

#### Scenario: Pickup/Drop/Open/Close delegate to InteractionSystem
- **WHEN** `IGameMapGrain.PickupAsync`, `DropAsync`, `OpenAsync`, or `CloseAsync` is invoked
- **THEN** the grain SHALL build an `ActionContext { World, Player, ViewLocation }` from its `_world` and the looked-up player Character
- **AND** SHALL call the corresponding `InteractionSystem.Try*(ActionContext, ...)` method to apply the mutation
- **AND** SHALL emit the appropriate `MapDelta` on success
- **AND** SHALL NOT reimplement the verb's pre-conditions or post-conditions in the grain itself

#### Scenario: Move/Rotate/ChangeLevel stay native
- **WHEN** `IGameMapGrain.MoveAsync`, `RotateAsync`, or `ChangeLevelAsync` is invoked
- **THEN** the grain SHALL implement the verb natively (no `InteractionSystem` equivalent)
- **AND** SHALL emit the appropriate `MapDelta` after mutation

#### Scenario: UseAsync remains limited to key-on-door
- **WHEN** `IGameMapGrain.UseAsync` is invoked with a non-key-on-door usage
- **THEN** the grain MAY return a failure with `Reason` indicating the mode is not yet supported in grain-bound sessions
- **AND** full `Use` disambiguation in grain mode is a future change that requires new delta DTOs (consume/place/lockpick/climb post-conditions)

#### Scenario: Mutation rejects unknown session
- **WHEN** a mutation method is invoked with a `sessionId` not in `_mapState.PlayerIds`
- **THEN** the grain SHALL return a failure result with a `Reason` indicating the unknown session
- **AND** SHALL NOT mutate `_world`

#### Scenario: Concurrent mutations are serialized
- **WHEN** two players concurrently invoke `PickupAsync(sessionId, sameItemId)` on the same map grain
- **THEN** exactly one call SHALL succeed (Orleans serialization)
- **AND** the other SHALL receive an `InteractionResultDto` with `Success=false` and `Reason` matching the existing "Already picked up" failure
- **AND** the item SHALL appear in exactly one player's inventory

### Requirement: Delta Fan-Out via SignalR Groups
Each session bound to a map SHALL be a member of the SignalR group `map:{mapId}`. After every mutation, the grain SHALL push the corresponding `MapDelta` to that group via `IHubContext<GameHub>.Clients.Group(...).SendAsync("ApplyDelta", delta)`. Clients SHALL implement `ApplyDelta(delta)` to receive the message.

#### Scenario: Group membership tracks map binding
- **WHEN** `GameHub.JoinWorld` succeeds for a session
- **THEN** the connection SHALL be added to SignalR group `map:{mapId}` via `Groups.AddToGroupAsync`
- **WHEN** the connection disconnects or calls `LeaveWorld`
- **THEN** the connection SHALL be removed from the group (automatically by SignalR on disconnect; explicitly via `Groups.RemoveFromGroupAsync` on `LeaveWorld`)

#### Scenario: Mutation propagates to all sessions in the map
- **WHEN** session A invokes a mutation (e.g. `PickupAsync`) on a map shared with session B
- **THEN** both A and B SHALL receive the resulting `ApplyDelta` message via the SignalR group
- **AND** the delta SHALL be received within milliseconds in single-silo deployments

#### Scenario: Delta translation failure does not roll back the mutation
- **WHEN** the grain's event-to-delta translation throws an exception
- **THEN** the grain SHALL log the exception
- **AND** the underlying mutation SHALL remain applied to `_world`
- **AND** the grain SHALL NOT roll back

#### Scenario: Coarse fan-out is acceptable in phase 2c
- **WHEN** a delta concerns a cell outside another session's FOV
- **THEN** the delta SHALL still be sent to that session (no FOV filtering at the grain in phase 2c)
- **AND** the session's perception layer SHALL exclude the unobservable change when rendering
- **AND** FOV-filtered fan-out is a separate later change

### Requirement: Session Mirror Reconciliation
`GameSession` SHALL expose an `ApplyDelta(MapDelta)` method that updates the session's local `World` mirror to reflect a grain-emitted delta. The method SHALL hold the session's existing `_stateLock` for the duration of the application. After applying a delta, the session SHALL recompute its perception and push `ReceivePerceptionUpdate` to its own connection.

#### Scenario: EntityMovedDelta updates the mirror
- **WHEN** a session receives an `EntityMovedDelta { EntityId, NewLocation }` via `ApplyDelta`
- **THEN** the session SHALL call `World.MoveEntity(EntityId, NewLocation)` on its mirror
- **AND** the session SHALL recompute perception
- **AND** the session SHALL send `ReceivePerceptionUpdate` to its own connection only

#### Scenario: EntityRemovedDelta with unknown ID is dropped silently
- **WHEN** a session receives an `EntityRemovedDelta` for an entity ID not in its mirror
- **THEN** the session SHALL log the discrepancy
- **AND** SHALL NOT throw an exception
- **AND** SHALL NOT crash the connection

#### Scenario: EntityAddedDelta reconstructs via EntityFactory
- **WHEN** a session receives an `EntityAddedDelta { Placement }`
- **THEN** the session SHALL use `EntityFactory.Create(Placement)` to instantiate the entity
- **AND** SHALL add it to its `World` mirror via `World.AddEntity`

#### Scenario: ItemTransferredDelta updates world and inventory atomically
- **WHEN** a session receives an `ItemTransferredDelta` describing an item moving from the world into a player's inventory (or vice versa)
- **THEN** the session SHALL update both the world entity index and the relevant inventory under the same `_stateLock` acquisition

### Requirement: Cross-Session Player Visibility
Player `Character` entities SHALL be visible to other joiners of the same map. Movement of one player SHALL produce `EntityMovedDelta` notifications visible to other sessions in the same SignalR group. `WorldSnapshot` payloads served to a joiner SHALL include all currently-joined player `Character` entities except the joiner's own.

#### Scenario: Joining player sees existing players
- **WHEN** player B joins a map where player A is already joined
- **THEN** the `WorldSnapshot` returned by `GetWorldSnapshotAsync` SHALL include an `EntityPlacement` for A's `Character`
- **AND** B's hydrated `World` SHALL include A's `Character` at A's current location

#### Scenario: Other player's movement is observable
- **WHEN** player A moves via `IGameMapGrain.MoveAsync`
- **THEN** the grain SHALL emit an `EntityMovedDelta { EntityId: A.SessionId, OldLocation, NewLocation }`
- **AND** player B's session SHALL receive the delta via the SignalR group
- **AND** B's `World` mirror SHALL update A's position
- **AND** B's perception SHALL reflect A's new position (subject to FOV)

### Requirement: Character Heading is Server-Authoritative
Each `Character` entity SHALL carry its heading as a server-side `HasHeading` component owned by the grain's `_world`. Rotation mutations SHALL update the component, not session-local state. Perception of *other* characters SHALL NOT leak heading information by default — observers see position but not facing direction. (A future change MAY add a "compass-equipped observers see nearby characters' headings" perception filter.)

#### Scenario: RotateAsync mutates the Character's HasHeading
- **WHEN** a player invokes `IGameMapGrain.RotateAsync(sessionId, degrees)`
- **THEN** the grain SHALL update the player Character's `HasHeading.Degrees` in `_world`
- **AND** the grain SHALL emit an `EntityHeadingChangedDelta` to the actor's own session
- **AND** the actor's session SHALL recompute its perception (the rotation changes what's in directional vision cones)

#### Scenario: Heading is omitted from default perception of other characters
- **WHEN** session A computes perception that includes another player B's `Character` entity
- **THEN** the perception data SHALL NOT include B's `HasHeading.Degrees` value
- **AND** session A's rendering SHALL show B's position but not B's facing direction

#### Scenario: GameSession.Heading reads through to the player Character
- **WHEN** code reads `session.Heading` or `session.HeadingDegrees`
- **THEN** the value SHALL come from `session.Player.Get<HasHeading>().Degrees` (or equivalent member)
- **AND** writing to those properties SHALL update the Character's `HasHeading` component, which (for grain-bound sessions) routes through `IMapMutationGateway.RotateAsync`

### Requirement: Heat Trail Tracking is Grain-Authoritative
The `HeatTrailTracker` for a map SHALL be owned by `IGameMapGrain`, not by individual sessions. Heat recording and decay SHALL be driven by grain-side mutations and tick events. Sessions SHALL maintain a local `HeatTrailTracker` mirror that converges to the grain's via `HeatRecordedDelta` and `HeatExpiredDelta` deltas. Whether a session can *perceive* a heat trail (e.g. `VisionMode.Infrared` reveals trails the eye can't see) remains a session-side perception filter on the same underlying data. Sessions SHALL NOT collect heat by iterating their local mirror's entities — the per-perception `UpdateHeatTracker` pass is removed; heat flows in only via deltas.

#### Scenario: Movement records heat in the grain
- **WHEN** any entity with a `HeatSignature` component moves and the move fires an `EntityMoved` event on `_world.WorldEvents`
- **THEN** the grain's subscriber SHALL call `_heatTracker.RecordEntityPosition` at the destination location with the current game time
- **AND** the grain SHALL emit a `HeatRecordedDelta { EntityId, X, Y, Z, GameTimeHours, Intensity }`
- **AND** the delta SHALL propagate to every session bound to the map via the host-side broker (`GameSessionManager.NotifyMapMutationAsync`)

#### Scenario: Heat expiry fires from the grain
- **WHEN** `IGameMapGrain.TickAsync` runs and `HeatTrailTracker.CleanupOldTrails(cutoff)` removes trails older than the retention window
- **THEN** the grain SHALL emit a `HeatExpiredDelta { X, Y, Z }` for each cell whose last trail was just cleared
- **AND** the grain SHALL NOT emit expiry deltas for cells that already had no trails before the cleanup

#### Scenario: Session ApplyDelta updates the local mirror
- **WHEN** a session receives a `HeatRecordedDelta`
- **THEN** the session SHALL update its local `HeatTrailTracker` with the same `(location, intensity, gameTime)` triple
- **AND** if the entity referenced by `EntityId` is in the session's local mirror, its reference SHALL be attached
- **AND** if the entity is not in the session's mirror, the heat SHALL still be recorded — heat is observable independently of entity visibility

#### Scenario: Session ApplyDelta removes expired trails
- **WHEN** a session receives a `HeatExpiredDelta`
- **THEN** the session SHALL call `HeatTrailTracker.RemoveTrailsAt(new WorldLocation(X, Y, Z))` to clear that cell's trails from the local mirror

#### Scenario: Two sessions in the same map have identical heat-tracker mirrors
- **WHEN** sessions A and B are joined to the same map and a sequence of movements occurs
- **THEN** after delta propagation, A's local heat data and B's local heat data SHALL be equivalent (same trails at same cells, same intensities, same timestamps)
- **AND** rendering differences SHALL come only from per-session vision modes, not from divergent data

#### Scenario: GetPerception no longer drives heat collection
- **WHEN** a session's `GetPerception` is invoked
- **THEN** it SHALL NOT iterate `World.Entities` to populate the heat tracker
- **AND** the heat tracker's content reflects only what `ApplyDelta` has put there

### Requirement: ActionContext Stateless API for Grain-Routed Verbs
`InteractionSystem` SHALL expose `TryPickup`, `TryDrop`, `TryOpen`, and `TryClose` as pairs of overloads: one taking `GameSession` (legacy / `LocalMutationGateway` consumers) and one taking `ActionContext { World, Character Player, WorldLocation ViewLocation }` (grain consumers). The session overload SHALL be a thin forwarder. The `ActionContext` overload SHALL hold the canonical implementation. Other verbs (`TryUse` and the disambiguation chain `GetUseOptions` / `TryUseWithMode` / `TryActivate` / `TryConsume` / `TryPlace` / `TryClimb` / `TryForceOpen` / `TryLockpick` / `TryEquip`) remain session-bound in this change and are reachable only via `LocalMutationGateway`; their migration is deferred to a future change that also extends the delta DTO vocabulary to cover the post-conditions those verbs trigger.

#### Scenario: Both overloads produce identical outcomes
- **WHEN** a caller invokes `interactionSystem.TryPickup(session, targetId)` and another caller invokes `interactionSystem.TryPickup(new ActionContext(session.World, session.Player, session.ViewLocation), targetId)` on equivalent state
- **THEN** both calls SHALL produce equivalent `InteractionResult` values
- **AND** both calls SHALL mutate the underlying world identically (same entity moved/removed, same inventory changes)

#### Scenario: ActionContext fields are non-null
- **WHEN** `ActionContext` is constructed
- **THEN** the record SHALL require non-null `World`, `Player`, and `ViewLocation`
- **AND** a caller that has null values SHALL surface that as a precondition failure before constructing the context (the session overload's null-checks remain the canonical guard)

#### Scenario: Use disambiguation remains session-bound
- **WHEN** any caller invokes `InteractionSystem.TryUse` or any of the disambiguation helper methods
- **THEN** the call SHALL go through the existing session-taking signature
- **AND** the grain's `IGameMapGrain.UseAsync` SHALL retain its native key-on-door-only implementation until a future change migrates the Use chain

### Requirement: End-to-End Multiplayer Validation
The test suite SHALL include at least one end-to-end test that exercises the full grain mutation → host-side broker → per-session perception dispatch chain against a real Orleans cluster, using either an Orleans `TestCluster` with a captured `IHubContext` substitute, or `WebApplicationFactory<Aetherium.Server.Program>` with co-hosted Orleans. The test SHALL verify that a gameplay mutation invoked on one session's behalf produces a `ReceivePerceptionUpdate` dispatch for every other session bound to the same map.

#### Scenario: Two sessions in the same world see each other join
- **WHEN** two sessions join the same map via `IGameMapGrain.JoinPlayerAsync`
- **THEN** each session's hydrated `World` SHALL contain the other's `Character` entity
- **AND** the host-side delta broker SHALL have been invoked with an `EntityAddedDelta` for each joiner

#### Scenario: Mutation propagates to host-side dispatch
- **WHEN** one session invokes a gameplay verb through `IMapMutationGateway` (or directly through `IGameMapGrain` in a test)
- **THEN** the grain SHALL apply the mutation to `_world`
- **AND** the host-side `GameSessionManager.NotifyMapMutationAsync` SHALL invoke `IHubContext.Clients.Client(connectionId).SendAsync("ReceivePerceptionUpdate", ...)` for every joined session in the map

#### Scenario: Leave removes player visibility from other sessions
- **WHEN** a session's `IGameMapGrain.LeavePlayerAsync` is invoked
- **THEN** the grain SHALL remove the player Character from `_world`
- **AND** SHALL emit an `EntityRemovedDelta` for that player
- **AND** the remaining session(s) SHALL receive a fresh `ReceivePerceptionUpdate` whose perception no longer references the departed Character

