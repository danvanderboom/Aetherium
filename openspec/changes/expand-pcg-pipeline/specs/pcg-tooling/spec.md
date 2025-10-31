## ADDED Requirements

### Requirement: Seed CLI and Repro Recipes
A CLI SHALL allow specifying seed, generator, and parameters to reproduce worlds and export artifacts.

#### Scenario: Repro via CLI
- WHEN running the CLI with a seed
- THEN the same map and metrics are produced and saved

### Requirement: Visual Debug Overlays
Developers SHALL be able to enable overlays for generation phases (rooms, corridors, biomes, roads, secrets, keys/locks).

#### Scenario: Phase overlays toggled
- WHEN overlays are enabled
- THEN each phase’s artifacts can be displayed independently

### Requirement: Metrics Export
The system MUST export metrics to logs and optional JSON for automated analysis.

#### Scenario: JSON metrics emitted
- WHEN generation completes
- THEN a JSON metrics file is written


