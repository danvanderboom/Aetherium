# Unreal Engine Client Migration Guide

This guide explains how to create an Unreal Engine client for the Console Game, reusing the existing game client library and network protocol.

## Overview

The Console Game architecture is designed to support multiple client implementations. The **game engine** runs on the server, and clients connect via SignalR to receive perception updates and send commands. The same `GameClient` and DTO classes used by the console client can be used directly in Unreal Engine.

## What's Reusable (No Changes Needed)

### ✅ Network Layer
- **GameClient.cs**: SignalR connection management
- **Microsoft.AspNetCore.SignalR.Client**: Works in Unreal via C# scripting

### ✅ All Data Models
All classes in `Aetherium.Model` project:
- `PerceptionDto`: Complete game state as seen by player
- `NavigationDataDto`: Compass/navigation information
- `InventoryDto`: Player inventory
- `ItemDto`: Item information
- `AffordanceDto`: Available interactions
- `VisualDto`: What the player sees
- `TileTypeDto`: Tile appearance data

### ✅ Game Logic
- Server handles all game rules
- Client is purely presentational
- No need to reimplement game mechanics

## What Needs Implementation

### 🔨 Rendering (IGameRenderer)
- Create `UnrealGameRenderer` implementing `IGameRenderer`
- Map perception data to 3D scene
- Render widgets as UMG components

### 🔨 Input Handling
- Capture Unreal input events
- Map to game commands
- Send via `GameClient`

### 🔨 Audio (Optional)
- Implement `IAudioSystem` using UE audio subsystem
- Or use `NullAudioSystem` initially

## Architecture in Unreal Engine

```
┌───────────────────────────────────────────┐
│         Game Server (C#/.NET)              │
│                                            │
│  - World simulation                        │
│  - Game rules                              │
│  - Perception calculation                  │
└───────────────────────────────────────────┘
                    │
              SignalR/HTTP
                    │
                    ▼
┌───────────────────────────────────────────┐
│    Unreal Engine Client                    │
│                                            │
│  ┌─────────────────────────────────────┐  │
│  │  C# Game Client (Reused!)           │  │
│  │  - GameClient.cs                    │  │
│  │  - All DTOs                         │  │
│  │  - WidgetManager                    │  │
│  └─────────────────────────────────────┘  │
│             │                              │
│             ▼                              │
│  ┌─────────────────────────────────────┐  │
│  │  UnrealGameRenderer (New)           │  │
│  │  - Implements IGameRenderer         │  │
│  │  - Updates UMG widgets              │  │
│  │  - Renders 3D scene                 │  │
│  └─────────────────────────────────────┘  │
│             │                              │
│             ▼                              │
│  ┌─────────────────────────────────────┐  │
│  │  Unreal Engine Subsystems           │  │
│  │  - Rendering                        │  │
│  │  - UMG                              │  │
│  │  - Audio                            │  │
│  │  - Input                            │  │
│  └─────────────────────────────────────┘  │
└───────────────────────────────────────────┘
```

## Step-by-Step Implementation

### Step 1: Setup C# in Unreal Engine

