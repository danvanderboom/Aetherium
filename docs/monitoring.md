# Quick Start: Game Monitoring System

## Starting the Game with Monitoring

1. **Start the game server** (if not already running):
   ```powershell
   cd Aetherium.Server
   dotnet run
   ```

2. **Start the game client with monitoring**:
   ```powershell
   cd Aetherium.Console
   dotnet run
   ```
   
   You should see:
   ```
   [Monitor] Listening on http://localhost:5001/
   [Monitor] WebSocket endpoint: ws://localhost:5001/monitor
   Connecting to server...
   ```

## Connecting a Monitor Script

In a new PowerShell window:

```powershell
cd scripts
.\monitor-game.ps1 -DisplayAsciiMap
```

## Common Commands

### Basic monitoring (stats only)
```powershell
.\monitor-game.ps1
```

### With ASCII map visualization
```powershell
.\monitor-game.ps1 -DisplayAsciiMap
```

### Save frames to files
```powershell
.\monitor-game.ps1 -SaveToFile -OutputPath "./my-test-run"
```

### Full debug mode
```powershell
.\monitor-game.ps1 -DisplayAsciiMap -DisplayJson -SaveToFile -Verbose
```

## Configuration

To enable file logging, edit `Aetherium.Console/Program.cs`:

```csharp
var monitoringConfig = new MonitoringConfig
{
    Enabled = true,
    Port = 5001,
    FileLogging = new FileLoggingConfig
    {
        Enabled = true,  // Change this to true
        OutputPath = "./monitoring-logs"
    }
};
```

## Endpoints

- **WebSocket**: `ws://localhost:5001/monitor`
- **Health Check**: `http://localhost:5001/health`
- **Config**: `http://localhost:5001/config`

## Troubleshooting

### "Cannot connect to server"
- Ensure the game client is running
- Check that port 5001 isn't blocked by firewall
- Verify monitoring is enabled in Program.cs

### "No frames received"
- Move around in the game to trigger perception updates
- Check the game client console for errors
- Try the health check: `Invoke-RestMethod http://localhost:5001/health`

### Port already in use
Change the port in `Aetherium.Console/Program.cs`:
```csharp
Port = 5002  // or any other available port
```

## What You Get

Each frame update contains:

1. **Player State**:
   - Location (relative coordinates)
   - Heading direction
   - Inventory contents
   - Available actions

2. **Visual Data**:
   - All visible tiles
   - Terrain types
   - Light levels
   - Items on ground

3. **ASCII Map**:
   - 2D array of tiles
   - Player marked as `@@`
   - Terrain and items shown with their characters
   - Matches what's displayed on screen

## Example Output

```
Frame #42 - 2025-10-30T12:34:56.789Z
  Player Location: (0, 0, 0)
  Player Heading: North
  Visible Tiles: 462
  Inventory: 2/10
    Items: Key, Torch

  Map (21x22):
  ┌──────────────────────────────────────────┐
  │##########################################│
  │####                            ##########│
  │####  @@                        ##########│
  │####                            ##########│
  │##########################################│
  └──────────────────────────────────────────┘
```

## For More Details

See:
- `Aetherium.Console/Monitoring/README.md` - Full documentation
- [history/MONITORING_IMPLEMENTATION_SUMMARY.md](history/MONITORING_IMPLEMENTATION_SUMMARY.md) - Original implementation report (archived)


