## ADDED Requirements

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

## MODIFIED Requirements

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
