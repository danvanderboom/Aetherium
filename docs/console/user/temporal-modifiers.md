# Temporal Modifiers System

Temporal modifiers are time-based systems that apply changes to the world during each game tick. This document explains how the temporal modifier system works and how to create custom modifiers.

## Overview

Temporal modifiers run during each world tick and apply time-based changes to regions. They are registered in a priority queue and execute in order during each tick.

## Built-in Modifiers

### Spawn Modifier

The spawn modifier handles creature spawning based on time, weather, and season conditions.

**Priority**: 50 (medium priority)

**Behavior**:
- Checks spawn probability each tick
- Uses `SpawnManager` to select appropriate creatures
- Spawns entities at valid locations within regions
- Records spawns in region deltas

**Configuration**:
```json
{
  "Simulation": {
    "EnableProceduralEvents": true
  }
}
```

### Builder Modifier

The builder modifier handles structure building by NPCs using prefab blueprints.

**Priority**: 100 (lowest priority - runs last)

**Behavior**:
- Checks build probability based on time of day
- Selects prefabs from the PrefabLibrary
- Finds suitable locations for structures
- Places structures over time (requires multiple ticks to complete)

**Status**: Currently requires World access which is not fully integrated. Building is skipped when World is not available.

## Creating Custom Modifiers

To create a custom temporal modifier, implement the `ITemporalModifier` interface:

```csharp
public class MyCustomModifier : ITemporalModifier
{
    public string Name => "my-modifier";
    public int Priority => 25; // Lower number = higher priority

    public async Task ApplyAsync(
        IMapRegionGrain region,
        RegionStateSnapshot regionSnapshot,
        TimeSpan gameTimeElapsed,
        double timeOfDay,
        int day)
    {
        // Your modifier logic here
        // Access region state via regionSnapshot
        // Apply changes via region.ApplyDeltaAsync()
    }
}
```

### Registering a Modifier

Modifiers are registered in the `TemporalModifierRegistry`:

```csharp
var registry = serviceProvider.GetService<TemporalModifierRegistry>();
registry.Register(new MyCustomModifier());
```

### Priority System

Modifiers execute in priority order (lower number = higher priority):
- **Priority 1-10**: Critical system modifiers (weather, seasons)
- **Priority 11-50**: Core gameplay modifiers (spawning, events)
- **Priority 51-100**: Secondary modifiers (building, decorative changes)

### Accessing Region State

Modifiers receive a `RegionStateSnapshot` containing:
- Region coordinates and size
- Game time (hours)
- Weather and season
- Terrain modifications
- Traversal heatmap
- Built structures

### Applying Changes

Modifiers apply changes via region deltas:

```csharp
var delta = new RegionDelta
{
    RegionId = regionSnapshot.RegionId,
    Timestamp = DateTime.UtcNow,
    Type = DeltaType.TerrainModified,
    Data = new Dictionary<string, object>
    {
        ["location"] = "10,20",
        ["terrainType"] = "grass"
    }
};

await region.ApplyDeltaAsync(delta);
```

## Delta Types

Available delta types:
- `TerrainModified`: Changes terrain at a location
- `EntityAdded`: Adds an entity to the world
- `EntityRemoved`: Removes an entity from the world
- `EntityMoved`: Moves an entity
- `TraversalRecorded`: Updates traversal heatmap
- `StructureBuilt`: Records a built structure
- `WeatherChanged`: Changes weather state

## Best Practices

### 1. Keep Modifiers Lightweight

Modifiers run every tick for every region. Keep processing minimal:
- Avoid expensive operations
- Use early returns for common cases
- Cache calculations when possible

### 2. Use Probabilistic Checks

Don't apply changes every tick - use probability:
```csharp
var probability = 0.01; // 1% chance per tick
if (_random.NextDouble() > probability)
    return;
```

### 3. Scale by Time Elapsed

Account for tick frequency in calculations:
```csharp
var adjustedProbability = baseProbability * (gameTimeElapsed.TotalSeconds / 60.0);
```

### 4. Validate Region State

Always check if region snapshot is valid:
```csharp
if (string.IsNullOrEmpty(regionSnapshot.RegionId))
    return;
```

### 5. Use Appropriate Priority

Set priority based on dependency order:
- Weather/season modifiers should run first (priority 1-10)
- Spawn modifiers run after weather (priority 50)
- Building modifiers run last (priority 100)

## Examples

### Example: Growth Modifier

A modifier that makes plants grow over time:

