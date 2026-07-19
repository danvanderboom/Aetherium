## ADDED Requirements

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
