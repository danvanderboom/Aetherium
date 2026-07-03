# Aetherium

Aetherium is a server-authoritative multiplayer dungeon crawler built on .NET 10, Microsoft Orleans, and SignalR. The server simulates the entire game world — procedural generation, field-of-view and lighting, entities, interactions, weather and seasons, AI agents, and emergent narrative — and streams each player only what their character can perceive. Clients are thin renderers: a Spectre.Console terminal client, a Unity 2D client, and a planned Unreal Engine client all consume the same perception protocol.

## Project map

| Project | What it is |
|---|---|
| [Aetherium.Server](Aetherium.Server/) | ASP.NET Core game server: Orleans silo, SignalR hubs, game simulation, world generation, AI agents, narrative systems |
| [Aetherium.Model](Aetherium.Model/) | Shared DTO contracts (perception, inventory, tools, management) used by server and all clients |
| [Aetherium.Console](Aetherium.Console/) | Terminal client: Spectre.Console UI, NAudio audio, WebSocket monitoring server |
| [Aetherium.Unity](Aetherium.Unity/) | Unity 2D client (tilemap rendering, Xbox controller support, mock-replay or live SignalR) |
| [Aetherium.Dashboard](Aetherium.Dashboard/) | Blazor Server dashboard for agent telemetry, training progress, PCG, and world management |
| [Aetherctl](Aetherctl/) | Operator CLI (`aetherctl`): sessions, agents, worlds, worldgen, prompts, monitoring |
| [WorldGenCLI](WorldGenCLI/) | Procedural-generation client library used by Aetherctl and the Dashboard |
| [Aetherium.Test](Aetherium.Test/) | Server/engine test suite (xUnit + NUnit, includes Orleans TestingHost integration tests) |
| [Aetherctl.Test](Aetherctl.Test/) | CLI test suite |

## Quick start

```powershell
# Build everything
dotnet build Aetherium.sln

# Run the test suite
dotnet test

# Start server + console client in separate windows (auto-cleans after timeout)
.\start-game-test.ps1 -TimeoutSeconds 20

# If a run is interrupted, clean up processes
.\stop-game.ps1          # uses .game-run-pids.json if present
.\stop-game.ps1 -All     # force-kill Aetherium.Server / Aetherium.Console
```

The server listens on `http://localhost:5000` (SignalR hubs at `/gamehub`, `/managementHub`, `/agentDashboardHub`); the console client exposes a monitoring WebSocket at `ws://localhost:5001/monitor`. See [docs/development.md](docs/development.md) for the full developer workflow.

## Documentation

- **[docs/README.md](docs/README.md)** — documentation index (start here)
- **[docs/architecture/overview.md](docs/architecture/overview.md)** — system architecture and data flow
- **[docs/architecture/server.md](docs/architecture/server.md)** — server subsystems (grains, simulation, perception, worldgen, agents, narrative)
- **[docs/architecture/clients.md](docs/architecture/clients.md)** — console, Unity, dashboard, and planned Unreal clients
- **[docs/architecture/tooling-and-data.md](docs/architecture/tooling-and-data.md)** — CLI tools, scripts, monitoring, data assets
- **[docs/audits/README.md](docs/audits/README.md)** — subsystem audit reports, recommendations, and improvement plan
- **[docs/history/](docs/history/)** — archived point-in-time status documents

## Spec-driven development

Requirements live in [openspec/specs/](openspec/specs/) (what IS built) and change proposals in [openspec/changes/](openspec/changes/) (what SHOULD change). See [openspec/AGENTS.md](openspec/AGENTS.md) for the workflow and [openspec/project.md](openspec/project.md) for project conventions.
