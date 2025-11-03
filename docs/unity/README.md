# Unity 2D Client Documentation

## Overview

The Aetherium Unity client is a 2D tilemap-based client for PC and iOS platforms. It renders game state from Perception DTOs and allows players to interact via a unified tool API.

## Prerequisites

- Unity 2023.3 LTS or later
- Unity Hub (for project management)
- .NET 4.x / .NET Standard 2.1 scripting backend
- iOS build support (optional, for iOS builds)

## Quick Start

### Opening the Project

1. Launch Unity Hub
2. Click "Open" → "Add project from disk"
3. Navigate to `Aetherium.Unity/` folder
4. Unity will import the project (may take a few minutes on first open)

### Project Structure

```
Aetherium.Unity/
├── Assets/
│   ├── Scenes/
│   │   └── Main.unity          # Main game scene
│   ├── Scripts/
│   │   ├── Model/              # Unity-friendly DTO shims
│   │   ├── Networking/         # Perception providers, facade
│   │   ├── Rendering/          # Tilemap renderer, player controller
│   │   └── Spatial/            # Grid helpers
│   ├── StreamingAssets/
│   │   └── PerceptionFrames/  # JSON perception frames for mock mode
│   └── Tests/                  # EditMode and PlayMode tests
├── Packages/                   # Unity package manifests
└── ProjectSettings/            # Unity project settings
```

## Running the Client

### Offline Mock Mode (Default)

By default, the client runs in Offline Mock mode, replaying Perception JSON frames from `Assets/StreamingAssets/PerceptionFrames/`.

1. Ensure sample perception frames exist in `Assets/StreamingAssets/PerceptionFrames/`
2. Open `Assets/Scenes/Main.unity`
3. Press Play in Unity Editor
4. You should see:
   - A tilemap rendering visible tiles
   - A player marker sprite at the player location
   - A HUD showing current Z-level, heading, and status

### Live Mode (Optional)

Live mode connects to the server via SignalR at `http://localhost:5000/gamehub`.

**Prerequisites:**
- Server must be running (see [CLIENT_SERVER_README.md](../../CLIENT_SERVER_README.md))
- SignalR client support must be enabled

**Enabling Live Mode:**

1. In Unity Editor, go to **Edit → Project Settings → Player**
2. Under **Other Settings → Scripting Define Symbols**, add `USE_SIGNALR`
3. Click **Apply**
4. Rebuild the project
5. Ensure `GameClientFacade.SetMode(true)` is called at runtime (or configure via Inspector)

**Note:** Live mode requires the `Microsoft.AspNetCore.SignalR.Client` package. See "SignalR Client Setup" below.

## Controls

### Movement
- **WASD** or **Arrow Keys**: Move player (North, East, South, West)
- **Gamepad Left Stick**: Move player (cardinalized to NESW)
- Movement executes the "move" tool with appropriate direction

### Rotation
- **Z**: Rotate left (counter-clockwise)
- **X**: Rotate right (clockwise)
- **Gamepad LB (Left Bumper)**: Rotate left (counter-clockwise)
- **Gamepad RB (Right Bumper)**: Rotate right (clockwise)
- Rotation executes the "rotate" tool

### Z-Level Changes
- **PageUp** or **U**: Move up one Z-level
- **PageDown** or **D**: Move down one Z-level
- **Gamepad RT (Right Trigger)**: Move up one Z-level
- **Gamepad LT (Left Trigger)**: Move down one Z-level
- Z-level change executes the "changelevel" tool

### Tool Use
- **Gamepad A Button**: Use tool (context-dependent) or confirm option selection
- **Gamepad B Button**: Cancel option selection

### Multi-Option Selection
When a tool returns multiple usage options (e.g., multi-use items), the game enters option selection mode:
- **Gamepad D-Pad Up/Down**: Navigate through options
- **Gamepad A Button**: Confirm selected option
- **Gamepad B Button**: Cancel selection
- The HUD displays available options with a selection indicator (>>)
- During option selection, movement and other actions are disabled until confirmed or cancelled

## Keybindings Reference

| Action | Keyboard | Gamepad | Tool Executed |
|--------|----------|---------|---------------|
| Move North | W / ↑ | Left Stick Up | `move` with direction="north" |
| Move South | S / ↓ | Left Stick Down | `move` with direction="south" |
| Move East | D / → | Left Stick Right | `move` with direction="east" |
| Move West | A / ← | Left Stick Left | `move` with direction="west" |
| Rotate Left | Z | LB | `rotate` with clockwise=false |
| Rotate Right | X | RB | `rotate` with clockwise=true |
| Level Up | PageUp / U | RT | `changelevel` with up=true |
| Level Down | PageDown / D | LT | `changelevel` with up=false |
| Use / Confirm | - | A | Context tool use or confirm option |
| Cancel | - | B | Cancel option selection |
| Option Up | - | D-Pad Up | Navigate options up |
| Option Down | - | D-Pad Down | Navigate options down |

