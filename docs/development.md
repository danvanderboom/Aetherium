# Developer Guide

Developer documentation for the Aetherium Console Game project.

## Table of Contents

- [Development Setup](#development-setup)
- [Testing](#testing)
- [Recent Changes](#recent-changes)
- [Development Workflow](#development-workflow)
- [Architecture Overview](#architecture-overview)

## Development Setup

### Prerequisites

- .NET 9.0 SDK or later
- PowerShell (for Windows development)
- Git

### Project Structure

```
Aetherium.sln
├── Aetherium.Model/          # Shared DTOs and data structures
├── Aetherium.Server/         # Server-side game logic (Orleans grains)
├── Aetherium.Console/        # Console client application
├── Aetherium.Test/           # Test suite
├── Aetherium.Dashboard/      # Web dashboard for game management
├── Aetherctl/                # Unified CLI tool (see a.plan.md)
├── AgentCLI/                 # Legacy agent CLI (deprecated)
└── WorldGenCLI/              # Legacy world generation CLI (deprecated)
```

### Building

```powershell
# Build entire solution
dotnet build Aetherium.sln

# Build specific project
dotnet build Aetherium.Server/Aetherium.Server.csproj

# Build with Release configuration
dotnet build Aetherium.sln -c Release
```

## Testing

### Running Tests

```powershell
# Run all tests
dotnet test Aetherium.sln

# Run tests in Release configuration
dotnet test Aetherium.sln -c Release

# Run specific test project
dotnet test Aetherium.Test/Aetherium.Test.csproj

# Run with verbose output
dotnet test Aetherium.sln --verbosity normal

# Run specific test class or method (using NUnit filter)
dotnet test Aetherium.Test --filter "FullyQualifiedName~GameMapGrain_LoadMap_RestoresRegions"
```

### Test Status

Current test status: **597 passed, 0 failed, 2 skipped** (as of latest run)

### Testing Best Practices

#### 1. Use Fixed Seeds for World Generation Tests

World generation tests can be flaky due to random seed generation. Always use fixed seeds for reproducible tests:

```csharp
// ✅ Good - Fixed seed for reproducibility
var parameters = new Dictionary<string, object> { { "seed", 12345 } };
await mapGrain.InitializeAsync(worldId, "Test Map", size, "outdoor", parameters);

// ❌ Bad - Random seed leads to flaky tests
await mapGrain.InitializeAsync(worldId, "Test Map", size, "outdoor", new Dictionary<string, object>());
```

#### 2. Test Isolation

- Each test should use unique grain keys to avoid conflicts
- Use `TestCluster` with in-memory storage for Orleans grain tests
- Clean up test data in `TearDown` methods when needed

#### 3. Test Naming Convention

Use descriptive test names that explain what is being tested:

```csharp
[Test]
public async Task GameMapGrain_LoadMap_RestoresRegions()
{
    // Test implementation
}
```

### Seed Parameter Support

The `GameMapGrain.InitializeAsync()` method now supports a `seed` parameter for deterministic world generation:

```csharp
public async Task InitializeAsync(
    string worldId, 
    string mapName, 
    WorldSize size, 
    string generatorType, 
    Dictionary<string, object> parameters)
{
    // If seed is provided in parameters, use it; otherwise generate random seed
    var seed = parameters.TryGetValue("seed", out var seedObj) && seedObj is int seedInt
        ? seedInt
        : (int)(DateTime.UtcNow.Ticks & 0x7FFFFFFF);
    // ...
}
```

This allows tests to use fixed seeds for reproducible results:

```csharp
var parameters = new Dictionary<string, object> { { "seed", 12345 } };
await mapGrain.InitializeAsync(worldId, "Test Map", size, "outdoor", parameters);
```

## Recent Changes

### Fixed Test Flakiness

Tests in `GameMapGrainRegionTests.cs` were failing intermittently due to random seed generation. Fixed by:

1. Added seed parameter support to `GameMapGrain.InitializeAsync()`
2. Updated all tests to use fixed seeds instead of random ones
3. Each test now uses a different seed value for isolation

**Files changed:**
- `Aetherium.Server/MultiWorld/GameMapGrain.cs` - Added seed parameter support
- `Aetherium.Test/MultiWorld/GameMapGrainRegionTests.cs` - Updated tests to use fixed seeds
- `Aetherium.Server/Middleware/ApiKeyMiddleware.cs` - Fixed missing using directives

### API Key Middleware

Added API key authentication middleware for control-plane endpoints. Requires `Dashboard:ApiKey` in configuration for production environments.

**Files:**
- `Aetherium.Server/Middleware/ApiKeyMiddleware.cs` - Middleware implementation
- `Aetherium.Server/Controllers/ManagementController.cs` - Management API endpoints
- `Aetherium.Dashboard/` - Web dashboard for game management

### Unified CLI (a.plan.md)

Planned unified CLI tool (`aetherctl`) to consolidate operator tooling. See `a.plan.md` in the project root for the complete specification.

**Status:** Planning phase - see `a.plan.md` for details

## Development Workflow

### Making Changes

1. **Plan Major Changes**: Use [OpenSpec workflow](../openspec/AGENTS.md) for architectural changes
2. **Create Feature Branch**: `git checkout -b feature/your-feature-name`
3. **Write Tests**: Add tests for new functionality
4. **Implement**: Write code following existing patterns
5. **Run Tests**: Ensure all tests pass before committing
6. **Commit**: Use descriptive commit messages
7. **Push**: Push to remote and create PR

### Code Style

- Follow existing code patterns and conventions
- Use C# nullable reference types appropriately
- Prefer async/await over blocking calls
- Document public APIs with XML comments

### Local Testing

#### Quick Game Test

```powershell
# Start server and client in separate windows
.\start-game-test.ps1 -TimeoutSeconds 20

# Clean up processes
.\stop-game.ps1
```

#### Monitor Game State

```powershell
# Lightweight monitor (no unicode borders)
.\scripts\monitor-lite.ps1

# Full monitoring with more features
.\scripts\monitor-game.ps1
```

#### UI Self-Tests

```powershell
# Run UI self-tests
.\scripts\run-client-ui-tests.ps1 -TimeoutSeconds 40
```

### Debugging

#### Orleans Grain Debugging

When debugging Orleans grains in tests:

1. Use `TestCluster` for isolated grain testing
2. Ensure in-memory storage is configured: `siloBuilder.AddMemoryGrainStorage("mapStore")`
3. Access grains via cluster's grain factory: `_cluster.GrainFactory.GetGrain<IGameMapGrain>(mapId)`

#### Server Debugging

1. Set breakpoints in `Aetherium.Server` project
2. Run server with debugger attached: `dotnet run` (from VS/Rider) or `dotnet build && dotnet exec`
3. Connect client separately

#### Client Debugging

1. Set breakpoints in `Aetherium.Console` project
2. Ensure server is running first
3. Run client with debugger attached

## Architecture Overview

### Client-Server Architecture

- **Server**: Hosts all game logic (world state, entities, AI, perception)
- **Client**: Receives only perception data (what player can see)
- **Communication**: SignalR for real-time bidirectional communication
- **Protocol**: Server-authoritative; client sends inputs, receives updates

See [CLIENT_SERVER_README.md](../CLIENT_SERVER_README.md) for detailed architecture information.

### Orleans Grains

The server uses Orleans for distributed state management:

- **GameMapGrain**: Manages individual game maps/worlds
- **MapRegionGrain**: Manages regions within maps for scalability
- **GameSession**: Per-client game state
- **GameManagementGrain**: Game-wide management and coordination

### World Generation

World generation uses a pass-based pipeline:

1. **Layout Pass**: Generate basic map structure
2. **Theming Pass**: Apply visual themes
3. **Population Pass**: Add entities and interactives
4. **Validation Pass**: Ensure generated world meets requirements

For details, see `Aetherium.Server/WorldGen/` directory.

## Common Issues and Solutions

### Tests Failing Intermittently

**Problem**: Tests fail randomly with "Generation failed" errors

**Solution**: Use fixed seeds in test parameters:
```csharp
var parameters = new Dictionary<string, object> { { "seed", 12345 } };
await grain.InitializeAsync(..., parameters);
```

### Orleans Grain Not Found

**Problem**: `GrainNotFoundException` in tests

**Solution**: Ensure grains are properly activated before use:
```csharp
await mapGrain.InitializeAsync(...); // Activate grain first
var metadata = await mapGrain.GetMetadataAsync(); // Then use it
```

### Port Already in Use

**Problem**: Server fails to start with "port already in use" error

**Solution**: 
```powershell
# Kill existing processes
.\stop-game.ps1 -All

# Or manually find and kill process on port 5000
netstat -ano | findstr :5000
```

### Build Errors

**Problem**: Missing using directives or namespace errors

**Solution**: Ensure all required namespaces are imported:
```csharp
using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
```

## Additional Resources

- [OpenSpec Workflow](../openspec/AGENTS.md) - For planning and proposing changes
- [Agent System Guide](agents/README.md) - AI agent system documentation
- [CLIENT_SERVER_README.md](../CLIENT_SERVER_README.md) - Client-server architecture details
- [a.plan.md](../a.plan.md) - Unified CLI tool specification
- [TEST_STATUS.md](../TEST_STATUS.md) - Current test status

## Contributing

When contributing code:

1. Follow the [OpenSpec workflow](../openspec/AGENTS.md) for major changes
2. Write tests for new functionality
3. Ensure all tests pass before submitting PR
4. Update documentation as needed
5. Use descriptive commit messages

---

**Last Updated**: Based on latest changes (seed parameter support, test fixes)  
**Test Status**: 597 passed, 0 failed, 2 skipped

