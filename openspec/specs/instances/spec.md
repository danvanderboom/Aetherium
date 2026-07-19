# instances Specification

## Purpose
TBD - created by archiving change add-instance-lifecycle-and-entry. Update Purpose after archive.
## Requirements
### Requirement: Instance Map Lifecycle
Releasing or sweeping a dungeon instance SHALL free its map so that abandoned instances stop consuming tick and storage resources. A world SHALL support removing a map from its map set.

#### Scenario: Removing a map stops it being ticked
- **WHEN** `IWorldGrain.RemoveMapAsync(mapId)` is called for a map the world owns
- **THEN** the map id is removed from the world's map set and no longer appears in `GetMapIdsAsync`
- **AND** any player locations pointing at that map are dropped

#### Scenario: Releasing an instance frees its map
- **WHEN** `InstanceAllocatorGrain.ReleaseInstanceAsync(instanceId)` is called for a tracked instance
- **THEN** the instance's party/player mappings are removed
- **AND** the instance's map is removed from the world (it is no longer ticked)

#### Scenario: The sweeper reaps abandoned instances
- **WHEN** `SweepAbandonedInstancesAsync()` runs and an instance reports `Abandoned` or `Stopped` (or has been idle past the threshold)
- **THEN** that instance is shut down and its map freed
- **AND** the number of instances reaped is returned

### Requirement: Lockout Recording Is Idempotent Per Instance
Recording a lockout SHALL count a distinct instance entry once. Re-entering the same instance (reuse) SHALL NOT increment the attempt count or extend the lockout window, and the reuse path SHALL record a lockout rather than bypassing it.

#### Scenario: Re-entering the same instance does not double-count
- **WHEN** `RecordLockoutAsync` is called more than once for the same party/player and the same instance id
- **THEN** the lockout entry's attempt count and lockout-until are unchanged after the first recording

#### Scenario: Reuse still records a lockout
- **WHEN** a party or player re-enters via the instance-reuse path
- **THEN** a lockout is recorded (or confirmed) rather than the reuse path silently bypassing lockout recording

### Requirement: Instance & Party Entry Surface
Players SHALL be able to form a party and enter a dungeon instance for their world, and this surface SHALL be reachable over the game hub and via an agent tool and the CLI.

#### Scenario: Entering a dungeon over the game hub
- **WHEN** a joined player calls `GameHub.EnterDungeon(dungeonId)` (optionally with a party id) for their current world
- **THEN** the world's instance allocator is resolved from the session and an instance is allocated or reused for the caller (or the whole party)
- **AND** the result reports the instance id and its map id, or an error

#### Scenario: Party operations over the game hub
- **WHEN** a player calls `GameHub.CreateParty` / `JoinParty` / `LeaveParty` / `GetParty`
- **THEN** the corresponding party grain is created or updated and its membership is returned

#### Scenario: Entry surface is reachable by agents and the CLI
- **WHEN** a player-profile agent uses the `enter_dungeon` / `create_party` tools, or an operator runs `aetherctl instance enter|sweep` / `party create|add|show`
- **THEN** the same allocator/party grains are resolved and the corresponding entry/inspection is performed

