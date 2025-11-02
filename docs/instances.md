# Instance System Documentation

The instance system enables players to enter private dungeon instances with lockout mechanics, party support, and automatic resource management.

## Overview

The instance system consists of two main Orleans grains:

1. **InstanceAllocatorGrain** - Allocates and manages dungeon instances for parties/players
2. **DungeonInstanceGrain** - Manages individual instance lifecycle and player state

## Architecture

### Instance Allocation Flow

1. Player/Party requests to enter a dungeon via `IInstanceAllocatorGrain.EnterAsync()`
2. Allocator checks lockout status via `ILockoutLedgerGrain.CheckLockoutAsync()`
3. If not locked out, attempts to reuse existing instance for party/player
4. If no existing instance, allocates new instance and creates a map
5. Records lockout entry after successful entry
6. Returns instance ID and lockout key to caller

### Instance Lifecycle

Instances go through the following states:

- **Creating** - Instance is being initialized
- **Active** - Instance is running with players
- **Completed** - Instance objectives completed (future)
- **Abandoned** - All players left, instance marked for cleanup
- **ShuttingDown** - Instance is being cleaned up
- **Stopped** - Instance is fully stopped

## API Reference

### IInstanceAllocatorGrain

**Key:** World ID (string)

Allocates dungeon instances for parties/players within a world.

```csharp
// Enter an instance
var request = new EnterInstanceRequest
{
    WorldId = new WorldId("world-123"),
    DungeonId = new DungeonId("dungeon-fire-temple"),
    PartyId = partyId, // Optional: null for solo
    PlayerIds = new List<PlayerId> { playerId1, playerId2 }
};

var result = await allocatorGrain.EnterAsync(request);
if (result.Success)
{
    var instanceId = result.InstanceId;
    var lockoutKey = result.LockoutKey;
    // Move players to instance map
}
else
{
    // Handle error: result.ErrorMessage
}
```

**Methods:**
- `Task<EnterInstanceResult> EnterAsync(EnterInstanceRequest request)` - Entry point for entering instances
- `Task<InstanceId> AllocateInstanceAsync(DungeonId, PartyId?, List<PlayerId>)` - Allocates new instance
- `Task<InstanceId?> GetOrReuseInstanceAsync(...)` - Reuses existing instance if available
- `Task ReleaseInstanceAsync(InstanceId)` - Releases instance resources

### IDungeonInstanceGrain

**Key:** Instance ID (string)

Manages a single dungeon instance lifecycle.

```csharp
// Get instance information
var instanceGrain = grainFactory.GetGrain<IDungeonInstanceGrain>(instanceId.Value);
var info = await instanceGrain.GetInfoAsync();

// Add players
await instanceGrain.AddPlayersAsync(new List<PlayerId> { playerId });

// Remove player
await instanceGrain.RemovePlayerAsync(playerId);

// Check if player is in instance
var isInInstance = await instanceGrain.IsPlayerInInstanceAsync(playerId);

// Get instance map ID (for teleportation)
var mapId = await instanceGrain.GetMapIdAsync();
```

**Methods:**
- `Task InitializeAsync(InstanceConfig config)` - Initializes instance with config
- `Task<InstanceInfo?> GetInfoAsync()` - Gets instance information
- `Task<bool> AddPlayersAsync(List<PlayerId>)` - Adds players to instance
- `Task RemovePlayerAsync(PlayerId)` - Removes player from instance
- `Task<List<PlayerId>> GetPlayersAsync()` - Gets all players in instance
- `Task<bool> IsPlayerInInstanceAsync(PlayerId)` - Checks if player is in instance
- `Task TickAsync(TimeSpan gameTimeElapsed)` - Processes game tick
- `Task<string?> GetMapIdAsync()` - Gets map ID for this instance
- `Task ShutdownAsync()` - Shuts down instance and releases resources

## Lockout System

The instance system integrates with `ILockoutLedgerGrain` to enforce entry restrictions:

- **Time-based lockouts** - Instance locked for duration (e.g., 24 hours)
- **Attempt-based lockouts** - Instance locked after max attempts
- **Hybrid lockouts** - Both time and attempt restrictions

Lockout policies are set per dungeon via `ILockoutLedgerGrain.SetPolicyAsync()`.

## Usage Examples

### Solo Instance Entry

