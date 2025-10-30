# Project Context

## Purpose
ConsoleGame is a real-time multiplayer dungeon crawler game with a client-server architecture. The game features a console-based UI with ASCII graphics, real-time lighting, field-of-view calculations, and interactive gameplay elements. The project includes a monitoring system for debugging and automated testing.

## Tech Stack
- **Backend**: .NET 9.0, ASP.NET Core, SignalR
- **Client**: .NET 9.0, Console Application
- **Testing**: xUnit, NUnit
- **Architecture**: Client-Server with WebSocket communication
- **Monitoring**: Built-in WebSocket server, PowerShell clients

## Project Conventions

### Code Style
- C# with nullable reference types enabled
- PascalCase for public members, camelCase for private
- Use meaningful variable names and avoid abbreviations
- Prefer composition over inheritance
- Use `var` for local variables when type is obvious

### Namespace Conventions
**CRITICAL**: The project has multiple assemblies with specific namespace patterns:

- **ConsoleGameServer** (assembly) uses namespace `ConsoleGameServer.*` for server-specific code:
  - `ConsoleGameServer.Agents` - Agent system
  - `ConsoleGameServer.Management` - Orleans management grains
  - `ConsoleGameServer.MultiWorld` - Multi-world hosting
  - `ConsoleGameServer.Narrative` - Narrative system
  - BUT inherits `ConsoleGame.*` namespaces from shared assemblies

- **ConsoleGameModel** (shared assembly) uses namespace `ConsoleGame.*`:
  - `ConsoleGame.Core` - Core game logic (World, Entity, etc.)
  - `ConsoleGame.WorldGen` - Procedural generation (generators, algorithms, prefabs)
  - `ConsoleGame.WorldGen.Generators` - Specific generator implementations
  - `ConsoleGame.WorldGen.Algorithms` - Reusable algorithms (Perlin, FloodFill, etc.)
  - `ConsoleGame.WorldGen.Prefabs` - Prefab system

- **ConsoleGameClient** (assembly) uses namespace `ConsoleGame.*` for client code

**Common Mistake**: Using `ConsoleGameServer.WorldGen` when it should be `ConsoleGame.WorldGen`
- WorldGen is in the **shared model** (ConsoleGameModel project)
- Only server-specific logic goes in `ConsoleGameServer.*` namespaces

### Architecture Patterns
- **ECS (Entity-Component-System)**: Game entities are composed of components
- **Client-Server**: Server maintains authoritative game state, client renders
- **SignalR**: Real-time communication between client and server
- **Orleans Grains**: Actor-based concurrency for game state management
- **Singleton**: Used sparingly for services like monitoring
- **Observer**: Event-driven updates for perception changes

### Orleans Configuration Patterns
**Storage Configuration**:
- Development: Use `AddMemoryGrainStorage()` for in-memory persistence
- Production: Use Azure Table Storage with environment variable `ORLEANS_STORAGE=azure`
- Storage names: `narrativeStore`, `worldStore`, `mapStore` (consistent naming)

**Service Registration for Grains**:
```csharp
// Host services need to be accessible to Orleans grains via co-hosting
siloBuilder.Services.AddSingleton<ServiceType>(sp =>
{
    var host = sp.GetRequiredService<IHost>();
    return host.Services.GetRequiredService<ServiceType>();
});
```

**Common Services to Register**:
- `IHubContext<GameHub>` - For grains to send SignalR messages
- `GameSessionManager` - For accessing active game sessions
- `IGrainFactory` - Already available, but may need explicit registration
- Custom services like `MapGeneratorRegistry`, `PrefabLibrary`, `PromptRegistry`

**Environment Variables**:
- `DISABLE_ORLEANS=1` - Disable Orleans for testing
- `ORLEANS_STORAGE=memory|azure` - Storage backend selection
- `AZURE_STORAGE_CONNECTION_STRING` - Required for Azure storage
- `PREFAB_STORAGE=file` - Force file-based prefab storage
- `PREFAB_PATH=./Data/Prefabs` - Prefab directory path

### Testing Strategy
- Unit tests for core game logic (geometry, FOV, lighting)
- Integration tests for client-server communication
- Manual testing for monitoring system (PowerShell scripts)
- All tests must pass before commits
- Use xUnit for new tests, maintain existing NUnit tests

### Git Workflow
- Main branch: `master`
- Feature branches for larger changes
- Commit messages: descriptive, include scope
- All tests must pass before push
- Use conventional commit format when possible

## Domain Context

### Game Mechanics
- **Player Movement**: Arrow keys for movement, Z/X for rotation
- **Field of View**: Ray-casting based visibility calculations
- **Lighting System**: Dynamic light propagation with dimming
- **Inventory**: Item pickup/drop with capacity limits
- **Interactions**: Doors, switches, keys, and other interactive objects

### Coordinate System
- **World Coordinates**: Absolute positions in 3D space (X, Y, Z)
- **Relative Coordinates**: Player-relative positions for client rendering
- **Screen Coordinates**: Console display positions

### Key Components
- **Entities**: Game objects (Player, Monster, Door, Item, etc.)
- **Components**: Data containers (Health, Tile, LightSource, etc.)
- **Systems**: Logic processors (VisionSystem, LightingSystem, etc.)
- **Views**: Rendering components (ConsoleMapView, ClientConsoleMapView)

