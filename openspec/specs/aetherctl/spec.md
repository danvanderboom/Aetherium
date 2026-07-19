# aetherctl Specification

## Purpose
TBD - created by archiving change add-identity-recognition. Update Purpose after archive.
## Requirements
### Requirement: Recognition Inspection Command
aetherctl SHALL provide `recognition get <worldId> <entityId> [--json]` to display a character's known individuals via the management grain's recognition read.

#### Scenario: Inspect a character's social memory
- **WHEN** the command is run for a character with recognition state
- **THEN** it SHALL display each known individual with kind, encounter count, effective familiarity, and last-seen time
- **AND** `--json` SHALL emit the raw DTO

#### Scenario: Missing state or gate
- **WHEN** the grain returns null (unknown world/entity or operator access disabled)
- **THEN** the command SHALL report the failure and set a non-zero process exit code

### Requirement: Memory Inspection Command
`aetherctl` SHALL provide a `memory get` command that displays a character's accumulated memories.

#### Scenario: Get memories for a session
- **WHEN** the operator runs `aetherctl memory get <sessionId>`
- **THEN** the CLI SHALL retrieve the character's memories from the server
- **AND** SHALL display a summary (locations tracked, memory count) and entries, emitting the full memory object when `--json` is set

#### Scenario: Unknown session or gated access
- **WHEN** the session does not exist or operator access is disabled on the server
- **THEN** the CLI SHALL display an error and exit with a non-zero status code

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

### Requirement: Scripted Action Command
`aetherctl` SHALL provide an `agent script` command that sends an ordered list of actions from a JSON file to one session and reports the per-step results.

#### Scenario: Run an action script against a session
- **WHEN** the operator runs `aetherctl agent script <sessionId> --file <actions.json>`
- **THEN** the CLI SHALL parse the JSON action list (each entry a tool id and arguments)
- **AND** SHALL execute the actions in order against the session via the server batch API
- **AND** SHALL display the result of each step, emitting full JSON when `--json` is set

#### Scenario: Stop on error flag
- **WHEN** the operator runs `aetherctl agent script <sessionId> --file <actions.json> --stop-on-error`
- **THEN** the CLI SHALL request that execution halt at the first failing step

#### Scenario: Non-zero exit on failure
- **WHEN** any step in the script fails
- **THEN** the CLI SHALL exit with a non-zero status code

#### Scenario: Missing or invalid file
- **WHEN** the action file does not exist or is not a valid JSON action list
- **THEN** the CLI SHALL display an error and exit with a non-zero status code

### Requirement: Multi-Character Scenario Command
`aetherctl` SHALL provide a `scenario run` command that drives one or more characters, each with its own action script, from a single scenario file.

#### Scenario: Drive multiple characters
- **WHEN** the operator runs `aetherctl scenario run <scenario.json>`
- **THEN** the CLI SHALL, for each character entry, resolve or create its session and run that character's action batch
- **AND** SHALL report the results for every character

#### Scenario: Create sessions from the scenario
- **WHEN** a character entry names a world and start location instead of an existing session id
- **THEN** the CLI SHALL create a headless session for that character before running its actions

#### Scenario: Concurrent fan-out
- **WHEN** the operator runs `aetherctl scenario run <scenario.json> --concurrent`
- **THEN** the CLI SHALL drive the characters' batches concurrently rather than one after another

#### Scenario: Invalid scenario file
- **WHEN** the scenario file does not exist or is not a valid scenario document
- **THEN** the CLI SHALL display an error and exit with a non-zero status code

### Requirement: World Edit Commands
`aetherctl` SHALL provide commands to modify a running world: a generic world-tool executor and a spawn convenience command.

#### Scenario: Execute a world tool generically
- **WHEN** the operator runs `aetherctl world edit <worldId> <toolId> --args '<json>'`
- **THEN** the CLI SHALL execute that world-building tool against the world via the server
- **AND** SHALL display the result (JSON with `--json`)

#### Scenario: Spawn a creature
- **WHEN** the operator runs `aetherctl world spawn <worldId> --type <creatureType> --at <x,y,z>`
- **THEN** the CLI SHALL invoke the `spawnentity` tool with those arguments
- **AND** SHALL display the new entity id on success

#### Scenario: Failure reporting
- **WHEN** the server reports a failure (unknown world/tool, invalid location, disallowed tool)
- **THEN** the CLI SHALL display the error and exit with a non-zero status code

### Requirement: Telemetry Inspection Commands
`aetherctl` SHALL expose agent telemetry — per-step snapshots, aggregated analysis, and failed-run replays — from the existing telemetry grain.

#### Scenario: List recent snapshots
- **WHEN** the operator runs `aetherctl telemetry snapshots <agentId> [--limit N]`
- **THEN** the CLI SHALL display the agent's recent per-step performance snapshots (step, action, success, latency), emitting JSON with `--json`

#### Scenario: Show aggregated analysis
- **WHEN** the operator runs `aetherctl telemetry analysis <agentId>`
- **THEN** the CLI SHALL display the agent's aggregated analysis (total steps, success rate, average latency, weaknesses, recommendations)

#### Scenario: List and fetch failed-run replays
- **WHEN** the operator runs `aetherctl telemetry replays <agentId>`
- **THEN** the CLI SHALL list the stored failed-run replay ids
- **WHEN** the operator runs `aetherctl telemetry replay <agentId> <replayId>`
- **THEN** the CLI SHALL emit the stored replay JSON

#### Scenario: Clear telemetry
- **WHEN** the operator runs `aetherctl telemetry clear <agentId>`
- **THEN** the agent's telemetry data SHALL be cleared

#### Scenario: No data
- **WHEN** an agent has no telemetry or a replay id does not exist
- **THEN** the CLI SHALL report that clearly and exit non-zero for a missing replay

