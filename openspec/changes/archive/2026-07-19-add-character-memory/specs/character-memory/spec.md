## ADDED Requirements

### Requirement: Perception-Time Memory Recording
The engine SHALL record what a character perceives into that character's `Memory` component at perception time, so that a character accumulates knowledge of terrain and entities they have actually seen.

#### Scenario: Record visible terrain and entities
- **WHEN** a session's perception is computed and memory is enabled for the world
- **THEN** each visible tile's terrain SHALL be recorded as a memory at its absolute location
- **AND** each visible non-terrain entity (other than the perceiving character) SHALL be recorded with its type and entity id

#### Scenario: Reinforcement on re-encounter
- **WHEN** a character perceives content identical to an existing memory at the same location
- **THEN** the existing memory's impression count SHALL increase and its last-seen time SHALL update
- **AND** no duplicate memory row SHALL be created

#### Scenario: Only perceived content is recorded
- **WHEN** a tile or entity is outside the character's computed perception
- **THEN** no memory of it SHALL be recorded

### Requirement: Memory Decay and Caps
Character memory SHALL decay lazily over time and SHALL be bounded per character.

#### Scenario: Lazy strength decay
- **WHEN** a memory is read with a positive decay half-life configured
- **THEN** its effective strength SHALL be its stored strength halved once per elapsed half-life
- **AND** stored state SHALL NOT be mutated by reads

#### Scenario: Decay disabled
- **WHEN** the decay half-life is zero or negative
- **THEN** effective strength SHALL equal stored strength

#### Scenario: Location cap enforcement
- **WHEN** recording pushes a character's tracked locations above the configured maximum
- **THEN** the oldest locations (by most recent activity) SHALL be pruned until within the cap

### Requirement: Per-World Memory Configuration
Memory behavior SHALL be per-world data, not hardcoded engine behavior.

#### Scenario: Defaults
- **WHEN** a world is created without memory parameters
- **THEN** memory SHALL be enabled with default caps and decay

#### Scenario: Overrides via world generation parameters
- **WHEN** a world is created with `MemoryEnabled`, `MemoryMaxLocations`, or `MemoryDecayHalfLifeSeconds` generator parameters
- **THEN** the world's memory policy SHALL reflect those values
- **AND** a world with `MemoryEnabled=false` SHALL record no memories
