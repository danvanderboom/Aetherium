## ADDED Requirements

### Requirement: Recognition Memory Retrieval
The management grain SHALL expose an operator-gated read of a character's individual-recognition memory by world and entity id, resolving the canonical world, so both player characters and NPCs can be inspected.

#### Scenario: Read a character's known individuals
- **WHEN** `GetRecognitionAsync(worldId, entityId)` is called for a character with recognition state and operator access is enabled
- **THEN** it SHALL return JSON listing each known individual with kind, first-met, last-seen, encounter count, stored and effective familiarity, stability, and permanence

#### Scenario: Operator gate
- **WHEN** operator access is disabled
- **THEN** `GetRecognitionAsync` SHALL return null

#### Scenario: Unknown world or entity
- **WHEN** the world cannot be resolved or the entity is not present
- **THEN** `GetRecognitionAsync` SHALL return null
