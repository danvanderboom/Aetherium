## ADDED Requirements

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
