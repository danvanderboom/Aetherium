# Console Game - Client-Server Architecture

## Overview

The console game has been split into a client-server architecture using SignalR for real-time communication.

## Architecture

- **Aetherium.Model**: Shared DTOs and data structures used by both client and server
- **Aetherium.Server**: ASP.NET Core server hosting the game engine
- **Aetherium.Console**: Console application client that connects to the server

## Running the Application

### Step 1: Start the Server

Open a terminal in the project root and run:

```powershell
cd Aetherium.Server
dotnet run
```

The server will start on `http://localhost:5000` and display:
```
Console Game Server starting on http://localhost:5000
Waiting for client connections...
```

### Step 2: Start the Client

Open a **second** terminal in the project root and run:

```powershell
cd Aetherium
dotnet run
```

The client will connect to the server and you can start playing.

## How It Works

1. **Server** hosts the complete game engine (World, Entities, AI, Perception systems)
2. **Client** connects via SignalR and receives only perception data (what the player can see)
3. Player inputs (arrow keys, etc.) are sent from client to server
4. Server processes the input, updates the game world, and sends updated perception back to client

## Controls

- **Arrow Keys**: Move forward/backward/left/right
- **Z**: Rotate left (counter-clockwise)
- **X**: Rotate right (clockwise)
- **U/D**: Move up/down levels
- **0** (zero): Return to level 0
- **J**: Jump to random location
- **M** + digit: Toggle grid coloring (0, 1, or 2)

### Interaction Commands
- **E or I**: Unified interact menu - shows all available actions (pickup, drop, use, open, close) based on current affordances
- **, (comma)**: Quick pickup (picks up first visible item)
- **. (period)**: Quick drop (drops last item in inventory)
- **O**: Quick open (opens first available door)
- **Ctrl+C**: Quick close (closes first available door)

The unified interact command (E/I) displays a numbered menu of all available actions at your location, making it easy for both human players and AI agents to see their options. Actions are grouped by type and include descriptions (e.g., "Pick up Key", "Use item on door (requires red key)").

**Inventory Display**: Your inventory appears below the map showing `[count/capacity]: item list`. Items with keys show their key ID (e.g., "Key(red)").

Client API (`Aetherium.Console/Client/GameClient.cs`) exposes `PickupAsync`, `DropAsync`, `UseAsync`, `OpenAsync`, `CloseAsync`. Each returns `InteractionResultDto { Success, Reason }` and triggers a perception update.

## Key Features

- Real-time communication via SignalR
- Server-authoritative game logic
- Client only receives what the player can perceive (FOV-based)
- Lighting and vision systems computed server-side
- Automatic reconnection if connection drops
- Orleans SignalR backplane for distributed scaling
- Azure AD B2C authentication for management operations

## Joining a Multi-World Map (Phase 1)

By default a client that connects to `GameHub` lands in a private, session-local
diagnostic world built by `FovDiagnosticWorldBuilder`. To bind a session to a
real Orleans-hosted world instead, include a `worldId` (and optional `mapId`)
query parameter on the SignalR connection URL:

```
ws://localhost:5000/gamehub?worldId=world-abc-123
```

The hub will:

