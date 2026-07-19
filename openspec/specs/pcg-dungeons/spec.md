# pcg-dungeons Specification

## Purpose
TBD - created by archiving change expand-pcg-pipeline. Update Purpose after archive.
## Requirements
### Requirement: Varied Room Geometry and Corridors
Dungeon layouts SHALL include varied room shapes (rect, L, T, circular-like), corridor widths, and spacing.

#### Scenario: Shape diversity threshold
- WHEN generating a dungeon
- THEN at least two distinct room shape families appear

### Requirement: Branching and Loops
Dungeons MUST include branches and intentional loops for alternative routes.

#### Scenario: Minimum loop ratio
- WHEN validation runs
- THEN loop ratio meets configured minimum (e.g., ≥10%)

### Requirement: Multi-Level Dungeons
Dungeons SHALL span multiple levels with vertical connectors and consistent difficulty ramp.

#### Scenario: Cross-level path
- WHEN a multi-level dungeon is generated
- THEN there exists at least one traversable path connecting start to boss across levels

### Requirement: Secrets and Hidden Connectivity
Secret rooms/passages SHALL be placed with discoverability cues and optional rewards.

#### Scenario: Discoverable secret
- WHEN secrets are placed
- THEN at least one has a soft cue within N tiles (e.g., cracks, odd symmetry)

### Requirement: Gating and Keys
Critical areas MAY be gated; required keys/tools MUST exist and be accessible on the critical path.

#### Scenario: Access proof
- WHEN a gated door blocks progress
- THEN validation proves a path from start → key → gate → objective

### Requirement: Difficulty-Parameterized Generation
Dungeon generation SHALL honor difficulty parameters supplied on the generation request (`GeneratorContext.GeneratorParams`), so that curriculum stages and benchmark scenarios measurably change the generated world. When a parameter is absent or invalid, generation SHALL fall back to its default value and preserve the default RNG draw order, so that a request with no parameters produces byte-for-byte identical output to prior behavior.

#### Scenario: Room count follows parameters
- **WHEN** a request supplies `minRooms` and `maxRooms`
- **THEN** the per-level room count is drawn from that range
- **AND** a request with neither parameter produces the same room count distribution as before (default 6–10)

#### Scenario: Key/lock chain depth follows parameters
- **WHEN** a request supplies `keyLockChainDepth` = N (N ≥ 1)
- **THEN** the layout gates the critical path with N key/lock pairs, each key reachable before its lock
- **AND** a request with no `keyLockChainDepth` gates exactly one pair (the prior default)

#### Scenario: Trap and secret density follow parameters
- **WHEN** a request supplies `trapDensity` and/or `secretRoomDensity`
- **THEN** the number of traps scales with `trapDensity` and walkable area, and secret rooms scale with `secretRoomDensity`
- **AND** absent parameters reproduce the prior single-trap / default-secret behavior

#### Scenario: Population density follows parameters
- **WHEN** a request supplies `enemyCount` and/or `resourceAvailability`
- **THEN** the population pass places that many monsters and treasure items (scaled to available walkable locations)
- **AND** absent parameters reproduce the prior area-ratio monster count and default treasure count

#### Scenario: Determinism preserved under parameters
- **WHEN** two requests share the same seed, generator version, and parameters
- **THEN** they produce identical worlds (same world hash)