Unreal Engine 5 supports C# via:
1. **UnrealCLR** (Community plugin)
2. **.NET for Unreal** (Epic's official solution - UE5.1+)

Choose one and install it in your Unreal project.

### Step 2: Import Game Client Libraries

Add these projects/DLLs to your Unreal project:
```
YourUnrealProject/
  Managed/          # C# assemblies
    Aetherium.Model.dll
    Aetherium.dll (just the client parts)
    Microsoft.AspNetCore.SignalR.Client.dll
    Spectre.Console.dll (optional, if using themes)
```

### Step 3: Create UnrealGameRenderer

```csharp
using UnrealEngine.Runtime;
using Aetherium.Rendering;

public class UnrealGameRenderer : IGameRenderer
{
    private UWorld world;
    private APlayerController playerController;
    
    public UnrealGameRenderer(UWorld world, APlayerController pc)
    {
        this.world = world;
        this.playerController = pc;
    }
    
    public void RenderFrame(GameViewState state)
    {
        if (state.Perception == null) return;
        
        // Update 3D world
        UpdateWorldRepresentation(state.Perception);
        
        // Update UMG widgets
        UpdateUIWidgets(state);
    }
    
    private void UpdateWorldRepresentation(PerceptionDto perception)
    {
        // For each visible tile in perception.Visuals:
        // 1. Get or create actor at relative position
        // 2. Set appearance based on TileTypeDto
        // 3. Apply lighting based on LightLevel
        
        foreach (var kvp in perception.Visuals)
        {
            var locationKey = kvp.Key;
            var visual = kvp.Value;
            
            // Parse location key "x,y,z"
            var coords = ParseLocationKey(locationKey);
            
            // Get or spawn actor
            var actor = GetOrSpawnTileActor(coords);
            
            // Update appearance
            UpdateActorAppearance(actor, visual);
        }
    }
    
    private void UpdateUIWidgets(GameViewState state)
    {
        // Get UMG widget references
        var compassWidget = GetCompassWidget();
        var inventoryWidget = GetInventoryWidget();
        
        // Update compass
        if (state.Widgets.TryGetValue("compass", out var compass))
        {
            if (compass.IsVisible)
            {
                var data = (CompassRenderData)compass.GetRenderData();
                compassWidget.UpdateHeading(data.DirectionSymbol, data.DirectionName);
                compassWidget.SetVisibility(ESlateVisibility.Visible);
            }
            else
            {
                compassWidget.SetVisibility(ESlateVisibility.Hidden);
            }
        }
        
        // Update inventory
        if (state.Widgets.TryGetValue("inventory", out var inv))
        {
            var data = (InventoryRenderData)inv.GetRenderData();
            inventoryWidget.UpdateInventory(data.Items, data.Count, data.Capacity);
        }
    }
    
    public void Initialize()
    {
        // Initialize UE subsystems
        // Create widget instances
    }
    
    public void Shutdown()
    {
        // Cleanup
    }
    
    public ConsoleKeyInfo? GetInputCommand()
    {
        // Unreal uses its own input system
        // This method can return null
        return null;
    }
    
    public async Task<ConsoleKeyInfo> WaitForInputCommandAsync()
    {
        // Unreal handles input via delegates/events
        // This can be a no-op
        await Task.Delay(16); // One frame
        return default;
    }
    
    public void Clear()
    {
        // Clear 3D scene if needed
    }
}
```

### Step 4: Setup Game Client Connection

```csharp
public class UnrealGameController : Actor
{
    private GameClient gameClient;
    private UnrealGameRenderer renderer;
    private WidgetManager widgetManager;
    private ThemeConfig theme;
    
    protected override void BeginPlay()
    {
        base.BeginPlay();
        
        // Initialize components
        theme = BuiltInThemes.Zen;
        widgetManager = new WidgetManager();
        renderer = new UnrealGameRenderer(GetWorld(), GetPlayerController());
        
        // Create widgets
        var compass = new CompassWidget(theme);
        widgetManager.RegisterWidget(compass);
        
        // Connect to server
        gameClient = new GameClient("http://yourserver:5000/gamehub");
        gameClient.PerceptionUpdated += OnPerceptionUpdated;
        gameClient.Connected += OnConnected;
        
        _ = ConnectAsync();
    }
    
    private async Task ConnectAsync()
    {
        await gameClient.ConnectAsync();
    }
    
    private void OnPerceptionUpdated(PerceptionDto perception)
    {
        // Update widgets
        widgetManager.UpdateFromPerception(perception);
        
        // Build view state
        var viewState = new GameViewState
        {
            Perception = perception,
            Widgets = widgetManager.GetAllWidgets(),
            Theme = theme,
            IsConnected = true
        };
        
        // Render
        renderer.RenderFrame(viewState);
    }
    
    private void OnConnected()
    {
        UE_LOG("Connected to game server!");
    }
}
```

### Step 5: Handle Input

```csharp
// In your Player Controller or Character class
public class UnrealGameCharacter : Character
{
    private GameClient gameClient;
    
    protected override void SetupPlayerInputComponent(UInputComponent input)
    {
        base.SetupPlayerInputComponent(input);
        
        // Bind movement
        input.BindAxis("MoveForward", MoveForward);
        input.BindAxis("MoveRight", MoveRight);
        
        // Bind actions
        input.BindAction("Pickup", EInputEvent.IE_Pressed, HandlePickup);
        input.BindAction("Drop", EInputEvent.IE_Pressed, HandleDrop);
        input.BindAction("Interact", EInputEvent.IE_Pressed, HandleInteract);
    }
    
    private async void MoveForward(float value)
    {
        if (value != 0)
        {
            var direction = value > 0 
                ? RelativeDirection.Forward 
                : RelativeDirection.Backward;
            await gameClient.MovePlayerAsync(direction, 1);
        }
    }
    
    private async void HandlePickup()
    {
        // Get closest item from perception
        var closestItem = FindClosestItem();
        if (closestItem != null)
        {
            var result = await gameClient.PickupAsync(closestItem.Id);
            if (result.Success)
            {
                PlayPickupSound();
            }
        }
    }
}
```

### Step 6: Create UMG Widgets

Create these UMG widgets in Unreal Editor:

#### WBP_Compass
- Text block for direction name
- Image for direction symbol
- Toggle visibility based on compass availability

#### WBP_Inventory
- List view for items
- Text block for capacity (X/Y)
- Item icons

#### WBP_HUD (Main HUD)
- Contains WBP_Compass
- Contains WBP_Inventory
- Status messages
- Any other UI elements

### Step 7: Coordinate System Mapping

The server uses relative coordinates (player at 0,0,0):

```csharp
private Vector3 ConvertToUnrealCoordinates(int x, int y, int z)
{
    // Map perception coordinates to Unreal world space
    // Server: X=forward/back, Y=left/right, Z=up/down
    // Unreal: X=forward/back, Y=left/right, Z=up/down (same!)
    
    float tileSize = 100.0f; // Unreal units per tile
    
    return new Vector3(
        x * tileSize,
        y * tileSize,
        z * tileSize
    );
}
```

## Data Flow Example

```
1. User presses W key
   └─> Unreal Input System detects

2. Call gameClient.MovePlayerAsync(Forward, 1)
   └─> SignalR sends command to server

3. Server processes movement
   └─> Updates world state
   └─> Computes new perception
   └─> Sends PerceptionDto back via SignalR

4. gameClient.PerceptionUpdated event fires
   └─> OnPerceptionUpdated() called

5. Update widgets from perception
   └─> widgetManager.UpdateFromPerception(perception)
   └─> compassWidget.UpdateNavigationData(...)

6. Build GameViewState
   └─> Includes perception + widgets

7. Render frame
   └─> renderer.RenderFrame(viewState)
   └─> UpdateWorldRepresentation() updates 3D actors
   └─> UpdateUIWidgets() updates UMG
```

## Testing Strategy

### 1. Start Small
- Get connection working first
- Display raw perception data as debug text
- Validate data is arriving correctly

### 2. Implement Core Rendering
- Spawn simple cubes for each visible tile
- Color them based on tile type
- Verify player is always at center

### 3. Add Lighting
- Apply light levels from perception
- Use Unreal's light system or material parameters

### 4. Polish Visuals
- Replace cubes with proper meshes
- Add animations
- Implement proper materials

### 5. Add UI
- Compass widget
- Inventory widget
- Status messages

## Common Issues

### Issue: SignalR Connection Fails
**Solution**: Ensure server is running and accessible. Check firewall rules. Verify URL is correct.

### Issue: Perception Data Not Updating
**Solution**: Verify event subscription: `gameClient.PerceptionUpdated += OnPerceptionUpdated`

### Issue: Coordinates Don't Match
**Solution**: Remember perception uses relative coordinates (player at 0,0,0). Don't expect absolute positions.

### Issue: Widgets Not Showing
**Solution**: Check `widget.IsVisible` property. Ensure `widgetManager.UpdateFromPerception()` is called.

## Performance Considerations

### Network
- Perception updates are sent ~20 times per second
- Each update contains only visible tiles (not entire world)
- Delta encoding not yet implemented (future optimization)

### Rendering
- Only render visible tiles from perception
- Pool actors/meshes for performance
- Use instanced static meshes for many tiles

### Widget Updates
- Widgets update only when perception changes
- Avoid rebuilding entire UI each frame
- Cache widget references

## Debugging

### Enable Verbose Logging
```csharp
gameClient.PerceptionUpdated += (perception) =>
{
    UE_LOG($"Received perception: {perception.Visuals.Count} visible tiles");
    UE_LOG($"Player heading: {perception.PlayerHeading}");
    if (perception.NavigationData != null)
    {
        UE_LOG($"Has compass: {perception.NavigationData.HasCompass}");
    }
};
```

### Visualize Perception Data
- Draw debug spheres at tile locations
- Display tile coordinates as text
- Show light levels as colors

## Advanced Topics

### Custom Widgets
```csharp
public class HealthBarWidget : WidgetBase
{
    public HealthBarWidget() : base("health-bar") { }
    
    public override object GetRenderData()
    {
        return new HealthBarRenderData
        {
            CurrentHealth = /* get from perception */,
            MaxHealth = /* ... */
        };
    }
}
```

### Theme Support
```csharp
// Apply theme to Unreal materials
var theme = BuiltInThemes.Cyberpunk;
var accentColor = ConvertToUnrealColor(theme.AccentColor);
materialInstance.SetVectorParameterValue("AccentColor", accentColor);
```

### Audio Integration
```csharp
public class UnrealAudioSystem : IAudioSystem
{
    public void PlaySoundEffect(string effectName)
    {
        var soundCue = LoadSound(effectName);
        UGameplayStatics.PlaySound2D(GetWorld(), soundCue);
    }
    
    // ... implement other methods
}
```

## Next Steps

1. ✅ Setup Unreal project with C# support
2. ✅ Import Aetherium client libraries
3. ✅ Create UnrealGameRenderer
4. ✅ Connect to server and receive perception
5. ✅ Render basic 3D representation
6. ✅ Create UMG widgets
7. ✅ Handle player input
8. ✅ Add audio and polish

## Resources

- **Console Game Rendering Docs**: `Aetherium.Console/Rendering/README.md`
- **Example Renderer**: `Aetherium.Console/Rendering/SpectreConsoleRenderer.cs`
- **Example Integration**: `Aetherium.Console/Core/ClientConsoleDungeonGameNew.cs`
- **Server API**: Check `Aetherium.Server/GameHub.cs` for available commands

## Questions?

The architecture is designed to make this migration straightforward. The key insight: **the game client library is platform-agnostic**. You're just swapping out the renderer!

Happy migrating! 🎮


