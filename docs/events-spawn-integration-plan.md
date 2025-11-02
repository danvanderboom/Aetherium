# SpawnControllerGrain Integration Plan

## Overview

This document outlines the plan for integrating SpawnControllerGrain with event handlers to enable entity spawning during procedural events (merchant caravans, monster invasions, etc.).

## Current State

### Implemented
- ✅ `ISpawnControllerGrain` and `SpawnControllerGrain` - Grain for managing entity spawns
- ✅ `IEventInstanceGrain` and `EventInstanceGrain` - Grain for managing event lifecycle
- ✅ `IEventSchedulerGrain` and `EventSchedulerGrain` - Grain for scheduling events
- ✅ AOI (Area of Interest) tracking in EventInstanceGrain
- ✅ Basic spawn infrastructure in SpawnControllerGrain

### Pending
- ⏳ Handler integration with SpawnControllerGrain
- ⏳ Map/Region ID resolution for spawn locations
- ⏳ Entity despawn cleanup on event completion
- ⏳ Spawn rate calculation integration with SpawnManager
- ⏳ Narrative event emission via AOI broadcasts

## Integration Steps

### Step 1: Enhance Event Handlers with SpawnControllerGrain

**Files to Modify:**
- `Aetherium.Server/Events/ProceduralEvents.cs`
  - `MerchantCaravanHandler`
  - `MonsterInvasionHandler`

**Changes:**
1. Add `IGrainFactory` dependency to handlers (or inject via ServiceProvider)
2. Resolve `EventInstanceGrain` from event data to get event instance ID
3. Get or create `SpawnControllerGrain` for the event instance
4. Use `SpawnControllerGrain.SpawnEntitiesAsync()` to spawn entities

**Example Implementation:**
```csharp
public class MonsterInvasionHandler : IEventHandler
{
    private readonly IGrainFactory _grainFactory;
    private readonly SpawnManager? _spawnManager;

    public MonsterInvasionHandler(IGrainFactory grainFactory, SpawnManager? spawnManager = null)
    {
        _grainFactory = grainFactory;
        _spawnManager = spawnManager;
    }

    public async Task HandleEventAsync(ScheduledEvent scheduledEvent, double currentGameTime, int day)
    {
        // Get event instance ID from event data (set by EventSchedulerGrain)
        if (!scheduledEvent.EventData.TryGetValue("eventInstanceId", out var instanceIdObj))
            return;

        var eventInstanceId = instanceIdObj?.ToString();
        if (string.IsNullOrEmpty(eventInstanceId))
            return;

        // Get event instance to retrieve map ID and location
        var eventInstanceGrain = _grainFactory.GetGrain<IEventInstanceGrain>(eventInstanceId);
        var eventInfo = await eventInstanceGrain.GetInfoAsync();
        if (eventInfo == null)
            return;

        // Get spawn configuration
        var spawnType = scheduledEvent.EventData.TryGetValue("spawnType", out var typeObj)
            ? typeObj?.ToString() ?? "Monster"
            : "Monster";

        var spawnCount = scheduledEvent.EventData.TryGetValue("spawnCount", out var countObj)
            ? Convert.ToInt32(countObj)
            : 5;

        // Calculate spawn locations (spread around event location)
        var x = eventInfo.X ?? 0;
        var y = eventInfo.Y ?? 0;
        var z = eventInfo.Z ?? 0;
        var mapId = eventInfo.MapId ?? string.Empty;

        if (string.IsNullOrEmpty(mapId))
            return;

        // Get or create spawn controller for this event instance
        var spawnController = _grainFactory.GetGrain<ISpawnControllerGrain>(eventInstanceId);
        
        // Prepare spawn config with SpawnManager integration
        var spawnConfig = new Dictionary<string, object>
        {
            { "spawnType", spawnType },
            { "spawnCount", spawnCount },
            { "eventType", scheduledEvent.EventType }
        };

        // Add spawn rate from SpawnManager if available
        if (_spawnManager != null && !string.IsNullOrEmpty(eventInfo.RegionId))
        {
            var timeOfDay = currentGameTime % 24.0;
            var spawnRate = _spawnManager.GetSpawnRate(spawnType, eventInfo.RegionId, timeOfDay, day);
            spawnConfig["spawnRate"] = spawnRate;
        }

        // Spawn entities
        var spawnResult = await spawnController.SpawnEntitiesAsync(
            scheduledEvent.EventType,
            spawnConfig,
            mapId,
            x, y, z,
            spawnCount
        );

        if (spawnResult.Success && spawnResult.EntityIds.Count > 0)
        {
            // Broadcast spawn event to players in AOI
            var broadcastData = new Dictionary<string, object>
            {
                { "eventType", "monster_invasion_spawned" },
                { "entityCount", spawnResult.EntityIds.Count },
                { "spawnType", spawnType },
                { "location", new { x, y, z } }
            };

            await eventInstanceGrain.BroadcastToAreaAsync("spawn", broadcastData);
        }
    }
}
```

