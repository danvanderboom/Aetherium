# Console Game - Client-Server Architecture

## Overview

The console game has been split into a client-server architecture using SignalR for real-time communication.

## Architecture

- **ConsoleGameModel**: Shared DTOs and data structures used by both client and server
- **ConsoleGameServer**: ASP.NET Core server hosting the game engine
- **ConsoleGameClient**: Console application client that connects to the server

## Running the Application

### Step 1: Start the Server

Open a terminal in the project root and run:

```powershell
cd ConsoleGameServer
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
cd ConsoleGame
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

## Key Features

- Real-time communication via SignalR
- Server-authoritative game logic
- Client only receives what the player can perceive (FOV-based)
- Lighting and vision systems computed server-side
- Automatic reconnection if connection drops

## Project Structure

```
ConsoleGame/
├── ConsoleGameModel/           # Shared DTOs
│   ├── PerceptionDto.cs
│   ├── VisualDto.cs
│   ├── WorldLocationDto.cs
│   └── ...
├── ConsoleGameServer/          # Server (game engine)
│   ├── Core/                   # World, Entity, Component
│   ├── Components/             # Game components
│   ├── Entities/               # Character, Monster, etc.
│   ├── Perception/             # Vision & FOV systems
│   ├── Lighting/               # Lighting system
│   ├── GameHub.cs              # SignalR hub
│   ├── GameSession.cs          # Per-client game state
│   └── Program.cs              # Server entry point
└── ConsoleGame/                # Client (console UI)
    ├── Client/                 # SignalR client
    ├── Views/                  # Console rendering
    ├── Core/ClientConsoleDungeonGame.cs
    └── Program.cs              # Client entry point
```

## Development

### Building

```powershell
# Build all projects
dotnet build ConsoleGame.sln

# Build individual projects
dotnet build ConsoleGameModel/ConsoleGameModel.csproj
dotnet build ConsoleGameServer/ConsoleGameServer.csproj
dotnet build ConsoleGame/ConsoleGameClient.csproj
```

### Testing

Run the test suite:
```powershell
cd ConsoleGame.Test
dotnet test
```

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

