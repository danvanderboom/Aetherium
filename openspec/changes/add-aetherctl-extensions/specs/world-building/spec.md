## ADDED Requirements
### Requirement: Agent/Session Management CLI
The system SHALL provide `aetherctl` commands to manage agent sessions when the server exposes the corresponding APIs.

#### Scenario: List sessions
- **WHEN** the operator runs `aetherctl session list`
- **THEN** the CLI returns the active sessions in JSON when `--json` is set

#### Scenario: Create session
- **WHEN** the operator runs `aetherctl session create`
- **THEN** a new session is created and the session id is returned

#### Scenario: Close session
- **WHEN** the operator runs `aetherctl session close <sessionId>`
- **THEN** the session is terminated and the CLI exits zero on success

