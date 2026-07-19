## ADDED Requirements
### Requirement: Flight Plans
The system SHALL represent every autonomous flyer's route as a `FlightPlan` whose `Source` is one of `Manual`, `AdHoc`, `Scheduled`, or `Patterned`, and a tick-driven follower SHALL advance the plan's legs at the entity's action cadence.

#### Scenario: Patterned plan is followed
- **WHEN** a flyer carries a `FlightPlan` with `Source = Patterned`
- **THEN** the follower MUST generate legs from the plan's pattern and step the flyer toward the current leg each cadence tick

#### Scenario: AdHoc plan is followed
- **WHEN** a flyer is issued a `FlightPlan` with `Source = AdHoc` toward a chosen destination
- **THEN** the follower MUST advance the flyer along the generated legs until the destination is reached

#### Scenario: Scheduled plan is followed
- **WHEN** a flyer carries a `FlightPlan` with `Source = Scheduled` and per-leg arrival times
- **THEN** the follower MUST advance legs to honor the timetable's arrival times

#### Scenario: Manual plan is not auto-advanced
- **WHEN** a flyer carries a `FlightPlan` with `Source = Manual`
- **THEN** the follower MUST NOT move the entity, leaving movement to an external controller

#### Scenario: Follower advances at the action cadence
- **WHEN** the tick chain runs and a non-Manual flyer has not yet reached its current leg
- **THEN** the follower MUST perform at most one cadence-move toward that leg per the entity's action cadence

### Requirement: Movement Patterns
Patterned flight plans SHALL generate their legs from a pattern identifier, supporting at minimum `orbit`, `patrol`, `wander`, and `hover`.

#### Scenario: Orbit pattern
- **WHEN** a Patterned plan has `PatternId = "orbit"` with a center and band
- **THEN** the generator MUST produce a closed ring of legs at that band around the center

#### Scenario: Patrol pattern
- **WHEN** a Patterned plan has `PatternId = "patrol"` between anchor points
- **THEN** the generator MUST produce legs that traverse back and forth (or a circuit) between the anchors

#### Scenario: Wander pattern
- **WHEN** a Patterned plan has `PatternId = "wander"` bounded to a region and band range
- **THEN** the generator MUST produce a bounded random-walk sequence of legs within that region and band range

#### Scenario: Hover pattern
- **WHEN** a Patterned plan has `PatternId = "hover"` at a cell and band
- **THEN** the generator MUST hold that cell and band with only small jitter

### Requirement: Cruising Altitude Rule
The system SHALL assign an autonomous flyer's cruise band from its heading using per-world semicircular-rule data so that opposing traffic self-separates by altitude, and landing, takeoff, hover, and manual flight SHALL be exempt from the rule.

#### Scenario: Eastbound and westbound cruise bands differ
- **WHEN** the per-world cruise rule maps eastbound headings and westbound headings to different band sets
- **AND** one flyer is heading eastbound and another westbound over the same column
- **THEN** the follower MUST place each flyer in the band set for its heading, separating them by altitude

#### Scenario: Heading change re-selects the cruise band
- **WHEN** an autonomous flyer turns past the threshold between two heading ranges
- **THEN** the follower MUST climb or descend it to the cruise band appropriate to the new heading

#### Scenario: Manual flight is exempt from the cruise rule
- **WHEN** a flyer is under manual/piloted control, or is landing, taking off, or hovering
- **THEN** the cruise rule MUST NOT override its altitude

### Requirement: Collision Policy
The system SHALL apply a per-world or per-flyer-class collision policy of either `separated` (default) or `collidable`, and a collision under `collidable` SHALL raise an event.

#### Scenario: Separated policy keeps flyers apart
- **WHEN** the collision policy is `separated`
- **THEN** the cruise rule and occupancy MUST keep flyers from overlapping
- **AND** no two footprints MUST occupy the same cell and band

