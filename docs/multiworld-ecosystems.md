# Multi-World Ecosystems

The multi-world ecosystem system enables players to travel between worlds, engage in cross-world economies, complete cross-world quests, and unlock new content through meta-progression.

## Overview

The multi-world ecosystem consists of several interconnected systems:

- **World Clusters**: Groups of worlds that share economy and portal networks
- **Portal Network**: Inter-world travel via portal entities placed during world generation
- **Cluster-Scoped Economy**: Markets, trade routes, and scheduled transports that span multiple worlds
- **Meta-Progression**: Player discovery tracking and generator unlocks across worlds
- **Hub Worlds**: Authored or procedural hub worlds that connect multiple procedural zones

## World Clusters

Worlds can be organized into clusters that share economy scope and portal connectivity.

### Creating Clusters

Clusters are created via the `IClusterGrain` interface:

```csharp
var clusterGrain = client.GetGrain<IClusterGrain>(clusterId);
var clusterInfo = new ClusterInfo
{
    ClusterId = clusterId,
    Name = "My Cluster",
    Description = "A cluster of connected worlds",
    WorldIds = new HashSet<string>()
};

await clusterGrain.InitializeAsync(clusterInfo);
```

### Registering Worlds

When creating a world, specify a `ClusterId` to automatically register it with the cluster:

```csharp
var request = new CreateWorldRequest
{
    Name = "World 1",
    ClusterId = "my-cluster" // Automatically registers with cluster
};
```

## Portal Network

Portals are entities placed during world generation that enable travel between worlds within a cluster.

### Portal Placement

Portals are automatically placed by `PortalNetworkPass` during world generation:
- **Procedural worlds**: 1-3 portals placed at strategic locations (near objectives, along paths)
- **Hub worlds**: Portals placed according to hub definition JSON files

### Portal Resolution

Portals can specify target worlds/maps by:
- **Tag**: `TargetTag` matches worlds/maps with specific tags
- **Direct**: `TargetWorldId` and `TargetMapId` when targets are known
- **Cluster resolution**: Portal targets are resolved at runtime via `IClusterGrain.ResolvePortalTargetAsync()`

### Using Portals

Players interact with portals via the `GameHub.UsePortal()` method:

```csharp
// Portal usage is handled automatically via GameHub interactions
// The system resolves portal targets and transports players
```

## Cluster-Scoped Economy

Economies span all worlds within a cluster, enabling cross-world commerce.

### Markets

Each world/map in a cluster has its own market:
- Markets track resource availability and pricing
- Prices update based on supply/demand during economy ticks
- Markets are automatically created when maps are registered with a cluster

### Trade Routes

Trade routes connect markets between worlds:
- Define source and destination markets
- Specify resource types and capacity
- Configure travel time between markets

### Transport Schedules

Scheduled transports move resources between markets:
- Transport cargo according to trade routes
- Arrival time calculated from departure time and travel time
- Resources update destination market availability on arrival

### Economy Ticking

The economy ticks periodically (every 5 minutes by default):
- Updates market prices based on supply/demand
- Processes transport schedules and updates resource availability
- Automatically manages trade routes and transports

## Meta-Progression

Meta-progression tracks player discoveries and unlocks new content across worlds.

### Discovery Tracking

The system tracks:
- **World visits**: Worlds and maps visited by the player
- **World templates**: Generator templates discovered
- **Tags**: Tags associated with visited worlds (e.g., "dungeon", "city", "hub")
- **Quest completions**: Cross-world quests completed

### Unlock System

Unlocks are evaluated based on criteria:
- **Minimum world visits**: Total number of worlds visited
- **Tag requirements**: Specific tags that must be discovered
- **Quest requirements**: Specific quests or cross-world quest counts
- **World template requirements**: Specific templates that must be discovered

### Generator Unlocks

Unlocked generators are available for world creation:
- Default generators (PerlinTerrain, BasicDungeon) are always available
- Advanced generators unlock as players explore and complete objectives
- `GetAllowedGeneratorsAsync()` returns only unlocked generators for UI filtering

