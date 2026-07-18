## ADDED Requirements

### Requirement: Memory Inspection Command
`aetherctl` SHALL provide a `memory get` command that displays a character's accumulated memories.

#### Scenario: Get memories for a session
- **WHEN** the operator runs `aetherctl memory get <sessionId>`
- **THEN** the CLI SHALL retrieve the character's memories from the server
- **AND** SHALL display a summary (locations tracked, memory count) and entries, emitting the full memory object when `--json` is set

#### Scenario: Unknown session or gated access
- **WHEN** the session does not exist or operator access is disabled on the server
- **THEN** the CLI SHALL display an error and exit with a non-zero status code
