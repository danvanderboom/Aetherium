# Map Standards

This document defines the standards for test maps and game maps. These standards are used by the `MapValidator` and should be followed by all map builders.

## Boundary Requirements

### Explicit Boundaries
When `RequireExplicitBoundary` is `true` (default for test maps), all passable terrain must be surrounded by impassable terrain (walls, mountains, etc.) at map edges.

- **Passable terrain types**: Indoors, Upstairs, Downstairs, Road, Plains, Forest, Cave
- **Impassable terrain types**: Wall, Mountain, Water (blocks movement)

### Implicit Boundaries
When `RequireExplicitBoundary` is `false` (for maps like toroidal worlds), boundaries can be implicit - locations simply don't exist beyond the map bounds, and movement attempts will fail.

## Lighting Requirements

### Light Source Requirements
If `RequireLightSource` is `true` (default), maps must contain at least one enabled `LightSource` component at the Z-level being validated.

- Light sources are entities with the `LightSource` component
- The `IsEnabled` property must be `true`
- Light sources should be placed in accessible locations

### Typical Placement
- At player start location
- At center of playable area
- In key areas where visibility is important

## Start Location Requirements

### Basic Requirements
- Must exist in the world (`EntitiesByLocation` contains the location)
- Must be passable terrain (player can stand there)
- Should be within the playable area boundaries

### Reachability
If `MinReachableLocations` is specified, the start location must have at least that many reachable passable locations via BFS (breadth-first search).

This ensures the player isn't trapped or in an isolated area too small for gameplay.

## Terrain Type Registration

All terrain types used in the map must be registered in `World.TerrainTypes`. Common types:

- `Indoors` - Passable, typical interior floor
- `Wall` - Impassable, blocks movement and view
- `Mountain` - Impassable, blocks view
- `Road` - Passable, outdoor path
- `Plains` - Passable, outdoor ground
- `Forest` - Passable, partially blocks view (opacity < 1)
- `Water` - Impassable, blocks movement
- `Cave` - Passable, dark interior
- `Upstairs` - Passable, allows vertical movement up
- `Downstairs` - Passable, allows vertical movement down

## Test Map Standards

Test maps should:
1. Have explicit boundaries (walls surrounding playable area)
2. Include at least one light source at the validated Z-level
3. Have a valid, accessible start location
4. Ensure all terrain types are properly registered
5. Be deterministic (same seed produces same map)

## Game Map Standards

Game maps can be more flexible:
1. May use implicit boundaries for special world types (e.g., toroidal)
2. Should still include light sources for visibility
3. Must have valid start locations for players
4. Should validate reachability to ensure playable gameplay