### Step 2: Store Event Instance ID in ScheduledEvent

**Files to Modify:**
- `Aetherium.Server/Events/EventSchedulerGrain.cs`

**Changes:**
1. When creating event instance in `TriggerEventAsync`, store `EventInstanceId` in `ScheduledEvent.EventData`
2. This allows handlers to resolve the event instance grain

**Example:**
```csharp
// In TriggerEventAsync method, after creating event instance:
scheduledEvent.EventData["eventInstanceId"] = eventInstanceId.Value;
```

### Step 3: Resolve Map ID from Region ID

**Files to Modify:**
- `Aetherium.Server/Events/EventInstanceGrain.cs`
- `Aetherium.Server/MultiWorld/IMapRegionGrain.cs` (may need new method)

**Changes:**
1. Add `GetMapIdAsync()` method to `IMapRegionGrain` if not exists
2. In `EventInstanceGrain.InitializeAsync`, resolve map ID from region ID if region ID is provided
3. Store resolved map ID in state

**Example:**
```csharp
// In EventInstanceGrain.InitializeAsync:
if (!string.IsNullOrEmpty(config.RegionId) && string.IsNullOrEmpty(config.MapId))
{
    var regionGrain = _grainFactory.GetGrain<IMapRegionGrain>(config.RegionId);
    var snapshot = await regionGrain.GetSnapshotAsync();
    _state.State.MapId = snapshot.MapId;
}
```

### Step 4: Entity Despawn on Event Completion

**Files to Modify:**
- `Aetherium.Server/Events/EventInstanceGrain.cs`
- `Aetherium.Server/Events/SpawnControllerGrain.cs`

**Changes:**
1. In `EventInstanceGrain.CompleteAsync()`, get spawned entities from SpawnControllerGrain
2. Call `DespawnEntitiesAsync()` to clean up spawned entities
3. Ensure entities are removed from map when event completes

**Example:**
```csharp
public async Task CompleteAsync()
{
    // ... existing completion logic ...

    // Despawn entities created for this event
    var spawnController = _grainFactory.GetGrain<ISpawnControllerGrain>(_state.State.EventInstanceId.Value);
    var spawnedEntities = await spawnController.GetSpawnedEntitiesAsync();
    
    if (spawnedEntities.Count > 0)
    {
        await spawnController.DespawnEntitiesAsync(spawnedEntities);
    }

    // ... rest of completion logic ...
}
```

### Step 5: SpawnManager Integration

**Files to Modify:**
- `Aetherium.Server/Events/SpawnControllerGrain.cs`
- `Aetherium.Server/Events/ProceduralEvents.cs`

**Changes:**
1. Inject `SpawnManager` into `SpawnControllerGrain` via ServiceProvider
2. Use `SpawnManager.GetSpawnRate()` when calculating spawn probabilities
3. Respect time-of-day and weather modifiers for spawn rates