1. Resolve the world via `IGameManagementGrain` / `IWorldGrain`.
2. Pick a map (the supplied `mapId`, or the world's first map).
3. Call `IGameMapGrain.JoinPlayerAsync` to reserve a unique spawn cell.
4. Fetch a `WorldSnapshot` from the grain and hydrate the session's `World`
   from it via `SnapshotWorldBuilder`.

Clients can also call `JoinWorld(worldId, mapId?)` explicitly after connect to
re-bind their session to a different world.

### Multiplayer model (phases 1, 2a, 2c)

Sessions are bound to a grain-hosted world via the `?worldId=` query
parameter or an explicit `JoinWorld(worldId, mapId?)` hub call. Once bound:

- **Mutations are grain-authoritative.** Gameplay tools (`move`, `rotate`,
  `pickup`, `drop`, `use`, `open`, `close`, `changelevel`) dispatch through
  `IMapMutationGateway`. For grain-bound sessions, that gateway is a
  `GrainMutationGateway` that routes calls to `IGameMapGrain` methods. The
  grain mutates its canonical `_world` and emits a `MapDelta`.
- **Other joiners see the change live.** The host-side `GameSessionManager`
  receives the delta, applies it to each affected session's local `World`
  mirror, and pushes a fresh `ReceivePerceptionUpdate` to each client over
  SignalR.
- **The wire is perception-pure.** Clients never receive raw deltas. They
  only ever see filtered `PerceptionDto`s. This means cells outside a
  player's FOV never reach the wire even when other players mutate them.
- **Character heading is server-authoritative.** A character's facing
  direction lives on the `HasHeading` component of the grain-owned
  `Character` entity. Perception of *other* characters does not expose
  their heading by default — discovering which way someone is facing
  requires a future "compass-style" perception filter (not yet implemented).

Legacy sessions (no `?worldId=`) continue to use a session-local
`FovDiagnosticWorldBuilder` world with `LocalMutationGateway`. They behave
exactly as they did pre-phase-2 and are unaffected by the multiplayer model.

### What phase 2 does NOT yet do

- **Heat trails on the grain.** Heat is still tracked client-side per
  session today. The wire format includes `HeatRecordedDelta` /
  `HeatExpiredDelta` and `GameSession.ApplyDelta` has placeholder handlers,
  but the grain doesn't yet record heat or emit those deltas. Scheduled for
  phase 2.1.
- **Player persistence.** When a player disconnects, their `Character` is
  removed from the grain's world via `LeavePlayerAsync` and other players
  see them depart. Reconnect creates a fresh player. Persistence across
  sessions — with placement policies (same location, repositioned, kept
  visible-but-idle) — is scheduled for a future `add-player-persistence`
  change.
- **Complex `Use` interactions** (consume food, light torch, place crystal,
  lockpick, climb) are only available in legacy mode. Grain-bound `Use`
  supports the "key on locked door" pattern; other modes return a
  "not supported in grain mode" failure. Resolving this requires refactoring
  `InteractionSystem` to stateless `(World, Character, WorldLocation)`
  overloads — a separate follow-up change. Phase 2d deliberately did not
  bundle that work in with its hub-surface cleanup.

## Wire protocol notes (post phase 2d)

Phase 2d removed the per-verb hub methods from `GameHub` (`MovePlayer`,
`Pickup`, `Open`, etc.). All gameplay verbs now go through a single
`ExecuteTool(toolId, args)` invocation. The `Aetherium.Console` client
preserves its method-level API surface (`MovePlayerAsync`, `PickupAsync`,
etc.) — those methods now wrap `ExecuteToolAsync` internally. Other
client implementations should follow the same pattern: send `ExecuteTool`
with the appropriate tool ID and argument dictionary.

Tool ID reference for legacy verb mappings:

| Old hub method | Tool ID | Args |
|---|---|---|
| `MovePlayer(direction, distance)` | `"move"` | `direction` (string F/B/L/R/N/E/S/W), `distance` (int) |
| `RotatePlayer(clockwise)` | `"rotate"` | `clockwise` (bool) OR `degrees` (int) |
| `ChangeLevel(deltaZ)` | `"changelevel"` | `delta` (int) |
| `JumpToRandomLocation()` | `"jumptolocation"` | (none) |
| `ToggleDirectionalVision()` | `"toggledirectionalvision"` | (none) |
| `Pickup(id)` | `"pickup"` | `targetEntityId` (string) |
| `Drop(id)` | `"drop"` | `itemEntityId` (string) |
| `Use(item, target, usage?)` | `"use"` | `itemEntityId`, `onEntityId`, `usageId?` |
| `Open(id)` | `"open"` | `targetEntityId` |
| `Close(id)` | `"close"` | `targetEntityId` |
| `SetLightingMode(mode)` | `"setlightingmode"` | `mode` (string) |
| `SetVisionMode(mode)` | `"setvisionmode"` | `mode` (string) |

## SignalR Configuration

### Orleans SignalR Backplane

The server uses `UFX.Orleans.SignalRBackplane` for distributed SignalR scaling. The backplane is automatically configured when Orleans is enabled - no explicit configuration needed.

**How it works:**
- SignalR connections, users, and groups are represented as Orleans grains
- Messages are distributed across silos using Orleans infrastructure
- No external dependencies (Redis, Azure SignalR Service) required
- Scales horizontally with Orleans cluster

**Configuration:**
- Automatically enabled when `UFX.Orleans.SignalRBackplane` package is referenced
- Orleans must be enabled (not disabled via `DISABLE_ORLEANS=1`)
- Configured in `Aetherium.Server/Program.cs` during service setup

### SignalR Hubs

#### GameHub (`/gamehub`)
- **Purpose**: Gameplay communication (client-server game state)
- **Authentication**: Optional (can be configured for authenticated gameplay)
- **Features**: Player actions, perception updates, game state synchronization

#### ManagementHub (`/managementHub`)
- **Purpose**: World management operations
- **Authentication**: Required (Azure AD B2C with Admin role for write operations)
- **Features**: World creation, pause/resume, shutdown, status queries
- **Client**: Use `Aetherctl.SignalR.ManagementClient` from `aetherctl` CLI

#### AgentDashboardHub (`/agentDashboardHub`)
- **Purpose**: Agent telemetry and monitoring
- **Authentication**: Optional
- **Features**: Agent status, activity monitoring, telemetry streaming

## DTO Additions

- `InventoryDto { Capacity, Items: ItemDto[] }`
- `ItemDto { Id, Label, Icon, KeyId? }`
- `AffordanceDto { Action, ActorId, TargetId?, RequiresKeyId? }`
- `PerceptionDto` now includes `Inventory`, `VisibleItems`, and `Affordances`.

## Project Structure

```
Aetherium.Console/
├── Aetherium.Model/           # Shared DTOs
│   ├── PerceptionDto.cs
│   ├── VisualDto.cs
│   ├── WorldLocationDto.cs
│   └── ...
├── Aetherium.Server/          # Server (game engine)
│   ├── Core/                   # World, Entity, Component
│   ├── Components/             # Game components
│   ├── Entities/               # Character, Monster, etc.
│   ├── Perception/             # Vision & FOV systems
│   ├── Lighting/               # Lighting system
│   ├── GameHub.cs              # SignalR hub
│   ├── GameSession.cs          # Per-client game state
│   └── Program.cs              # Server entry point
└── Aetherium.Console/                # Client (console UI)
    ├── Client/                 # SignalR client
    ├── Views/                  # Console rendering
    ├── Core/ClientConsoleDungeonGame.cs
    └── Program.cs              # Client entry point
```

## Development

### Building

```powershell
# Build all projects
dotnet build Aetherium.sln

# Build individual projects
dotnet build Aetherium.Model/Aetherium.Model.csproj
dotnet build Aetherium.Server/Aetherium.Server.csproj
dotnet build Aetherium.Console/Aetherium.Console.csproj
```

### Testing

Run the test suite:
```powershell
# Run all tests
dotnet test Aetherium.sln

# Run tests in Release configuration
dotnet test Aetherium.sln -c Release

# Run specific test project
cd Aetherium.Test
dotnet test
```

**Current Test Status**: 597 passed, 0 failed, 2 skipped

For detailed testing information, see [Development Guide - Testing](../docs/development.md#testing).

## Troubleshooting

### Server won't start
- Ensure port 5000 is not already in use
- Check firewall settings

### Client can't connect
- Ensure server is running first
- Check that server is listening on http://localhost:5000
- Verify no firewall is blocking the connection

### Game rendering issues
- Ensure terminal supports Unicode characters
- Try a different terminal (Windows Terminal recommended)