## Monitoring System

### Overview
The game includes a built-in monitoring system that allows real-time observation of game state for debugging, testing, and AI development.

### Quick Start
1. Start server + client for UI validation:
   - `./start-game-test.ps1 -TimeoutSeconds 20` (opens two windows and auto-cleans)
   - If interrupted, run `./stop-game.ps1` or `./stop-game.ps1 -All`
2. Connect a monitor:
   - Full: `cd scripts; .\monitor-game.ps1 -DisplayAsciiMap`
   - Minimal: `cd scripts; .\monitor-lite.ps1` (no Unicode borders)
3. WebSocket endpoint: `ws://localhost:5001/monitor`

### Features
- **Real-time streaming**: WebSocket-based push updates
- **Dual data format**: Raw perception JSON + rendered ASCII maps
- **PowerShell client**: Easy connection and display
- **File logging**: Optional human-readable logs
- **Zero dependencies**: Uses built-in .NET libraries

### Usage Examples
```powershell
# Basic monitoring (stats only)
.\scripts\monitor-game.ps1

# With ASCII map display
.\scripts\monitor-game.ps1 -DisplayAsciiMap

# Save frames for analysis
.\scripts\monitor-game.ps1 -SaveToFile -OutputPath "./test-run"

# Full verbose mode
.\scripts\monitor-game.ps1 -DisplayAsciiMap -DisplayJson -SaveToFile -Verbose
```

### Developer Notes (Console UI + Spectre)
- The map is rendered by `ClientConsoleMapView`; the Spectre renderer draws widgets only.
- Avoid calling `AnsiConsole.Clear()` (or otherwise clearing the console) after the map draws, or the map will disappear.
- PowerShell command chaining: use `;` instead of `&&` in this environment.

### Configuration
Edit `ConsoleGame/Program.cs` to modify:
- Port (default: 5001)
- File logging enabled/disabled
- Output directory for logs

## OpenSpec Usage

### What is OpenSpec?
OpenSpec is a spec-driven development system used in this project to manage requirements, track changes, and ensure consistent development practices.

### Quick Commands
```bash
# List active changes and specs
openspec list
openspec list --specs

# View details
openspec show [change-id]
openspec show [spec-id] --type spec

# Validate changes
openspec validate [change-id] --strict

# Archive completed changes
openspec archive [change-id] --yes
```

### When to Use OpenSpec
- **Create proposals** for new features, breaking changes, or architecture changes
- **Skip proposals** for bug fixes, typos, or dependency updates
- **Always validate** before implementation: `openspec validate [change-id] --strict`

### Key Files
- `openspec/specs/` - Current requirements (what IS built)
- `openspec/changes/` - Proposed changes (what SHOULD change)
- `openspec/project.md` - This file (project context)

### Workflow
1. **Explore**: `openspec list` to see current state
2. **Create**: New change proposal with `proposal.md`, `tasks.md`, and spec deltas
3. **Validate**: `openspec validate [change-id] --strict`
4. **Implement**: Follow tasks.md checklist
5. **Archive**: `openspec archive [change-id] --yes` after completion

## Important Constraints
- **Console UI**: Must work in standard Windows console
- **Real-time**: Game must maintain 60+ FPS for smooth gameplay
- **Memory**: Efficient rendering to avoid console flicker
- **Compatibility**: .NET 9.0+ required
- **Platform**: Primarily Windows (uses Console.Beep, Console.CapsLock)

## External Dependencies
- **SignalR**: Real-time client-server communication
- **Orleans**: Actor-based framework for distributed systems
- **Newtonsoft.Json**: JSON serialization for DTOs
- **System.Net.WebSockets**: Built-in WebSocket support for monitoring
- **System.Net.HttpListener**: Built-in HTTP server for monitoring
- **Azure Storage** (optional): Orleans grain persistence in production

## Development Notes
- **Monitoring**: Always test with PowerShell client after changes
- **Performance**: Monitor frame rates during development
- **Testing**: Run `dotnet test` before commits
- **Documentation**: Update README files when adding features
- **Specs**: Use OpenSpec for significant changes

## Common Build Errors and Solutions

### "Type or namespace does not exist"
1. **Check namespace conventions** (see above)
   - Is it `ConsoleGame.WorldGen` or `ConsoleGameServer.WorldGen`?
   - WorldGen is in ConsoleGameModel, so use `ConsoleGame.WorldGen`
2. **Verify project references** in `.csproj` files
3. **Check assembly name** vs namespace - they can differ

### "Does not contain a definition for 'AddAzureTableGrainStorage'"
- Missing NuGet package: `Microsoft.Orleans.Persistence.AzureStorage`
- Or using wrong Orleans storage extension method for your Orleans version

### Orleans Grain Storage Setup
- Always configure storage in `Program.cs` **before** grains are activated
- Use named storage: `AddMemoryGrainStorage("storeName")`
- Match storage names in grain `[StorageProvider(ProviderName = "storeName")]` attributes

### Missing Dependencies in Grains
- Grains can't use constructor injection for host services directly
- Use co-hosting pattern (see "Service Registration for Grains" above)
- Register services in both host DI and Orleans silo DI
