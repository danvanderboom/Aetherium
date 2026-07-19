## ADDED Requirements

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
