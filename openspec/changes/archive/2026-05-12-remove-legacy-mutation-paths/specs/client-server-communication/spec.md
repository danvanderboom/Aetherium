## MODIFIED Requirements

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

## REMOVED Requirements

### Requirement: Phase 1 Mutation Semantics
**Reason**: This requirement was the documentation guardrail for the phase-1 "independent mutation per session" model. After `add-grain-authoritative-mutation` shipped, the model changed and the requirement was MODIFIED to describe live shared mutation. After this change ships, the legacy phase-1 framing is no longer accurate even as a comparison point — the entire `?worldId=` flow runs in grain-authoritative mode and the legacy `OnConnectedAsync` private-world path is preserved purely for testing. The successor requirement that survives is `Server-Authoritative Game State`, which already captures the grain-canonical model.

**Migration**: No code action required. `CLIENT_SERVER_README.md` and the XML doc summaries already reflect the phase-2 mutation model after `add-grain-authoritative-mutation`. This removal is a cleanup of an obsolete spec node, not a behavior change. Developers looking for the historical context should consult the archived `add-hub-grain-snapshot-bridge` and `add-grain-authoritative-mutation` changes.
