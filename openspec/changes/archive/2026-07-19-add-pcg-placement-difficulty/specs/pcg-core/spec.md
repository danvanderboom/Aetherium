## MODIFIED Requirements

### Requirement: Metrics and Introspection
The system MUST expose generation metrics (branching factor, loop rate, dead-end count, path-length distribution, biome coverage, validation pass/fail reasons). Generation metrics SHALL also include a computed difficulty profile derived from the effective generation parameters and the measured layout, so the difficulty implied by a curriculum stage or benchmark is observable after generation rather than being purely advisory input.

#### Scenario: Metrics emitted per run
- WHEN a world is generated
- THEN metrics are available to validators and logs

#### Scenario: Difficulty profile computed per run
- **WHEN** a world is generated (with or without explicit difficulty parameters)
- **THEN** `GenerationMetrics` exposes a `DifficultyProfile` whose components reflect the effective parameters (room/branching, key-lock chain depth, trap/enemy density, resource availability) and the measured layout
- **AND** the profile's difficulty score and predicted success rate are populated (not left at defaults)
