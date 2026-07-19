## ADDED Requirements

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