## Scene Setup

The Main.unity scene requires the following components:

1. **Grid GameObject** (2D Tilemap Grid)
   - Add `Grid` component
   - Child: `Tilemap` GameObject with `Tilemap` and `TilemapRenderer` components
   - Add `TilemapRenderer2D` script to Tilemap GameObject

2. **GameManager GameObject**
   - Add `GameManager` script
   - Add `GameClientFacade` script
   - Assign references in Inspector:
     - `GameClientFacade` → GameManager
     - `TilemapRenderer2D` → Tilemap component
     - `PlayerController` → Player GameObject

3. **Player GameObject**
   - Sprite renderer with a visible sprite (e.g., white square)
   - Add `PlayerController` script
   - Set initial position to (0, 0, 0)

4. **HUD Canvas**
   - UI Canvas with a Text element
   - Assign to `GameManager.hudText` in Inspector

5. **Input System Setup**
   - `Assets/InputActions.inputactions` should be imported
   - Create a PlayerInput component and assign InputActions
   - Or use direct Input System calls in PlayerController

See [Scene Setup Guide](#scene-setup-guide) for detailed steps.

## Perception JSON Format

Perception frames should match this structure:

```json
{
  "PlayerLocation": {
    "X": 0,
    "Y": 0,
    "Z": 0
  },
  "PlayerHeading": 0,
  "HeadingDegrees": 0,
  "VisibleBounds": {
    "X": -5,
    "Y": -5,
    "Width": 11,
    "Height": 11
  },
  "Visuals": {
    "0,0,0": {
      "Location": { "X": 0, "Y": 0, "Z": 0 },
      "TileTypeId": "stone",
      "LightLevel": 1.0
    }
  },
  "TileTypes": {
    "stone": {
      "Name": "Stone",
      "Settings": {}
    }
  }
}
```

Place JSON files in `Assets/StreamingAssets/PerceptionFrames/` for mock mode.

## Building

### PC (Windows)

1. **File → Build Settings**
2. Select **PC, Mac & Linux Standalone**
3. Choose **Windows** as target platform
4. Click **Build**
5. Select output folder

### iOS

1. Install iOS build support in Unity Hub
2. **File → Build Settings**
3. Select **iOS** as target platform
4. Click **Build**
5. Open generated Xcode project and build from Xcode

**Note:** SignalR Live mode may require additional configuration on iOS. See "SignalR Client Setup" below.

## SignalR Client Setup

To enable Live mode, you need to add the SignalR client package:

1. Install `Microsoft.AspNetCore.SignalR.Client` via NuGet or Unity Package Manager (if available)
2. Enable `USE_SIGNALR` scripting define (see "Live Mode" above)
3. Configure server URL in `GameClientFacade` or `PerceptionSignalRClient`

**Limitations:**
- SignalR client may not be directly available via Unity Package Manager
- May require manual DLL references or third-party Unity SignalR packages
- iOS builds may need additional configuration

**Fallback:** If SignalR client is not available, the client defaults to Offline Mock mode.

## Troubleshooting

### No Tiles Render

- Check that perception JSON files exist in `Assets/StreamingAssets/PerceptionFrames/`
- Verify `TilemapRenderer2D` is attached to Tilemap GameObject
- Ensure `GameClientFacade` is receiving perception updates

### Player Marker Not Visible

- Ensure Player GameObject has a Sprite Renderer with an assigned sprite
- Check that `PlayerController` is receiving perception updates
- Verify player marker Z-order is above tilemap

### Input Not Working

- Ensure Input System package is installed
- Check that `InputActions.inputactions` is imported
- Verify `PlayerController` has Input System event handlers assigned

### Live Mode Not Connecting

- Ensure server is running (see [CLIENT_SERVER_README.md](../../CLIENT_SERVER_README.md))
- Check server URL is correct (default: `http://localhost:5000/gamehub`)
- Verify `USE_SIGNALR` scripting define is set
- Check console for connection errors

## Known Issues

- **Dictionary Serialization:** Unity's JsonUtility does not natively support Dictionary serialization. The mock provider may need custom JSON parsing or Newtonsoft.Json.
- **SignalR on iOS:** May require additional configuration or alternative SignalR library for iOS compatibility.

## Next Steps

- See [Testing Guide](testing.md) for running EditMode and PlayMode tests
- Review [CLIENT_SERVER_README.md](../../CLIENT_SERVER_README.md) for server startup
- Explore `Assets/Scripts/` for code structure and customization

## Support

For issues or questions:
1. Check troubleshooting section above
2. Review Unity console logs
3. See project README for general development workflow