## Hub Worlds

Hub worlds are special worlds that connect multiple procedural zones.

### Hub Definitions

Hub worlds are defined in JSON files in `Data/Hubs/`:

```json
{
  "HubId": "central-hub",
  "Name": "Central Hub",
  "Description": "A central hub connecting multiple zones",
  "GeneratorType": "hub",
  "Size": {
    "Width": 200,
    "Height": 200,
    "Depth": 1
  },
  "Tags": ["hub", "central", "starting-area"],
  "Portals": [
    {
      "PortalId": "hub-to-dungeon",
      "TargetWorldTag": "dungeon",
      "TargetMapTag": "entrance"
    },
    {
      "PortalId": "hub-to-city",
      "TargetWorldTag": "city",
      "Activation": "unlocked"
    }
  ]
}
```

### Creating Hub Worlds

Hub worlds can be created using the hub template:

```csharp
var request = new CreateWorldRequest
{
    Name = "My Hub",
    GeneratorType = "hub:central-hub", // Or just "central-hub"
    ClusterId = "my-cluster"
};
```

The `HubTemplateResolver` automatically resolves hub templates to `WorldConfig` with portal definitions embedded in generator parameters.

## Cross-World Quests

The narrative system supports cross-world objectives and constraints.

### Travel Objectives

Quests can include `travel_to` objectives that require players to visit specific worlds/maps:
- Objectives specify target tags or world IDs
- Objectives are evaluated via `NarrativeStateGrain` events
- Completion is tracked in `MetaProgressionGrain`

### Cross-World Constraints

Narrative constraints can span multiple worlds:
- `CrossWorldConstraint` types enable multi-world quest chains
- Constraints are resolved via `ClusterGrain` for portal resolution
- Quest progress is tracked per-player via `NarrativeStateGrain`

## REST APIs

### Cluster Management

- `GET /api/cluster` - List all clusters
- `POST /api/cluster` - Create a cluster
- `GET /api/cluster/{clusterId}` - Get cluster info
- `GET /api/cluster/{clusterId}/worlds` - List worlds in cluster
- `GET /api/cluster/{clusterId}/portals` - List portals in cluster
- `POST /api/cluster/{clusterId}/portals` - Register a portal
- `POST /api/cluster/{clusterId}/portals/{portalId}/resolve` - Resolve portal target

### Economy

- `GET /api/cluster/{clusterId}/markets/{worldId}/{mapId}` - Get market info
- `POST /api/cluster/{clusterId}/routes` - Create trade route
- `GET /api/cluster/{clusterId}/routes/{routeId}` - Get trade route
- `POST /api/cluster/{clusterId}/transports` - Schedule transport
- `GET /api/cluster/{clusterId}/transports` - List transport schedules
- `GET /api/cluster/{clusterId}/economy` - Get economy state
- `POST /api/cluster/{clusterId}/economy/tick` - Manually tick economy

### Meta-Progression

- `GET /api/metaprogression/{playerId}` - Get meta-progression state
- `GET /api/metaprogression/{playerId}/discoveries` - Get discovery details
- `POST /api/metaprogression/{playerId}/discoveries` - Record discovery
- `POST /api/metaprogression/{playerId}/quests/{questId}/complete` - Record quest completion
- `GET /api/metaprogression/{playerId}/unlocks` - Get unlocked generators
- `GET /api/metaprogression/{playerId}/generators` - Get allowed generators
- `GET /api/metaprogression/{playerId}/generators/{generatorName}/unlocked` - Check unlock status
- `POST /api/metaprogression/{playerId}/unlocks/evaluate` - Evaluate unlock criteria
- `POST /api/metaprogression/{playerId}/unlocks/criteria` - Add unlock criteria

## Code Structure

### Core Components

