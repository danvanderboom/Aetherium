# Game State Monitoring System

The Console Game Client now includes a built-in monitoring service that allows PowerShell scripts and other tools to subscribe to real-time game state updates via WebSocket.

## Features

- **Real-time WebSocket streaming** of game state updates
- **Raw perception data** (JSON) - Complete game state as seen by the client
- **Rendered ASCII map** - 2D array of map tiles as displayed in the console
- **Optional file logging** - Save frames to human-readable text files
- **Zero performance impact** - Non-blocking broadcast architecture
- **No external dependencies** - Uses built-in .NET WebSocket support

## Quick Start

### 1. Start the Console Game Client

The monitoring service starts automatically with the game client on port **5001**:

```bash
cd Aetherium
dotnet run
```

You should see:
```
[Monitor] Listening on http://localhost:5001/
[Monitor] WebSocket endpoint: ws://localhost:5001/monitor
```

### 2. Connect with PowerShell

Open a new PowerShell window and run:

```powershell
cd scripts
.\monitor-game.ps1 -DisplayAsciiMap
```

This will connect to the monitoring service and display each frame update with the ASCII map.

## PowerShell Script Options

The `monitor-game.ps1` script supports several options:

```powershell
# Basic monitoring (just stats)
.\monitor-game.ps1

# Display ASCII maps with each update
.\monitor-game.ps1 -DisplayAsciiMap

# Save all frames to JSON files
.\monitor-game.ps1 -SaveToFile -OutputPath "./my-logs"

# Full verbose monitoring with everything
.\monitor-game.ps1 -DisplayAsciiMap -DisplayJson -SaveToFile -Verbose

# Connect to different port
.\monitor-game.ps1 -ServerUrl "ws://localhost:5002/monitor" -DisplayAsciiMap
```

### Options:
- `-ServerUrl` - WebSocket URL (default: `ws://localhost:5001/monitor`)
- `-SaveToFile` - Save each frame to a JSON file
- `-OutputPath` - Directory for saved files (default: `./monitor-output`)
- `-DisplayAsciiMap` - Show the ASCII map with each update
- `-DisplayJson` - Show the full JSON message
- `-Verbose` - Show additional debug information

## WebSocket Endpoints

The monitoring service exposes several HTTP endpoints:

### WebSocket Endpoint
```
ws://localhost:5001/monitor
```
Main WebSocket endpoint for subscribing to frame updates.

### Health Check
```
GET http://localhost:5001/health
```
Returns JSON with service health status:
```json
{
  "status": "healthy",
  "connectedClients": 2,
  "framesProcessed": 156
}
```

### Configuration
```
GET http://localhost:5001/config
```
Returns the current monitoring configuration.

## Message Format

Each frame update sent over WebSocket has this structure:

```json
{
  "type": "frame",
  "data": {
    "timestamp": "2025-10-30T12:34:56.789Z",
    "frameNumber": 42,
    "rawPerception": {
      "playerLocation": { "x": 0, "y": 0, "z": 0 },
      "playerHeading": "North",
      "visuals": { /* dictionary of visible tiles */ },
      "inventory": { /* inventory data */ },
      "affordances": [ /* available actions */ ]
    },
    "asciiMap": {
      "width": 21,
      "height": 22,
      "tiles": [
        ["##", "##", "  ", "@@", ...],
        ["##", "  ", "  ", "  ", ...],
        ...
      ]
    }
  }
}
```

## Configuration

Monitoring can be configured in `Aetherium.Console/Program.cs`:

```csharp
var monitoringConfig = new MonitoringConfig
{
    Enabled = true,           // Enable/disable monitoring
    Port = 5001,              // WebSocket server port
    FileLogging = new FileLoggingConfig
    {
        Enabled = false,      // Enable file-based logging
        OutputPath = "./monitoring-logs"
    }
};
```

### File Logging

When file logging is enabled, each frame is written to a human-readable text file with:
- Frame number and timestamp
- Player location and heading
- Inventory summary
- ASCII map with border characters

Files rotate after 100 frames to prevent excessive file sizes.

## Use Cases

### Automated Testing
```powershell
# Capture 100 frames for analysis
.\monitor-game.ps1 -SaveToFile -OutputPath "./test-run-001"
# Later: analyze the JSON files programmatically
```

### Real-time Debugging
```powershell
# Watch the game state live
.\monitor-game.ps1 -DisplayAsciiMap -Verbose
```

### AI/Bot Development
Connect your AI bot to `ws://localhost:5001/monitor` to:
- Observe the game state in real-time
- Parse perception data to make decisions
- Test navigation algorithms

### Performance Monitoring
```powershell
# Check service health
Invoke-RestMethod http://localhost:5001/health
```

## Architecture

The monitoring system consists of:

1. **MapFrameMonitor** - Singleton service managing WebSocket connections
2. **MonitoringModels** - Data structures for frame updates
3. **MapFrameLogger** - Optional file-based logging
4. **ClientConsoleMapView.CaptureRenderedFrame()** - Captures ASCII representation
5. **Integration in ClientConsoleDungeonGame** - Broadcasts on each perception update

The system uses:
- `System.Net.HttpListener` for HTTP server
- `System.Net.WebSockets` for WebSocket connections
- Non-blocking async broadcasts
- Thread-safe concurrent collections

## Troubleshooting

### Port Already in Use
If port 5001 is already in use, change it in `Program.cs`:
```csharp
Port = 5002  // or any other available port
```

### PowerShell WebSocket Errors
Ensure you're running PowerShell 5.1 or later. The script uses .NET WebSocket classes.

### No Frames Received
1. Verify the game client is running
2. Check the monitoring service started (look for "[Monitor] Listening..." message)
3. Verify connection URL matches the configured port
4. Move around in the game to trigger perception updates

## Performance Notes

- The monitoring system is designed to have **zero impact** on game performance
- Broadcasts are fire-and-forget (non-blocking)
- Failed client sends don't affect other clients or the game
- Multiple clients can connect simultaneously
- Frame capture happens in the existing render pipeline

## Example Output

```
=== Console Game Monitor ===
Connecting to: ws://localhost:5001/monitor

Connected successfully!

Waiting for frame updates... (Press Ctrl+C to stop)
============================================================

────────────────────────────────────────────────────────────
Frame #1 - 2025-10-30T12:34:56.789Z
  Player Location: (0, 0, 0)
  Player Heading: North
  Visible Tiles: 462
  Inventory: 2/10
    Items: Key, Torch

  Map (21x22):
  ┌──────────────────────────────────────────┐
  │##########################################│
  │##########################################│
  │####                            ##########│
  │####  @@                        ##########│
  │####                            ##########│
  │##########################################│
  └──────────────────────────────────────────┘

────────────────────────────────────────────────────────────
Frame #2 - 2025-10-30T12:34:57.012Z
...
```


