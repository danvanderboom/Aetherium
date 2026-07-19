# character-memory Specification

## Purpose
TBD - created by archiving change add-memory-dynamics. Update Purpose after archive.
## Requirements
### Requirement: Memory Stability and Reinforcement
When memory dynamics are enabled for a world, each memory SHALL carry its own stability (personal half-life), and spaced re-encounters SHALL grow that stability so frequently revisited content decays more slowly over time.

#### Scenario: Spaced re-encounter grows stability
- **WHEN** a character re-perceives remembered content after at least the configured minimum reinforcement interval
- **THEN** the memory's stability SHALL multiply by the configured growth factor
- **AND** its strength SHALL refresh to full

#### Scenario: Massed re-encounter does not grow stability
- **WHEN** a character re-perceives remembered content before the minimum reinforcement interval has elapsed
- **THEN** the memory's impressions and last-seen time SHALL update as before
- **AND** its stability SHALL NOT change

#### Scenario: Stability fallback for legacy entries
- **WHEN** a memory with no stability recorded (stability = 0) is read
- **THEN** its effective strength SHALL be computed against the world's decay half-life scaled by the character's profile multiplier

### Requirement: Memory Permanence Through Familiarity
A memory whose stability reaches the configured permanence threshold SHALL become permanent and never decay thereafter.

#### Scenario: Permanence latch
- **WHEN** reinforcement raises a memory's stability to or above the permanence threshold
- **THEN** the memory SHALL be marked permanent

#### Scenario: Permanent memories do not decay or cull
- **WHEN** a permanent memory is read at any later time
- **THEN** its effective strength SHALL equal its stored strength
- **AND** it SHALL never be removed by strength-based culling

### Requirement: Forgetting Weak Memories
When memory dynamics are enabled, memories whose effective strength has fallen below the configured forget threshold SHALL be removed at write time; reads SHALL never mutate stored state.

#### Scenario: Write-time cull at touched locations
- **WHEN** recording touches a location holding entries below the forget threshold
- **THEN** those entries SHALL be removed

#### Scenario: Culling disabled
- **WHEN** the forget threshold is zero
- **THEN** no strength-based removal SHALL occur

### Requirement: Per-Character Memory Profiles
Memory quality SHALL be configurable per character via a `MemoryProfile` component overriding world defaults; a character without the component SHALL use world defaults.

#### Scenario: Forgetful and sharp characters
- **WHEN** two characters with different half-life multipliers perceive the same content at the same time
- **THEN** the character with the smaller multiplier SHALL show lower effective strength for the same memory at any later read

#### Scenario: Profile overrides caps and growth
- **WHEN** a character's profile sets a location-cap override or a stability-growth multiplier
- **THEN** those values SHALL apply to that character in place of the world defaults

### Requirement: Memory Dynamics Opt-In
Memory dynamics SHALL be disabled by default and enabled per world via generator parameters; a world without dynamics SHALL behave exactly as before this change.

#### Scenario: Default off preserves legacy behavior
- **WHEN** a world is created without `MemoryDynamicsEnabled`
- **THEN** no stability SHALL be written, no permanence latched, and no culling performed
- **AND** decay SHALL follow the world half-life exactly as before

#### Scenario: Dynamics parameters
- **WHEN** a world is created with `MemoryDynamicsEnabled=true` and any of `MemoryStabilityGrowthFactor`, `MemoryMinReinforcementIntervalSeconds`, `MemoryPermanenceThresholdSeconds`, `MemoryForgetThreshold`
- **THEN** the world's memory policy SHALL reflect those values

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

