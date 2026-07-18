## ADDED Requirements

### Requirement: Recognition Inspection Command
aetherctl SHALL provide `recognition get <worldId> <entityId> [--json]` to display a character's known individuals via the management grain's recognition read.

#### Scenario: Inspect a character's social memory
- **WHEN** the command is run for a character with recognition state
- **THEN** it SHALL display each known individual with kind, encounter count, effective familiarity, and last-seen time
- **AND** `--json` SHALL emit the raw DTO

#### Scenario: Missing state or gate
- **WHEN** the grain returns null (unknown world/entity or operator access disabled)
- **THEN** the command SHALL report the failure and set a non-zero process exit code
