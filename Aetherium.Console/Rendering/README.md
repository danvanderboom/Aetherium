# Rendering System Documentation

## Overview

The Console Game uses a presentation-agnostic rendering abstraction that separates game logic from display technology. This enables the same game engine to work with multiple frontends including console, Spectre.Console, and future implementations like Unreal Engine.

## Architecture

```
┌─────────────────────────────────────────────┐
│          Game Server (Aetherium.Server)     │
│  ┌──────────────────────────────────────┐  │
│  │  Game Engine & Perception System      │  │
│  │  - World state                        │  │
│  │  - Entity management                  │  │
│  │  - Vision/Lighting calculations       │  │
│  └──────────────────────────────────────┘  │
└─────────────────────────────────────────────┘
                      │
                  SignalR/HTTP
                      │
                      ▼
┌─────────────────────────────────────────────┐
│     Game Client Library (Aetherium)        │
│  ┌──────────────────────────────────────┐  │
│  │  GameClient + DTO Models              │  │
│  │  - Network communication              │  │
│  │  - State management                   │  │
│  │  - Reusable across all clients        │  │
│  └──────────────────────────────────────┘  │
└─────────────────────────────────────────────┘
                      │
                      ▼
┌─────────────────────────────────────────────┐
│      Rendering Abstraction Layer            │
│  ┌──────────────────────────────────────┐  │
│  │  IGameRenderer Interface              │  │
│  │  - RenderFrame(GameViewState)        │  │
│  │  - Initialize() / Shutdown()         │  │
│  │  - GetInputCommand()                 │  │
│  └──────────────────────────────────────┘  │
│  ┌──────────────────────────────────────┐  │
│  │  GameViewState (Transport Object)    │  │
│  │  - PerceptionDto                     │  │
│  │  - Widgets Dictionary                │  │
│  │  - ThemeConfig                       │  │
│  └──────────────────────────────────────┘  │
│  ┌──────────────────────────────────────┐  │
│  │  Widget System                        │  │
│  │  - CompassWidget                     │  │
│  │  - InventoryWidget                   │  │
│  │  - Extensible architecture           │  │
│  └──────────────────────────────────────┘  │
└─────────────────────────────────────────────┘
                      │
          ┌───────────┴───────────┐
          ▼                       ▼
┌──────────────────┐    ┌──────────────────┐
│ SpectreConsole   │    │  Future: Unreal  │
│   Renderer       │    │  Engine Renderer │
│  - Rich console  │    │  - 3D graphics   │
│  - Panels        │    │  - UMG widgets   │
│  - Themes        │    │  - Same DTOs!    │
└──────────────────┘    └──────────────────┘
```

## Core Interfaces

### IGameRenderer

The main rendering abstraction. Any presentation technology implements this interface.

```csharp
public interface IGameRenderer
{
    void RenderFrame(GameViewState state);
    void Initialize();
    void Shutdown();
    ConsoleKeyInfo? GetInputCommand();
    Task<ConsoleKeyInfo> WaitForInputCommandAsync();
    void Clear();
}
```

**Usage:**
```csharp
var renderer = new SpectreConsoleRenderer();
renderer.Initialize();

// In game loop
var viewState = BuildGameViewState(perception);
renderer.RenderFrame(viewState);

var input = await renderer.WaitForInputCommandAsync();
```

### GameViewState

Complete UI state for a single frame. Presentation-agnostic.

```csharp
public class GameViewState
{
    public PerceptionDto? Perception { get; set; }
    public Dictionary<string, IWidget> Widgets { get; set; }
    public ThemeConfig Theme { get; set; }
    public bool IsConnected { get; set; }
    public DateTime Timestamp { get; set; }
    public string? StatusMessage { get; set; }
}
```

## Widget System

Widgets are modular UI components that can be rendered by any renderer.

### Creating a Widget

```csharp
public class MyCustomWidget : WidgetBase
{
    public MyCustomWidget() : base("my-widget-id")
    {
        ZOrder = 100; // Render priority
    }

    public override object GetRenderData()
    {
        return new MyWidgetRenderData
        {
            // Your widget's data
        };
    }
}
```

### Registering a Widget

```csharp
var widgetManager = new WidgetManager();
var widget = new MyCustomWidget();
widgetManager.RegisterWidget(widget);

// Update visibility based on game state
widgetManager.UpdateFromPerception(perception);
```

## Theme System

Themes provide visual styling configuration.

### Using Themes

```csharp
// Get built-in theme
var theme = BuiltInThemes.Zen;

// Or by name
var theme = BuiltInThemes.GetByName("cyberpunk");

// Access theme properties
var symbol = theme.GetSymbol("compass_n", "↑");
var color = theme.GetColor("health", ConsoleColor.Green);
```

### Built-in Themes

- **Zen**: Minimal, calm, meditative (default)
- **Cyberpunk**: Neon colors, sharp edges
- **Halloween**: Spooky orange and black
- **Winter**: Cool blues and whites
- **Classic**: Traditional roguelike ASCII

### Creating Custom Themes

```csharp
var customTheme = new ThemeConfig
{
    Name = "MyTheme",
    BackgroundColor = ConsoleColor.Black,
    ForegroundColor = ConsoleColor.White,
    AccentColor = ConsoleColor.Cyan,
    BorderStyle = "Rounded",
    Symbols = new Dictionary<string, string>
    {
        ["compass_n"] = "⬆",
        ["compass_e"] = "➡",
        // ... more symbols
    }
};
```

