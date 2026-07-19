## ADDED Requirements

### Requirement: Individual Recognition Memory
Characters SHALL maintain a per-individual memory of other characters they have encountered — first met, last seen, encounter count, and a familiarity value that reinforces on spaced re-meetings and decays between them on the shared memory-stability curve.

#### Scenario: First meeting records the individual
- **WHEN** a character with recognition active comes within recognition range of a character they have never met
- **THEN** the individual SHALL be recorded with the configured meet strength and initial familiarity stability

#### Scenario: Spaced re-meetings reinforce
- **WHEN** the pair meets again after the minimum reinforcement interval
- **THEN** familiarity stability SHALL grow by the configured factor and strength SHALL refresh
- **AND** continuous contact SHALL NOT compound stability

#### Scenario: Familiarity fades and can become permanent
- **WHEN** a known individual has not been seen for a long time relative to their familiarity stability
- **THEN** effective familiarity SHALL decay on the shared curve
- **AND** an individual whose familiarity stability reaches the permanence threshold SHALL never fade

#### Scenario: Individual cap
- **WHEN** recording a new individual would exceed the configured maximum
- **THEN** the individual with the lowest effective familiarity SHALL be pruned first

### Requirement: Kind-Dependent Recognition Acuity
Recognition SHALL be determined deterministically as acuity × effective familiarity ≥ the configured threshold, where acuity depends on the recognizer's relationship to the target's kind: own kind (high default), other kinds (low default), with optional per-kind overrides.

#### Scenario: Good with own kind
- **WHEN** a character meets an individual of their own kind for the second time at default configuration
- **THEN** recognition SHALL succeed

#### Scenario: Poor with other kinds
- **WHEN** a character meets an individual of a different kind for the second time at default configuration
- **THEN** recognition SHALL NOT succeed until repeated meetings raise familiarity above the threshold

#### Scenario: Per-kind override
- **WHEN** a character's recognition profile sets an acuity override for a specific kind
- **THEN** that acuity SHALL be used for targets of that kind in place of the own/other-kind defaults

### Requirement: Recognition Proximity Sweep
Recognition SHALL be evaluated on the canonical world during the map grain tick for all characters with recognition active — player characters and NPCs alike — using topology distance within the configured range on the same z-level.

#### Scenario: PCs and NPCs both participate
- **WHEN** a player character's canonical body and an NPC are within recognition range at a tick
- **THEN** each side with recognition active SHALL update its own individual memory of the other

#### Scenario: Distance via topology
- **WHEN** proximity is evaluated
- **THEN** distance SHALL be computed through the world's topology, not raw coordinate deltas

#### Scenario: Disabled worlds do no work
- **WHEN** the world's recognition policy is disabled
- **THEN** the sweep SHALL perform no per-character iteration and no state SHALL change

### Requirement: Encounter-Gated Recognition Events
A recognition event SHALL fire at most once per pair per encounter; an encounter ends when the pair has been apart longer than the configured encounter timeout.

#### Scenario: No re-fire during continuous contact
- **WHEN** two characters remain within range across consecutive ticks after an event fired
- **THEN** no further event SHALL fire for that pair

#### Scenario: New encounter after separation
- **WHEN** the pair separates for longer than the encounter timeout and comes back into range
- **THEN** a new event SHALL fire

### Requirement: Per-World Recognition Configuration
Recognition SHALL be disabled by default and configured per world via generator parameters, with per-character `RecognitionProfile` component overrides.

#### Scenario: Opt-in default off
- **WHEN** a world is created without `RecognitionEnabled`
- **THEN** no recognition state SHALL be recorded and no recognition events SHALL fire

#### Scenario: World parameters
- **WHEN** a world is created with `RecognitionEnabled=true` and any of range, acuity, threshold, encounter-timeout, familiarity half-life, meet-strength, or max-individuals parameters
- **THEN** the world's recognition policy SHALL reflect those values

#### Scenario: Profile overrides
- **WHEN** a character carries a `RecognitionProfile`
- **THEN** its enabled flag, range, and acuity values SHALL override the world defaults for that character

### Requirement: Runtime Profile Configuration
A `configurecharacter` worldbuilding tool SHALL set memory and recognition profile fields on an entity by id at runtime, creating the profile components when absent.

#### Scenario: Configure an NPC live
- **WHEN** the tool is executed with an entity id and profile fields via the world-tool execution path
- **THEN** the entity's profiles SHALL reflect the new values for all subsequent memory and recognition processing

#### Scenario: Unknown entity
- **WHEN** the tool is executed with an entity id not present in the world
- **THEN** it SHALL fail with an error naming the entity id
