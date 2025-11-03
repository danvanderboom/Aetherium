# Main Scene Setup Guide

## Overview

This guide explains how to set up the `Main.unity` scene for the Unity 2D client.

## Required GameObjects

### 1. Grid (Root)

- **Name**: "Grid"
- **Components**: 
  - `Grid` (2D Tilemap)
- **Child GameObjects**: 
  - Tilemap (see below)

### 2. Tilemap

- **Name**: "Tilemap"
- **Parent**: Grid
- **Components**:
  - `Tilemap`
  - `TilemapRenderer`
  - `TilemapRenderer2D` (custom script)
- **Setup**:
  - Attach `TilemapRenderer2D` script
  - Assign in Inspector if needed

### 3. GameManager

- **Name**: "GameManager"
- **Components**:
  - `GameManager` (custom script)
  - `GameClientFacade` (custom script)
- **Inspector Settings**:
  - `Game Client Facade`: Self reference (auto-assigned)
  - `Tilemap Renderer`: Reference to Tilemap's TilemapRenderer2D
  - `Player Controller`: Reference to Player GameObject's PlayerController
  - `HUD Text`: Reference to HUD Canvas Text element

### 4. Player

- **Name**: "Player"
- **Components**:
  - `SpriteRenderer` (with a sprite assigned)
  - `PlayerController` (custom script)
- **Position**: (0, 0, 0)
- **Setup**:
  - Create or assign a simple sprite (e.g., white square 16x16 pixels)
  - Attach `PlayerController` script
  - Set Sorting Layer above tilemap

### 5. HUD Canvas

- **Name**: "HUDCanvas"
- **Components**:
  - `Canvas` (Render Mode: Screen Space - Overlay)
  - `CanvasScaler` (UI Scale Mode: Scale With Screen Size)
- **Child GameObjects**:
  - Text (see below)

#### HUD Text

- **Name**: "HUDText"
- **Parent**: HUDCanvas
- **Components**:
  - `Text` or `TextMeshProUGUI`
- **Setup**:
  - Anchor to top-left or top-center
  - Set font size (e.g., 24)
  - Assign to `GameManager.hudText` in Inspector

## Quick Setup Steps

1. **Create Grid**:
   - Right-click Hierarchy → 2D Object → Tilemap → Rectangular
   - Unity will create Grid and Tilemap automatically

2. **Add Custom Scripts**:
   - Select Tilemap → Add Component → `TilemapRenderer2D`
   - Create empty GameObject "GameManager" → Add `GameManager` and `GameClientFacade` scripts
   - Create Sprite GameObject "Player" → Add `PlayerController` script

3. **Create HUD**:
   - Right-click Hierarchy → UI → Canvas
   - Right-click Canvas → UI → Text - TextMeshPro (or Legacy Text)
   - Position text in top-left area

4. **Wire References**:
   - Select GameManager
   - In Inspector, drag Tilemap → GameManager's "Tilemap Renderer" field
   - Drag Player → "Player Controller" field
   - Drag HUD Text → "HUD Text" field

5. **Input System Setup**:
   - Select Player GameObject
   - Add Component → `PlayerInput` (if using PlayerInput component)
   - Or ensure `PlayerController` handles Input System events directly

## Verification

After setup, when you press Play:

- ✅ Tilemap should render tiles from perception (if JSON frames exist)
- ✅ Player marker should appear at grid position (0, 0, 0)
- ✅ HUD should display "Z: 0 | Heading: North (0°) | Tiles: X"
- ✅ WASD/Arrow keys should move player marker
- ✅ Z/X should rotate player marker
- ✅ PageUp/PageDown or U/D should cycle Z-levels

## Troubleshooting

### No Tiles Render

- Check `TilemapRenderer2D` is attached to Tilemap GameObject
- Verify `GameClientFacade` is on GameManager
- Ensure perception JSON files exist in `Assets/StreamingAssets/PerceptionFrames/`

### Player Not Moving

- Verify `PlayerController` is on Player GameObject
- Check `GameManager` has Player Controller reference assigned
- Ensure Input System is configured (see main README)

### HUD Not Updating

- Verify `GameManager.hudText` is assigned in Inspector
- Check Text component is active and visible
- Ensure `GameClientFacade` is receiving perception updates

## Notes

- Scene setup is required before running PlayMode tests
- All scripts must be compiled without errors
- Ensure Input System package is installed and enabled

