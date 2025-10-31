# Quick Test - Client-Server Architecture

## Status: ✅ All Systems Operational

### Build Results
- **Aetherium.Model**: ✅ Built successfully
- **Aetherium.Server**: ✅ Built successfully  
- **Aetherium.Console**: ✅ Built successfully
- **Aetherium.Test**: ✅ All 91 tests passed

### How to Run

#### Terminal 1 (Server)
```powershell
cd Aetherium.Server
dotnet run
```

Expected output:
```
Console Game Server starting on http://localhost:5000
Waiting for client connections...
```

#### Terminal 2 (Client)
```powershell
cd Aetherium
dotnet run
```

The client will connect and you can play the game!

### Controls
- **Arrow Keys**: Move around
- **Z**: Rotate left
- **X**: Rotate right
- **U/D**: Change levels
- **J**: Jump to random location

### What's Different?
**Before**: Single process running the entire game  
**After**: Two processes communicating via SignalR
- Server hosts the game world and all logic
- Client only receives perception data (what you can see)
- All input is sent to server, which processes and sends back updated perception

### Architecture Highlights
1. **Server-Authoritative**: All game logic runs on the server
2. **Perception-Based**: Client only knows what the player can see (FOV/lighting)
3. **Real-Time**: SignalR provides low-latency bidirectional communication
4. **Identical Gameplay**: Plays exactly like the original single-process version




