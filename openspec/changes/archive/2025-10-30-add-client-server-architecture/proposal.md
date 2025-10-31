## Why

The monolithic console game architecture coupled UI rendering with game logic, making it impossible to support multiple players, remote clients, or server-side game state management. This change enables true client-server separation where the authoritative game engine runs on a server and clients receive only perception data (what they can see/hear).

## What Changes

- **NEW**: Client-server communication via SignalR with real-time bidirectional messaging
- **NEW**: Server-authoritative game engine hosting (World, entities, AI, perception computation)
- **NEW**: Perception-based data transfer - clients receive only visible tiles/entities (FOV-respecting)
- **NEW**: Command pattern for player actions sent from client to server
- **MODIFIED**: Console client becomes thin UI layer with no direct World access
- **ADDED**: Shared DTO model for serializable game state representation

## Impact

- Affected specs: New capability `client-server-communication`
- Affected code:
  - New project: `Aetherium.Model` (shared DTOs)
  - New project: `Aetherium.Server` (ASP.NET Core with game engine)
  - Modified project: `Aetherium.Console` (thin client with SignalR)
  - New files: GameHub, GameSession, PerceptionService, GameClient
  - Modified files: Program.cs, ConsoleMapView → ClientConsoleMapView