**Example:**
```csharp
// In SpawnControllerGrain constructor or OnActivateAsync:
var spawnManager = this.ServiceProvider.GetService<SpawnManager>();

// In SpawnEntitiesAsync:
if (spawnManager != null && !string.IsNullOrEmpty(regionId))
{
    var timeOfDay = currentGameTime % 24.0;
    var spawnRate = spawnManager.GetSpawnRate(spawnType, regionId, timeOfDay, day);
    
    // Use spawnRate to adjust spawn probability
    // Only spawn if random check passes based on spawnRate
}
```

### Step 6: Narrative Event Emission

**Files to Modify:**
- `Aetherium.Server/Events/EventInstanceGrain.cs`
- `Aetherium.Server/Narrative/` (narrative grain integration)

**Changes:**
1. In event handlers, emit narrative events via `IEventInstanceGrain.BroadcastToAreaAsync()`
2. Integrate with narrative system to trigger quest updates, dialogue, etc.
3. Broadcast to all players in AOI when entities spawn/complete

**Example:**
```csharp
// In handler after spawning:
var narrativeData = new Dictionary<string, object>
{
    { "eventType", "monster_invasion" },
    { "questId", "defend_village" },
    { "entityIds", spawnResult.EntityIds },
    { "location", new { x, y, z } }
};

await eventInstanceGrain.BroadcastToAreaAsync("narrative_event", narrativeData);
```

## Testing Strategy

### Unit Tests
1. **SpawnControllerGrain Tests**
   - Test `SpawnEntitiesAsync` with valid/invalid inputs
   - Test `DespawnEntitiesAsync` removes entities
   - Test `GetSpawnedEntitiesAsync` returns correct list

2. **EventInstanceGrain Tests**
   - Test AOI tracking updates correctly
   - Test broadcast sends to correct players
   - Test completion triggers entity despawn

3. **Handler Tests**
   - Test handlers resolve event instance correctly
   - Test spawn configuration is passed correctly
   - Test narrative events are emitted

### Integration Tests
1. **End-to-End Event Spawn Flow**
   - Schedule event → Trigger event → Spawn entities → Players see spawns → Complete event → Despawn entities

2. **AOI Broadcast Tests**
   - Verify players in AOI receive broadcasts
   - Verify players outside AOI don't receive broadcasts
   - Verify broadcasts include correct event data

3. **SpawnManager Integration Tests**
   - Verify spawn rates respect time-of-day
   - Verify spawn rates respect weather
   - Verify spawn rates respect season

## Implementation Order

1. **Phase 1: Basic Integration** (Steps 1-2)
   - Enhance handlers with SpawnControllerGrain
   - Store event instance ID in ScheduledEvent
   - Basic spawn functionality

2. **Phase 2: Map/Region Resolution** (Step 3)
   - Resolve map ID from region ID
   - Ensure spawns happen on correct map

3. **Phase 3: Cleanup** (Step 4)
   - Despawn entities on event completion
   - Prevent entity leaks

4. **Phase 4: Advanced Features** (Steps 5-6)
   - SpawnManager integration
   - Narrative event emission
   - AOI broadcast enhancements

## Success Criteria

- ✅ Events can spawn entities at correct locations
- ✅ Spawned entities are visible to players in AOI
- ✅ Entities are cleaned up when events complete
- ✅ Spawn rates respect environmental modifiers
- ✅ Narrative events are emitted correctly
- ✅ All tests pass
- ✅ No entity leaks after event completion

## Future Enhancements

- **Spawn Patterns**: Support spawn patterns (line, circle, random scatter)
- **Spawn Delays**: Support staggered spawns over time
- **Dynamic Spawn Rates**: Adjust spawn rates based on player count
- **Spawn Limits**: Cap maximum spawned entities per event
- **Spawn Despawn Triggers**: Despawn entities based on conditions (time, player distance, etc.)

