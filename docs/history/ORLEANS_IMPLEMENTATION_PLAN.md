# Orleans Multi-World Implementation Plan

## Status

### Completed ✓
- [x] **Host abstraction and Orleans implementation** - `IWorldHost`, `OrleansWorldHost`, `WorldDirectoryGrain`, `WorldGrain`, `WorldAclGrain`, `WorldInviteGrain` implemented
- [x] **Admin CLI and server endpoints** - `aetherctl` commands for `world create/list/invite/accept-invite/set-acl` implemented
- [x] **Party/Raid grains and LockoutLedger** - `PartyGrain`, `RaidGrain`, `LockoutLedgerGrain` implemented with lockout checks
- [x] **InstanceAllocator and DungeonInstance grains** - `IInstanceAllocatorGrain`, `InstanceAllocatorGrain`, `IDungeonInstanceGrain`, `DungeonInstanceGrain` implemented with Enter/Rejoin flows and allocation logic

### Not Started ⏳
- [ ] **EventScheduler, EventInstance, and AOI broadcasts** - `EventScheduler` exists but not as grain; needs `EventInstanceGrain`, `SpawnControllerGrain`, trigger APIs, AOI broadcasts
- [ ] **Travel system** - Missing `TravelNetworkGrain`, `TravelNodeGrain`, `WaypointServiceGrain`, `HearthServiceGrain`, `MountServiceGrain`
- [ ] **Housing system** - Missing `PlotDirectoryGrain`, `HousingPlotGrain`, `PlayerHomeGrain`, `GuildHallGrain`, `DecorationGrain`, `StorageChestGrain`
- [ ] **Faction system** - Missing `FactionDirectoryGrain`, `FactionGrain`, `ReputationGrain`, `AlignmentServiceGrain`
- [ ] **Territory control** - Missing `TerritoryGrain`, `ContributionGrain`
- [ ] **Console UI** - Basic console UI exists but needs commands/widgets for new features (party, instances, travel, housing, factions, etc.)
- [ ] **Testing** - Need slice-through tests and CLI recipes for demo scenarios

## Implementation Details

### Instance System (In Progress)
- `IInstanceAllocatorGrain`: Allocates dungeon instances for parties/players
- `InstanceAllocatorGrain`: Coordinates instance creation, lockout checks, allocation
- `IDungeonInstanceGrain`: Manages individual dungeon instance lifecycle
- `DungeonInstanceGrain`: Handles instance state, players, world state, event streams

