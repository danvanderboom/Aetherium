## ADDED Requirements

### Requirement: Transit Network Generation
The system SHALL provide a transit network generation pass that builds one or more lines for each enabled transport mode (rail, road, subway, monorail, bus). For each line the pass SHALL place stations at sites chosen with Poisson-disc spacing, connect those stations into a route using a minimum spanning tree (or a simple path), and assign every segment an altitude band so that lines on different bands grade-separate and interleave without colliding.

#### Scenario: Lines are generated per enabled mode
- **WHEN** world generation runs with a transit config that enables the subway and monorail modes
- **THEN** the generation pass SHALL produce at least one line for the subway mode and at least one line for the monorail mode
- **AND** each line SHALL carry an ordered list of stations connected into a single route

#### Scenario: Stations are placed with Poisson spacing
- **WHEN** the pass places stations for a line with a configured station spacing
- **THEN** station sites SHALL be sampled so that no two stations on the line are closer than the configured spacing
- **AND** station placement SHALL be biased toward population or points of interest

#### Scenario: Stations are connected and reachable
- **WHEN** a line's stations are connected into a route by the minimum spanning tree
- **THEN** every station on the line SHALL be reachable from every other station along the line's segments
- **AND** generation validation SHALL report no stranded or unreachable stations

#### Scenario: Multi-level lines do not collide across bands
- **WHEN** a subway line at band -2, a surface line at band 0, and a monorail line at band +3 pass through the same (x,y) column
- **THEN** each line's segment SHALL occupy only its assigned altitude band at that column
- **AND** no two segments SHALL occupy the same band at the same tile
- **AND** the surface line SHALL remain passable at band 0 while the monorail viaduct occupies band +3

### Requirement: Corridor Cross-Section Profile
A transit line SHALL carry a corridor cross-section profile that defines the corridor's total width in tiles, its track or lane count, the running terrain of the central way, the altitude band of that way, and the flank structures placed on each side. The corridor carver MUST stamp corridors that are multiple tiles wide by laying the profile perpendicular to the line direction at each step, placing running terrain in the center and flank structures on the sides.

#### Scenario: A wide corridor stamps running terrain plus flanking platforms
- **WHEN** the carver processes a line whose profile has width 9, two tracks, running terrain "Rail", and flank structures for platforms and walls
- **THEN** each step along the line SHALL stamp 9 tiles across, perpendicular to the line direction
- **AND** the central tiles SHALL be set to the running terrain
- **AND** the flanking tiles SHALL be set to platform and wall structures on each side

### Requirement: Stations and Stops
Stations and stops SHALL be entities that carry a station component referencing the owning line id, a schedule reference, the platform tiles that form the boarding area, and a board hotspot tile that characters interact with to board. Rail and subway stations SHALL include a platform sub-area; bus stops MAY be lightweight surface markers placed along city streets.

#### Scenario: A generated station exposes line, schedule, platform, and hotspot
- **WHEN** the generation pass creates a subway station for a line
- **THEN** the station entity SHALL reference the owning line id
- **AND** SHALL reference the schedule that serves the station
- **AND** SHALL record the platform tiles that form the boarding area
- **AND** SHALL expose a board hotspot tile that a character can interact with to board a waiting service

#### Scenario: Bus stops are lightweight surface markers
- **WHEN** the pass places bus stops along a city street for a bus line
- **THEN** each stop SHALL be an entity that references its line id and a board hotspot
- **AND** the stop SHALL be placed on the surface band along the street

### Requirement: Scheduled Services
A scheduled service SHALL follow a timetabled route that visits its line's stations in order, dwelling at each station to board and alight passengers, and looping on a configured headway. A recurring dispatch SHALL spawn or release the service vehicle at the configured headway interval.

#### Scenario: Recurring dispatch on headway
- **WHEN** a scheduled service is configured with a headway of 6 game minutes
- **THEN** the dispatcher SHALL release a service vehicle onto the route every 6 game minutes
- **AND** each released vehicle SHALL follow a scheduled plan that visits the line's stations in order

#### Scenario: Dwell boards waiting passengers
- **WHEN** a scheduled service arrives at a station where passengers are waiting
- **THEN** the service SHALL dwell at the station for the configured dwell time
- **AND** SHALL board the waiting passengers and alight any passengers whose destination is that station
- **AND** SHALL depart toward the next station when the dwell completes

#### Scenario: Service loops along its route
- **WHEN** a scheduled service reaches the final station on its route
- **THEN** the service SHALL loop and continue serving the route according to its timetable

### Requirement: AdHoc Services
A player SHALL be able to summon or hail a vehicle using an affordance (kiosk, app, or whistle); the nearest idle vehicle MUST generate an on-demand route to the caller, arrive, and after boarding present a destination menu using multi-option selection so the player can choose the next leg.

#### Scenario: Summoned vehicle routes to the caller and offers destinations
- **WHEN** a player uses a summon or hail affordance
- **THEN** the nearest idle vehicle SHALL generate an ad-hoc route to the player's location
- **AND** the vehicle SHALL travel to and arrive at the caller
- **WHEN** the player boards the arrived vehicle
- **THEN** the client SHALL present a destination menu as a multi-option selection
- **AND** the chosen destination SHALL become the vehicle's next ad-hoc leg

### Requirement: Manual Piloting
A player SHALL be able to take a pilot seat of a service vehicle; while piloted the vehicle MUST have no active flight or route plan and SHALL be driven directly by the player's controls. Leaving the pilot seat SHALL return control to the player's avatar.

#### Scenario: Taking the pilot seat enables direct control
- **WHEN** a player takes the pilot seat of a vehicle
- **THEN** the vehicle SHALL have no active flight or route plan
- **AND** the vehicle SHALL be driven directly by the player's piloting controls
- **WHEN** the player leaves the pilot seat
- **THEN** control SHALL return to the player's avatar
- **AND** the vehicle SHALL become idle or resume its prior plan

### Requirement: Inhabited Corridors
A transit venue generation pass SHALL run after the network is generated and stamp prefab venues (shops, eateries, bars, rooms) into the flank strips and concourse levels of wide underground or elevated corridors, producing ordinary walkable map content that players can explore.

#### Scenario: Venue pass populates a wide underground corridor
- **WHEN** the venue pass processes a wide subway concourse whose config lists venues such as shop, cafe, and bar
- **THEN** the pass SHALL stamp prefab venues into the corridor's flank strips and concourse levels
- **AND** the stamped venues SHALL be walkable map areas that a player can enter and explore
- **AND** the venues MAY be populated with NPCs