#### Scenario: Collidable policy raises a collision event
- **WHEN** the collision policy is `collidable`
- **AND** two flyers occupy the same cell and band
- **THEN** the system MUST raise a collision event for downstream reactions or damage

### Requirement: Landing and Takeoff
The system SHALL provide `land` and `takeoff` tools that transition a flyer through the state machine Airborne ↔ Landing/TakingOff ↔ Landed. Landing lowers the flyer onto the top of the next obstruction below it — a structure top, a terrain peak, or the ground floor — coming to rest co-located with that band (rendered on top). Takeoff raises the flyer above whatever it was resting on into clear air. Which surfaces are landable depends on the flyer: a per-flyer `LandableTerrain` set governs terrain surfaces (a bird lands on forest/mountain/water, a wheeled plane only on flat dry ground), falling back to per-world landing-terrain data when unset; a structure top is landable by any flyer with `Flight.CanLand`.

#### Scenario: Land on the next obstruction below
- **WHEN** the `land` tool is invoked and `Flight.CanLand` is true
- **AND** the flyer descends toward the highest surface in its column below its band
- **THEN** the flyer MUST come to rest co-located with that surface's top band — the ground floor at the terrain band, a tall terrain feature at its peak, or a structure at its top

#### Scenario: Terrain landability depends on the flyer
- **WHEN** the `land` tool is invoked and the resting surface is terrain
- **THEN** the flyer MUST land only if that terrain type is in the flyer's `LandableTerrain` set (or, when that set is empty, the world's landing-terrain set)
- **AND** a different flyer over the same terrain whose set excludes it MUST be refused and remain airborne

#### Scenario: Land on top of a structure
- **WHEN** the resting surface below a landing-capable flyer is a structure (a monorail deck, bridge, or building top)
- **THEN** the flyer MUST come to rest on the structure's top band regardless of the terrain far below

#### Scenario: Landing refused on unlandable or occupied surface
- **WHEN** the `land` tool is invoked and the resting surface is terrain the flyer cannot land on, or the resting cell is occupied by another character, or the column is open with nothing below
- **THEN** the tool MUST refuse and the flyer MUST remain airborne

#### Scenario: Landing refused when CanLand is false
- **WHEN** the `land` tool is invoked and `Flight.CanLand` is false
- **THEN** the tool MUST refuse and the flyer MUST remain airborne

#### Scenario: Takeoff rises above the resting surface
- **WHEN** the `takeoff` tool is invoked on a `Landed` flyer
- **THEN** the flyer MUST transition `Landed → TakingOff → Airborne` and rise to the first clear band above the surface it was resting on — the cruise band when that lies above the surface, otherwise just clear of it, clamped to `[MinBand, MaxBand]`
- **AND** takeoff MUST be refused when the flyer cannot ascend clear of the surface within its ceiling
- **AND** ground obstruction MUST no longer apply once airborne

### Requirement: Flyer Interaction
The system SHALL surface type-specific, altitude-aware affordances for flyers, so the interactions available depend on the flyer's type and on the observer's band relative to the flyer.

#### Scenario: Hack an orbital satellite at range
- **WHEN** a player targets a satellite in an orbit band via a `hack` interaction
- **THEN** the interaction MUST resolve using a line-of-sight/uplink check rather than physical adjacency
- **AND** MUST NOT require the player to be in the satellite's band

#### Scenario: Summon or hail an air taxi
- **WHEN** a player invokes `summon`/`hail` on an air taxi
- **THEN** the taxi MUST generate an AdHoc flight plan to the caller and proceed to land for boarding

#### Scenario: Attack a low-air drone
- **WHEN** a grounded player targets a drone in a low-air band within range
- **THEN** an `attack`/`shoot` affordance MUST be available

#### Scenario: Affordances respect altitude
- **WHEN** a flyer is only reachable in a high band such as orbit
- **THEN** physical affordances (attack, board) MUST be withheld
- **AND** ranged/uplink affordances such as `hack` MUST remain available