- `Aetherium.Server/MultiWorld/`:
  - `IClusterGrain`, `ClusterGrain` - Cluster and economy management
  - `ClusterModels.cs` - Cluster, market, trade route models
  - `GameMapGrain.cs` - Map registration and portal discovery
  - `WorldGrain.cs` - World cluster registration

- `Aetherium.Server/MetaProgression/`:
  - `IMetaProgressionGrain`, `MetaProgressionGrain` - Player meta-progression
  - `MetaProgressionModels.cs` - Discovery and unlock models

- `Aetherium.Server/HubWorld/`:
  - `HubWorldLoader` - Loads hub definitions from JSON
  - `HubWorldGenerator` - Generates `WorldConfig` from hub definitions
  - `HubTemplateResolver` - Resolves hub templates in world creation

- `Aetherium.Server/WorldGen/Passes/`:
  - `PortalNetworkPass` - Places portal entities during generation

- `Aetherium.Server/Controllers/`:
  - `ClusterController` - REST API for clusters, portals, economy
  - `MetaProgressionController` - REST API for meta-progression

### Data Files

- `Data/Hubs/*.json` - Hub world definitions (e.g., `central-hub.json`)

## Examples

### Creating a Multi-World Cluster

```csharp
// 1. Create cluster
var clusterGrain = client.GetGrain<IClusterGrain>("my-cluster");
await clusterGrain.InitializeAsync(new ClusterInfo
{
    ClusterId = "my-cluster",
    Name = "Adventure Cluster",
    WorldIds = new HashSet<string>()
});

// 2. Create worlds with cluster ID
var world1 = await mgmt.CreateWorldAsync(new CreateWorldRequest
{
    Name = "Starting Hub",
    GeneratorType = "hub:central-hub",
    ClusterId = "my-cluster"
});

var world2 = await mgmt.CreateWorldAsync(new CreateWorldRequest
{
    Name = "Dungeon Zone",
    GeneratorType = "rooms-and-corridors",
    ClusterId = "my-cluster"
});

// 3. Portals are automatically registered and resolved by the cluster
```

### Tracking Meta-Progression

```csharp
var metaGrain = client.GetGrain<IMetaProgressionGrain>(playerId);

// Record discovery
await metaGrain.RecordDiscoveryAsync("world-1", "map-1", "outdoor", new List<string> { "forest" });

// Check unlocks
var generators = await metaGrain.GetAllowedGeneratorsAsync();

// Evaluate unlocks (happens automatically, but can be triggered manually)
await metaGrain.EvaluateUnlocksAsync();
```

### Managing Economy

```csharp
var clusterGrain = client.GetGrain<IClusterGrain>(clusterId);

// Create trade route
var route = await clusterGrain.CreateTradeRouteAsync(new TradeRoute
{
    RouteId = "route-1",
    SourceMarketId = "world-1:map-1",
    DestinationMarketId = "world-2:map-1",
    ResourceTypes = new List<string> { "ore", "wood" },
    Capacity = 100,
    TravelTime = TimeSpan.FromHours(1)
});

// Schedule transport
var schedule = await clusterGrain.ScheduleTransportAsync(route, new Dictionary<string, int>
{
    { "ore", 50 },
    { "wood", 30 }
}, DateTime.UtcNow);

// Economy ticks automatically, or can be triggered manually
await clusterGrain.TickEconomyAsync();
```

## Testing

Unit and integration tests are available in `Aetherium.Test/Orleans/`:
- `ClusterGrainTests.cs` - Cluster, portal, economy tests
- `MetaProgressionGrainTests.cs` - Meta-progression and unlock tests
- `PortalNetworkPassTests.cs` - Portal placement tests

## Related Documentation

- [Narrative Systems](narrative-systems.md) - Cross-world quests and constraints
- [Development Guide](development.md) - Developer setup and testing
- [Client-Server Architecture](architecture/overview.md) - SignalR and Orleans usage

---

**Last Updated:** November 2025  
**Feature Status:** ✅ Complete and tested

