# pcg-narrative Specification

## Purpose
TBD - created by archiving change expand-pcg-pipeline. Update Purpose after archive.
## Requirements
### Requirement: Narrative Constraint Intake
The system SHALL accept narrative constraint inputs (required locations/items/themes) prior to generation.

#### Scenario: Constraints provided
- WHEN quests specify required POIs/items
- THEN generation reserves space and resources for them

### Requirement: Critical Placement Guarantees
Narrative-critical objects and locations MUST be placed with access proofs and difficulty pacing.

#### Scenario: Guaranteed quest target
- WHEN a quest target is required
- THEN it is placed and reachable within the intended progression

### Requirement: Thematic Cohesion
The system SHALL bias tiles, props, and encounters towards the active narrative theme.

#### Scenario: Thematic bias applied
- WHEN a theme is active
- THEN content frequency skews within configured bounds

### Requirement: Narrative Tokens API
The system SHALL support a Narrative Tokens API enabling narratives to request “tokens” (e.g., “locked shrine”, “ruined tower near river”) that map to prefab + constraints.

#### Scenario: Token satisfied
- WHEN a narrative requests a token
- THEN generation places a matching structure with constraints enforced