## Implementing a New Renderer

To create a renderer for a new platform (e.g., Unreal Engine, web browser):

1. **Implement IGameRenderer**:
```csharp
public class UnrealEngineRenderer : IGameRenderer
{
    public void RenderFrame(GameViewState state)
    {
        // Update UMG widgets with state.Widgets
        // Render perception data to 3D viewport
        // Apply theme styling
    }

    public void Initialize()
    {
        // Initialize UE subsystems
    }

    // ... implement other methods
}
```

2. **Map Widget Render Data to UI**:
```csharp
private void RenderWidgets(GameViewState state)
{
    foreach (var widget in state.Widgets.Values)
    {
        var renderData = widget.GetRenderData();
        
        if (renderData is CompassRenderData compassData)
        {
            UpdateCompassUMGWidget(compassData);
        }
        // ... handle other widget types
    }
}
```

3. **Use Shared DTOs**:
The `PerceptionDto`, `NavigationDataDto`, and other model classes are **completely reusable**. No changes needed.

## Reusable Components for Unreal Engine

These components require **zero modification** for Unreal:

### Network Layer
- `GameClient.cs` - SignalR connection management
- All `*Dto.cs` classes in `Aetherium.Model`

### Game State
- `PerceptionDto` - Complete game perception
- `NavigationDataDto` - Compass data
- `InventoryDto` - Player inventory
- `AffordanceDto` - Available actions

### Usage in Unreal:
```csharp
// Same code works in Unreal!
var gameClient = new GameClient("http://server:5000/gamehub");
await gameClient.ConnectAsync();

gameClient.PerceptionUpdated += (perception) =>
{
    // Update Unreal widgets
    UpdateCompassWidget(perception.NavigationData);
    UpdateInventory(perception.Inventory);
};

await gameClient.MovePlayerAsync(RelativeDirection.Forward, 1);
```

## Audio System

The audio system is also abstracted for platform independence.

### Interface

```csharp
public interface IAudioSystem
{
    void PlayBackgroundMusic(string trackName, bool loop = true);
    void StopBackgroundMusic();
    void PlaySoundEffect(string effectName);
    void SetMusicVolume(float volume);
    void SetEffectsVolume(float volume);
}
```

### Implementations

- **NAudioSystem**: Windows/cross-platform using NAudio
- **NullAudioSystem**: No-op for platforms without audio
- **Future**: UnrealAudioSystem using UE audio subsystem

## Best Practices

### 1. Keep Business Logic Server-Side
- Game rules in `Aetherium.Server`
- Client only handles presentation

### 2. Use DTOs for Communication
- Never expose server Entity classes to client
- DTOs are the contract between client/server

### 3. Make Widgets Self-Contained
- Each widget manages its own state
- Widgets don't depend on each other
- Renderer-agnostic render data

### 4. Theme Everything
- Use ThemeConfig for colors/symbols
- Don't hard-code visual styling
- Makes skins easy to implement

### 5. Test Without UI
- Mock IGameRenderer for tests
- Validate widget logic independently
- Test themes and widget visibility

## Example: Full Rendering Flow

```csharp
// 1. Server computes perception
var perception = perceptionService.ComputePerception(
    world, playerLocation, playerHeading, viewportSize);

// 2. Client receives perception via SignalR
gameClient.PerceptionUpdated += OnPerceptionUpdated;

void OnPerceptionUpdated(PerceptionDto perception)
{
    // 3. Update widgets
    widgetManager.UpdateFromPerception(perception);
    compassWidget.UpdateNavigationData(perception.NavigationData);
    
    // 4. Build view state
    var viewState = new GameViewState
    {
        Perception = perception,
        Widgets = widgetManager.GetAllWidgets(),
        Theme = currentTheme,
        IsConnected = true
    };
    
    // 5. Render (works with ANY renderer!)
    renderer.RenderFrame(viewState);
}
```

## Migration Checklist for New Platforms

- [ ] Implement IGameRenderer
- [ ] Map widget render data to native UI
- [ ] Implement IAudioSystem (optional)
- [ ] Reuse GameClient (no changes!)
- [ ] Reuse all DTOs (no changes!)
- [ ] Handle input and send commands to server
- [ ] Test with existing game server

## Files Reference

### Core Abstraction
- `Aetherium.Console/Rendering/IGameRenderer.cs`
- `Aetherium.Console/Rendering/GameViewState.cs`
- `Aetherium.Console/Rendering/WidgetManager.cs`

### Widgets
- `Aetherium.Console/Rendering/Widgets/IWidget.cs`
- `Aetherium.Console/Rendering/Widgets/CompassWidget.cs`
- `Aetherium.Console/Rendering/Widgets/InventoryWidget.cs`

### Themes
- `Aetherium.Console/Rendering/Themes/ThemeConfig.cs`
- `Aetherium.Console/Rendering/Themes/BuiltInThemes.cs`

### Implementations
- `Aetherium.Console/Rendering/SpectreConsoleRenderer.cs` (reference)
- `Aetherium.Console/Core/ClientConsoleDungeonGameNew.cs` (integration example)

### Reusable Client Code
- `Aetherium.Console/Client/GameClient.cs` ✅ Unreal-ready
- `Aetherium.Model/*.cs` ✅ All DTOs Unreal-ready

## Support

For questions or help implementing a new renderer, refer to this documentation and the `SpectreConsoleRenderer` as a reference implementation.