```csharp
var worldId = new WorldId("world-123");
var allocator = grainFactory.GetGrain<IInstanceAllocatorGrain>(worldId.Value);

var request = new EnterInstanceRequest
{
    WorldId = worldId,
    DungeonId = new DungeonId("dungeon-fire-temple"),
    PartyId = null, // Solo
    PlayerIds = new List<PlayerId> { playerId }
};

var result = await allocator.EnterAsync(request);
if (result.Success && result.InstanceId.HasValue)
{
    var instanceGrain = grainFactory.GetGrain<IDungeonInstanceGrain>(result.InstanceId.Value.Value);
    var mapId = await instanceGrain.GetMapIdAsync();
    
    // Move player to instance map
    var worldGrain = grainFactory.GetGrain<IWorldGrain>(worldId.Value);
    await worldGrain.MovePlayerToMapAsync(playerId.Value, mapId);
}
```

### Party Instance Entry

```csharp
// Get party members
var partyGrain = grainFactory.GetGrain<IPartyGrain>(partyId.Value);
var memberIds = await partyGrain.GetMemberIdsAsync();

var request = new EnterInstanceRequest
{
    WorldId = worldId,
    DungeonId = new DungeonId("dungeon-fire-temple"),
    PartyId = partyId,
    PlayerIds = memberIds
};

var result = await allocator.EnterAsync(request);
// ... same as solo entry
```

### Rejoining an Instance

The allocator automatically reuses existing instances for parties/players:

```csharp
// First entry creates instance
var result1 = await allocator.EnterAsync(request1);

// Second entry (same party) reuses existing instance
var result2 = await allocator.EnterAsync(request2);
// result2.InstanceId == result1.InstanceId
```

## Integration with World System

Instances are created as maps within the world:

1. `InstanceAllocatorGrain` calls `IWorldGrain.AddMapAsync()` to create instance map
2. Map ID is stored in `InstanceConfig.GeneratorParameters["mapId"]`
3. Players are moved to instance map via `IWorldGrain.MovePlayerToMapAsync()`
4. Instance map ticks independently via `IGameMapGrain.TickAsync()`

## Cleanup and Resource Management

Instances are automatically cleaned up when:

- All players leave (marked as `Abandoned`)
- Instance is explicitly shut down via `ShutdownAsync()`
- Allocator releases instance via `ReleaseInstanceAsync()`

On cleanup:
1. Players are removed from instance
2. Instance state set to `Stopped`
3. Allocator removes instance tracking
4. Map grain can be deactivated (via Orleans idle deactivation)

## Configuration

### Instance Config

```csharp
var config = new InstanceConfig
{
    InstanceId = new InstanceId(Guid.NewGuid().ToString()),
    DungeonId = new DungeonId("dungeon-fire-temple"),
    WorldId = new WorldId("world-123"),
    DungeonName = "Fire Temple",
    GeneratorType = "dungeon",
    GeneratorParameters = new Dictionary<string, object>
    {
        { "dungeonId", "dungeon-fire-temple" },
        { "instanceId", instanceId.Value },
        { "mapId", mapId }, // Set by allocator
        { "MaxPlayers", 10 }
    },
    PartyId = partyId, // Optional
    PlayerIds = playerIds,
    CreatedAt = DateTime.UtcNow
};
```

### Lockout Policy

```csharp
var policy = new LockoutPolicy
{
    DungeonId = new DungeonId("dungeon-fire-temple"),
    Type = LockoutType.TimeBased,
    Duration = TimeSpan.FromHours(24),
    MaxAttempts = -1, // Unlimited
    ResetOnSuccess = false
};

var ledgerGrain = grainFactory.GetGrain<ILockoutLedgerGrain>("dungeon-fire-temple");
await ledgerGrain.SetPolicyAsync(policy);
```

## Best Practices

1. **Always check lockout before entry** - Use `EnterAsync()` which handles lockout checks
2. **Reuse instances when possible** - Allocator automatically reuses instances for parties
3. **Handle errors gracefully** - `EnterInstanceResult` includes error messages
4. **Clean up on player disconnect** - Remove players from instance when they disconnect
5. **Set appropriate lockout policies** - Balance between preventing abuse and allowing replay

## Related Systems

- **LockoutLedgerGrain** - Manages lockout policies and entries
- **PartyGrain/RaidGrain** - Manages party composition
- **WorldGrain** - Creates and manages instance maps
- **GameMapGrain** - Runs instance world simulation

## Future Enhancements

- Instance completion detection (boss defeated, objectives completed)
- Instance difficulty scaling based on party size
- Cross-world instance support
- Instance persistence across server restarts
- Instance telemetry and metrics

