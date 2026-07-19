## ADDED Requirements

### Requirement: Character Memory Retrieval
The grain SHALL expose a character's accumulated memories to authorized operators as JSON.

#### Scenario: Retrieve memories for a session
- **WHEN** `GetMemoryAsync` is called with a valid `sessionId`
- **THEN** the grain SHALL return the character's memories as JSON, each entry carrying absolute location, content type, content, stored strength, effective (decayed) strength, impression count, and last-seen time

#### Scenario: Unknown session
- **WHEN** `GetMemoryAsync` is called with an unknown `sessionId`
- **THEN** the grain SHALL return null and SHALL NOT throw

#### Scenario: Operator gating
- **WHEN** operator access is disabled
- **THEN** `GetMemoryAsync` SHALL be denied (memories carry absolute world coordinates and are a god-view read)