```csharp
public class GrowthModifier : ITemporalModifier
{
    private readonly Random _random = new();

    public string Name => "growth";
    public int Priority => 75;

    public async Task ApplyAsync(
        IMapRegionGrain region,
        RegionStateSnapshot snapshot,
        TimeSpan gameTimeElapsed,
        double timeOfDay,
        int day)
    {
        // Only grow during daytime
        if (timeOfDay < 6.0 || timeOfDay > 18.0)
            return;

        // 0.1% chance per hour to grow
        var probability = 0.001 * (gameTimeElapsed.TotalHours);
        if (_random.NextDouble() > probability)
            return;

        // Select a random location to grow
        var x = snapshot.RegionX * snapshot.RegionSize + _random.Next(0, snapshot.RegionSize);
        var y = snapshot.RegionY * snapshot.RegionSize + _random.Next(0, snapshot.RegionSize);

        var delta = new RegionDelta
        {
            RegionId = snapshot.RegionId,
            Timestamp = DateTime.UtcNow,
            Type = DeltaType.TerrainModified,
            Data = new Dictionary<string, object>
            {
                ["location"] = $"{x},{y}",
                ["terrainType"] = "tall_grass"
            }
        };

        await region.ApplyDeltaAsync(delta);
    }
}
```

### Example: Decay Modifier

A modifier that decays structures over time:

```csharp
public class DecayModifier : ITemporalModifier
{
    public string Name => "decay";
    public int Priority => 90; // Low priority - runs near the end

    public async Task ApplyAsync(
        IMapRegionGrain region,
        RegionStateSnapshot snapshot,
        TimeSpan gameTimeElapsed,
        double timeOfDay,
        int day)
    {
        // Check built structures for decay
        if (snapshot.BuiltStructures == null || snapshot.BuiltStructures.Count == 0)
            return;

        // Decay structures that are very old (over 365 days)
        foreach (var structure in snapshot.BuiltStructures)
        {
            // In a full implementation, you'd track structure age
            // For now, this is a placeholder
            var structureAge = day; // Simplified
            if (structureAge > 365)
            {
                // Decay the structure
                var delta = new RegionDelta
                {
                    RegionId = snapshot.RegionId,
                    Timestamp = DateTime.UtcNow,
                    Type = DeltaType.StructureBuilt,
                    Data = new Dictionary<string, object>
                    {
                        ["location"] = structure.Key,
                        ["action"] = "decay"
                    }
                };

                await region.ApplyDeltaAsync(delta);
            }
        }
    }
}
```

## Testing Modifiers

When testing temporal modifiers:

1. **Unit Tests**: Test modifier logic with mock regions
2. **Integration Tests**: Test modifiers with real region grains
3. **Probabilistic Tests**: Account for randomness in tests
4. **Time-Based Tests**: Test modifiers across different game times

Example test:

```csharp
[Test]
public async Task GrowthModifier_GrowsDuringDaytime()
{
    var modifier = new GrowthModifier();
    var mockRegion = new MockRegionGrain();
    var snapshot = new RegionStateSnapshot
    {
        RegionId = "region:0,0,0",
        RegionX = 0,
        RegionY = 0,
        RegionSize = 64
    };

    // Test during daytime
    await modifier.ApplyAsync(mockRegion, snapshot, TimeSpan.FromHours(1), 12.0, 0);

    // Verify delta was applied
    Assert.That(mockRegion.DeltasApplied, Is.GreaterThan(0));
}
```

## Troubleshooting

### Modifier Not Running

- Check if modifier is registered in `TemporalModifierRegistry`
- Verify priority doesn't conflict with other modifiers
- Check if region is being ticked (region must be active)

### Changes Not Applying

- Verify delta is being applied via `region.ApplyDeltaAsync()`
- Check delta type and data format
- Ensure region snapshot is valid

### Performance Issues

- Reduce probability of modifier execution
- Cache expensive calculations
- Use early returns for common cases
- Consider batching operations

## Configuration

Modifiers are configured through `SimulationOptions`:

```json
{
  "Simulation": {
    "TickHz": 1.0,
    "DayLengthMinutes": 24,
    "EnableWeather": true,
    "EnableSeasons": true,
    "EnableAgentChanges": true,
    "EnableProceduralEvents": true
  }
}
```

## Future Enhancements

Planned improvements:
- Modifier dependency system
- Conditional modifier execution
- Modifier lifecycle hooks (on activate, on deactivate)
- Performance monitoring for modifiers
- Modifier configuration per modifier type

