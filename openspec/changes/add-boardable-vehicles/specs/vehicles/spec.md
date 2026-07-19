## ADDED Requirements

### Requirement: Vehicle Definition
A vehicle SHALL be described by authored per-world data that binds an exterior footprint, an interior map source, landing rules, and a passenger capacity. The definition MUST NOT be hardcoded and SHALL be loadable at world-creation time, consistent with the engine's data-vs-behavior split.

#### Scenario: Vehicle definition binds its pieces
- **WHEN** a vehicle definition is loaded
- **THEN** it MUST provide an exterior footprint (a box size or explicit relative cells)
- **AND** it MUST reference an interior map source (builder or prefab) with a spawn dock
- **AND** it MUST specify landing rules (valid terrain and clearance) and a passenger capacity

#### Scenario: Capacity bounds a boarding manifest
- **WHEN** a boarding manifest contains more players than the vehicle's capacity
- **THEN** the server SHALL reject the surplus
- **AND** MUST NOT move more players into the interior than the declared capacity

### Requirement: Vehicle Interior Map
A vehicle's interior SHALL be a map created within a world via `IWorldGrain.AddMapAsync`, and it SHALL tick independently like an instance so gameplay proceeds inside it whether the vehicle is parked or in transit.

#### Scenario: Interior is created as a map within a world
- **WHEN** a vehicle is initialized
- **THEN** the server SHALL create the interior via `IWorldGrain.AddMapAsync` and record the returned interior map id
- **AND** players boarded into the interior SHALL be located on that map

#### Scenario: Interior ticks independently during transit
- **WHEN** the vehicle is in transit and its interior map is active
- **THEN** the interior map SHALL continue to tick so combat, exploration, and dialogue proceed
- **AND** ticking the interior MUST NOT depend on the origin or destination surface map ticking

### Requirement: World and Map Perception Re-point
When a player is moved to a different world or map, the server SHALL re-point the player's session to that world and view location and push a fresh perception frame. This SHALL close the currently stubbed "load new world/map into session" seam in `GameHub.JoinWorld` and `GameHub.UsePortal`, and MUST keep the session's current world and the world grain's player-location record in agreement.

#### Scenario: Boarding re-points perception to the interior
- **WHEN** a player is moved into a vehicle interior map
- **THEN** the server SHALL re-point the player's session world and view location to the interior map
- **AND** SHALL push a fresh perception frame so the player perceives the interior
- **AND** both the session's current world and the world grain's player-location record MUST reflect the interior map

#### Scenario: Disembarking re-points perception to the destination surface
- **WHEN** a player is moved from a vehicle interior onto a destination surface map
- **THEN** the server SHALL re-point the player's session to the destination world and surface view location
- **AND** SHALL push a fresh perception frame showing the destination surface

#### Scenario: JoinWorld loads the target world into the session
- **WHEN** a client requests to join a target world/map that is not its current one
- **THEN** the server SHALL hydrate the session with the target world/map and push a fresh perception frame
- **AND** MUST NOT return a "not yet supported" response

### Requirement: Boarding and Disembarking
A party or manifest SHALL be able to board a landed vehicle, with each player moved onto the interior map and their session re-pointed. Disembarking SHALL reverse the operation, placing each player onto valid, passable, unoccupied surface tiles adjacent to the vehicle footprint.

#### Scenario: Party boards a landed vehicle
- **WHEN** a party interacts with a landed vehicle's board hotspot and the party fits within capacity
- **THEN** each party member SHALL be moved onto the interior map at the spawn dock
- **AND** each member's session SHALL be re-pointed to the interior and receive a fresh perception frame
- **AND** players who did not board SHALL still perceive the exterior footprint on the surface

#### Scenario: Boarding requires a landed vehicle
- **WHEN** a board request targets a vehicle that is in transit rather than landed
- **THEN** the server MUST reject the boarding request
- **AND** MUST NOT move any player onto the interior

#### Scenario: Disembarking places players on valid surface tiles
- **WHEN** players disembark at an interior airlock
- **THEN** each player SHALL be moved onto valid, passable, unoccupied surface tiles adjacent to the vehicle footprint
- **AND** each player's session SHALL be re-pointed to the surface world and receive a fresh perception frame

### Requirement: Timed Voyage
Departure SHALL remove the exterior footprint from the origin surface, start a durable Orleans reminder for a 10-30 minute real-time journey, tick the interior and fire scheduled in-transit events while en route, and on reaching the ETA place the exterior footprint at the destination surface dock and allow disembarking.

#### Scenario: Departure starts a durable voyage
- **WHEN** departure is invoked for a landed, boarded vehicle
- **THEN** the server SHALL remove the exterior footprint from the origin surface map
- **AND** SHALL compute an ETA from the route's real-time duration (between 10 and 30 minutes) via `WorldClock`
- **AND** SHALL register a durable Orleans reminder that drives the voyage and mark the vehicle in transit

#### Scenario: Voyage advances over time
- **WHEN** a voyage reminder wakes before the ETA
- **THEN** the server SHALL tick the interior map and push a voyage-progress update to passengers
- **AND** the vehicle SHALL remain in transit

#### Scenario: Arrival re-docks at the destination
- **WHEN** the voyage reaches its ETA
- **THEN** the server SHALL place the exterior footprint at the destination surface dock
- **AND** SHALL unregister the voyage reminder and transition the vehicle to landed
- **AND** disembarking onto the destination surface SHALL become available

#### Scenario: In-transit event broadcast to passengers
- **WHEN** a scheduled in-transit event becomes due during a voyage
- **THEN** the server SHALL fire the event via the event scheduler and broadcast it to everyone aboard the interior
- **AND** the interior map SHALL continue ticking so the encounter can be played out
