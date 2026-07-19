## 1. CLI
- [x] 1.1 Add `OrleansClientFactory.GetAgentTelemetry(agentId)`
- [x] 1.2 Add `Aetherctl/Commands/TelemetryCommands.cs`: `snapshots`, `analysis`, `replays`, `replay`, `clear`
- [x] 1.3 Register in `Program.cs`; non-zero exit + clear errors on missing data

## 2. Tests (linked to spec requirements)
- [x] 2.1 CLI: structural coverage for all five subcommands
- [x] 2.2 Server anchor: record a snapshot + replay via the grain and read both back (the retrieval path the CLI uses)

## 3. Docs
- [x] 3.1 Update `docs/agents/README.md` + `TOOL_SYSTEM_STATUS.md`
