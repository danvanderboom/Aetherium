# multiworld Specification

## Purpose
Defines multi-world clustering: grouping multiple worlds into a single game universe/session that shares a cluster-scoped economy and a portal network, with runtime resolution of portal links to concrete worlds and maps.
## Requirements
### Requirement: World Clusters
The system SHALL support clustering multiple worlds into a single game universe/session, enabling shared economy and portal networks.

#### Scenario: Cluster creation
- **WHEN** a cluster is created via IClusterGrain
- **THEN** it SHALL have a unique ClusterId and track associated WorldIds
- **AND** worlds in the cluster SHALL share economy scope and portal connectivity

#### Scenario: World registration to cluster
- **WHEN** a WorldGrain is configured with ClusterId
- **THEN** on initialization it SHALL register itself with IClusterGrain
- **AND** all maps within that world SHALL be registered for portal resolution

### Requirement: Cluster-Scoped Economy
Economies SHALL span all worlds within a cluster, with markets, trade routes, and scheduled transports enabling cross-world commerce.

#### Scenario: Market per world/map
- **WHEN** a world/map is registered with a cluster
- **THEN** IClusterGrain SHALL create a Market for that world/map
- **AND** markets SHALL track resource availability and pricing per location

#### Scenario: Trade routes
- **WHEN** TradeRoutes are defined between markets in a cluster
- **THEN** they SHALL specify source/destination markets and resource types
- **AND** transport schedules SHALL manage timing and capacity for cross-world shipments

#### Scenario: Economy ticking
- **WHEN** IClusterGrain.TickEconomyAsync() is called
- **THEN** it SHALL update market prices based on supply/demand
- **AND** it SHALL process transport schedules and update resource availability at destinations

### Requirement: Portal Link Resolution
Portals SHALL be resolved to target worlds/maps within the same cluster at runtime, enabling player travel between worlds.

#### Scenario: Portal registration
- **WHEN** PortalNetworkPass places portals during generation
- **THEN** GameMapGrain SHALL register portal metadata with IClusterGrain
- **AND** IClusterGrain SHALL resolve link hints (tags, hub references) to concrete WorldId/MapId pairs

#### Scenario: Portal activation
- **WHEN** a player interacts with a portal
- **THEN** the system SHALL query IClusterGrain for the resolved target WorldId/MapId
- **AND** if Activation requirements are met, it SHALL transport the player to the target location
