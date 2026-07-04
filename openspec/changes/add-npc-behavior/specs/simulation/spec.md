## ADDED Requirements

### Requirement: NPC Wander Behavior on the World Tick
Monsters placed by world generation SHALL act on the server tick pipeline: on each eligible tick the map SHALL advance every monster by at most one validated cardinal step and broadcast the move so co-located players perceive it. Movement SHALL be gated by configuration and SHALL never move a monster through a wall, off the map, or onto an occupied cell.

#### Scenario: Monster steps on a tick
- **WHEN** `GameMapGrain.TickAsync` runs and NPC behavior is enabled
- **AND** the current tick falls on the configured `NpcMoveIntervalTicks` boundary
- **THEN** each `Monster` on the map SHALL choose a cardinal direction and attempt a one-cell move via the validated `World.TryMoveSteps`
- **AND** for every move that lands, the grain SHALL fan out an `EntityMovedDelta` so every session bound to the map receives a fresh perception

#### Scenario: Boxed-in or blocked monster stays put
- **WHEN** a monster has no passable cardinal neighbour, or its chosen step is blocked by a wall, map edge, or another character
- **THEN** the monster SHALL remain in place for that tick without error
- **AND** no `EntityMovedDelta` SHALL be emitted for it

#### Scenario: NPC behavior can be disabled or paced
- **WHEN** `SimulationOptions.EnableNpcBehavior` is false
- **THEN** no monster SHALL move on any tick
- **WHEN** `SimulationOptions.NpcMoveIntervalTicks` is N (N ≥ 1)
- **THEN** monsters SHALL step at most once every N ticks, independent of `TickHz`

#### Scenario: Movement is serialized and lays heat
- **WHEN** monster movement runs during a tick
- **THEN** the world mutations SHALL be applied on the grain's activation turn (serialized with player moves on the same world)
- **AND** each landed move SHALL record a heat trail via the existing world-event subscriber, so infrared perceives moving monsters
