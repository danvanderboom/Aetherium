# pcg-core Specification

## Purpose
TBD - created by archiving change expand-pcg-pipeline. Update Purpose after archive.
## Requirements
### Requirement: Deterministic Generation with Seed and Version
The system SHALL produce identical worlds for the same (seed, generator-version) across runs and machines.

#### Scenario: Stable replay across runs
- WHEN the same seed and generator version are used
- THEN the generated world is identical byte-for-byte

#### Scenario: Version bump changes output
- WHEN generator version changes with same seed
- THEN the output may differ and is tagged with the new version

### Requirement: Phased Pipeline Orchestration
The system SHALL execute generation in phases: layout → theming → population → interactions → validation.

#### Scenario: Phase ordering enforced
- WHEN generation runs
- THEN phases execute in defined order and pass artifacts forward

### Requirement: RNG Namespaces
The system MUST provide RNG namespaces per phase/module to prevent cross-phase randomness coupling.

#### Scenario: Independent randomness streams
- WHEN reordering non-dependent modules
- THEN outputs of other modules remain stable

### Requirement: Multi-Level Support
The system SHALL support multiple levels (z-index) and vertical connectors (stairs, ladders).

#### Scenario: Vertical connectivity graph available
- WHEN levels exist
- THEN connectors are recorded in a navigable graph

### Requirement: Metrics and Introspection
The system MUST expose generation metrics (branching factor, loop rate, dead-end count, path-length distribution, biome coverage, validation pass/fail reasons). Generation metrics SHALL also include a computed difficulty profile derived from the effective generation parameters and the measured layout, so the difficulty implied by a curriculum stage or benchmark is observable after generation rather than being purely advisory input.

#### Scenario: Metrics emitted per run
- WHEN a world is generated
- THEN metrics are available to validators and logs

#### Scenario: Difficulty profile computed per run
- **WHEN** a world is generated (with or without explicit difficulty parameters)
- **THEN** `GenerationMetrics` exposes a `DifficultyProfile` whose components reflect the effective parameters (room/branching, key-lock chain depth, trap/enemy density, resource availability) and the measured layout
- **AND** the profile's difficulty score and predicted success rate are populated (not left at defaults)

### Requirement: Audio Zone Generation Pass
The system SHALL include an AudioGenerationPass in the Adaptation phase that analyzes terrain and creates audio zones with biome mappings, reverb presets, and occlusion values.

#### Scenario: Audio zones created during generation
- WHEN world generation completes
- THEN audio zones are stored in SharedData for use by perception system

