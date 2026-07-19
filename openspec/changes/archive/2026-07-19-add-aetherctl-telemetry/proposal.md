## Why
`AgentTelemetryGrain` already records per-step performance snapshots, aggregated analysis, and failed-run replays for every agent, but the only consumer is the Blazor Dashboard — `aetherctl` cannot read any of it. Surfacing telemetry in the CLI turns "an agent got stuck" into an inspectable artifact from the same terminal that drives the agent, closing the debug loop (drive → observe → diagnose).

## What Changes
- Add `OrleansClientFactory.GetAgentTelemetry(agentId)` in the CLI.
- Add `aetherctl telemetry` commands over the existing grain API (no server changes):
  - `telemetry snapshots <agentId> [--limit N] [--json]` — recent per-step snapshots
  - `telemetry analysis <agentId> [--json]` — aggregated performance analysis
  - `telemetry replays <agentId> [--limit N] [--json]` — failed-run replay ids
  - `telemetry replay <agentId> <replayId> [--json]` — a stored replay (JSON)
  - `telemetry clear <agentId>` — clear an agent's telemetry

## Impact
- Affected specs: `aetherctl` (ADDED: Telemetry Inspection Commands)
- Affected code: `Aetherctl/Orleans/OrleansClientFactory.cs`, new `Aetherctl/Commands/TelemetryCommands.cs`, `Aetherctl/Program.cs`, tests in `Aetherctl.Test` (+ a grain round-trip anchor test in `Aetherium.Test`)
- No server-side changes: `IAgentTelemetryGrain` already exposes everything needed.
- Non-Goals: replay *playback* (driving a character from a replay), new telemetry capture, dashboard changes.
