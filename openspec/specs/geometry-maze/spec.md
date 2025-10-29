## Purpose
Specifies grid coloring behavior and the maze generation algorithm over a colored grid.

## Requirements

### Requirement: GridColoring Periodic Pattern
The system SHALL map an infinite grid to colors based on a repeating 2D array.

#### Scenario: Color lookup repeats
- WHEN `GetColor(x, y)` is called for any integer coordinates
- THEN it MUST return the color from the pattern at indices `(y % H, x % W)` using absolute values

### Requirement: Connected Cells by Color
The system SHALL return all 4-connected cells (N/E/S/W) of the same color from a seed.

#### Scenario: Connected component discovery
- WHEN `GetConnectedCells(x, y)` is called
- THEN it MUST return all cells reachable via 4-directional steps that share the same color as `(x, y)`

### Requirement: Maze Generation on Colored Grid
The system SHALL build a maze by treating colored regions as rooms, walls, or pillars.

#### Scenario: Room/wall/pillar classification
- WHEN `MazeGenerator` is constructed with a color mapping
- THEN locations mapped to Room MUST be added to Rooms
- AND locations mapped to Wall MUST be expanded to include connected cells as Walls
- AND all other locations MUST be Pillars

#### Scenario: Initial carve and placement
- WHEN `Build()` is invoked
- THEN all Walls MUST be set via SetWall
- AND all Rooms MUST be set via SetRoom
- AND all Pillars MUST be set via SetPillar

#### Scenario: Stepwise connection
- WHEN `BuildNext()` is invoked repeatedly
- THEN it MUST select unvisited neighbor rooms and call RemoveWall for the separating wall cells
- UNTIL no unvisited rooms remain, then return false


