## MODIFIED Requirements

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

### Requirement: Phase 1 Mutation Semantics
The system SHALL document and surface the active mutation model in user-facing return values, source-code summaries, and operator-facing documentation. For grain-bound sessions, mutations SHALL be grain-authoritative and propagate live to all sessions in the same map. For legacy sessions, mutations SHALL remain local to the session. Doc surfaces SHALL be kept consistent with the phase-2 behavior; the phase-1 "independent mutation" caveat SHALL no longer appear in user-facing copy after this change ships.

#### Scenario: JoinWorldResult and related summaries describe live shared mutation
- **WHEN** a developer reads the XML doc summary for `JoinWorldResult`, `WorldSnapshot`, `GameSession.ReplaceWorld`, or `GrainMutationGateway`
- **THEN** the summary SHALL describe live shared mutation for grain-bound sessions
- **AND** SHALL note that legacy sessions (no `worldId` query parameter) remain locally-authoritative

#### Scenario: CLIENT_SERVER_README documents the multiplayer model
- **WHEN** a developer reads `CLIENT_SERVER_README.md`
- **THEN** it SHALL describe how `?worldId=` activates grain-bound mode
- **AND** SHALL document that mutations in one client are visible to other clients in the same map
- **AND** SHALL NOT contain the previous "phase 1 = independent mutation per session" caveat

## ADDED Requirements

### Requirement: Grain Mutation Methods
`IGameMapGrain` SHALL expose typed mutation methods that apply gameplay actions to the grain's `_world`: `MoveAsync(sessionId, direction, distance)`, `RotateAsync(sessionId, degrees)`, `ChangeLevelAsync(sessionId, deltaZ)`, `PickupAsync(sessionId, targetEntityId)`, `DropAsync(sessionId, itemEntityId)`, `UseAsync(sessionId, itemEntityId, onEntityId, usageId)`, `OpenAsync(sessionId, targetEntityId)`, `CloseAsync(sessionId, targetEntityId)`. Orleans's single-threaded grain contract SHALL serialize concurrent invocations.

#### Scenario: Mutation applies to grain world and emits a delta
- **WHEN** a mutation method is invoked with a valid `sessionId`
- **THEN** the grain SHALL apply the mutation to `_world` using the stateless `InteractionSystem` API (or equivalent movement logic) targeting the player's `Character` entity
- **AND** the grain SHALL emit one or more `MapDelta` records via SignalR group fan-out
- **AND** the method SHALL return a typed result DTO indicating success or a structured failure reason

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
The `HeatTrailTracker` for a map SHALL be owned by `IGameMapGrain`, not by individual sessions. Heat recording and decay SHALL be driven by grain-side mutations and tick events. Sessions SHALL maintain a local `HeatTrailTracker` mirror that converges to the grain's via `HeatRecordedDelta` and `HeatExpiredDelta` deltas. Whether a session can *perceive* a heat trail (e.g. `VisionMode.Infrared` reveals trails the eye can't see) remains a session-side perception filter on the same underlying data.

#### Scenario: Movement records heat in the grain
- **WHEN** any entity with a `HeatSignature` component moves via grain mutation methods or tick-driven AI
- **THEN** the grain's `HeatTrailTracker` SHALL record the position at the current game time
- **AND** the grain SHALL emit a `HeatRecordedDelta { Location, EntityId, GameTime, Intensity }`
- **AND** every session in the map's SignalR group SHALL receive the delta

#### Scenario: Heat expiry fires from the grain
- **WHEN** the grain's periodic heat cleanup runs and removes trails older than the retention window
- **THEN** the grain SHALL emit a `HeatExpiredDelta { Location }` for each cleared cell
- **AND** session mirrors SHALL apply the delta to remove the trail from their local trackers

#### Scenario: Two sessions in the same map have identical heat-tracker mirrors
- **WHEN** sessions A and B are joined to the same map and a sequence of movements occurs
- **THEN** after delta propagation, A's `HeatTrailTracker.AllTrails` SHALL equal B's `HeatTrailTracker.AllTrails`
- **AND** rendering differences SHALL come only from per-session vision modes, not from divergent data
