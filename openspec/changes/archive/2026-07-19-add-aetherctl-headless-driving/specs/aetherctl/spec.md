## ADDED Requirements

### Requirement: Headless Session Creation Command
`aetherctl` SHALL provide a `session create` command that provisions a headless session in a world via the server and returns the new session id.

#### Scenario: Create a session in a world
- **WHEN** the operator runs `aetherctl session create --world <worldId>`
- **THEN** the CLI SHALL call the server's headless-session provisioning API
- **AND** SHALL print the new `sessionId`
- **AND** SHALL emit `{ "success": true, "sessionId": "..." }` when `--json` is set

#### Scenario: Create a session at an explicit location and profile
- **WHEN** the operator runs `aetherctl session create --world <worldId> --at <x>,<y>,<z> --profile <profile>`
- **THEN** the CLI SHALL pass the start location and profile to the server
- **AND** SHALL return the new `sessionId`

#### Scenario: Unknown or missing world
- **WHEN** the operator runs `aetherctl session create` with an unknown or missing `--world`
- **THEN** the CLI SHALL display an error
- **AND** SHALL exit with a non-zero status code

#### Scenario: Server without headless support
- **WHEN** the connected server does not support headless session creation
- **THEN** the CLI SHALL display a clear, non-crashing error message
- **AND** SHALL exit with a non-zero status code

### Requirement: Perception Inspection Command
`aetherctl` SHALL provide a `perception get` command that retrieves and displays a session's current perception.

#### Scenario: Get perception for a session
- **WHEN** the operator runs `aetherctl perception get <sessionId>`
- **THEN** the CLI SHALL retrieve the session's perception from the server
- **AND** SHALL display it, emitting the raw perception object when `--json` is set

#### Scenario: Get perception with absolute coordinates
- **WHEN** the operator runs `aetherctl perception get <sessionId> --absolute`
- **THEN** the displayed `PlayerLocation` SHALL contain true world coordinates

#### Scenario: Unknown session
- **WHEN** the operator runs `aetherctl perception get <sessionId>` for a session that does not exist
- **THEN** the CLI SHALL display an error
- **AND** SHALL exit with a non-zero status code

### Requirement: World Inspection Command
`aetherctl` SHALL provide a `world dump` command that displays a world's tiles and entities from the omniscient world snapshot.

#### Scenario: Dump a world
- **WHEN** the operator runs `aetherctl world dump <worldId>`
- **THEN** the CLI SHALL retrieve the world snapshot from the server
- **AND** SHALL display the world's tiles and entities
- **AND** SHALL emit the snapshot as machine-readable output when `--json` is set

#### Scenario: Unknown world
- **WHEN** the operator runs `aetherctl world dump <worldId>` for a world that does not exist
- **THEN** the CLI SHALL display an error
- **AND** SHALL exit with a non-zero status code
