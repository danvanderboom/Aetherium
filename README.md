# Aetherium

Aetherium is a server-authoritative multiplayer simulation engine built on .NET 10, Microsoft Orleans, and SignalR. The server simulates an entire game world — procedural generation, field-of-view and lighting, entities, interactions, weather and seasons, AI agents, and emergent narrative — and streams each player only what their character can perceive. Clients are thin renderers: a Spectre.Console terminal client (the reference renderer), a Unity 2D client, and a planned Unreal Engine client all consume the same semantic perception protocol. The first game built on it is a multiplayer dungeon crawler; the engine itself is designed to be a substrate for many.

## Vision & goals

Three constraints shape the engine's design. They are the north star for every subsystem — see the [engine gap-analysis & roadmap](docs/audits/2026-07-06-engine-gap-analysis/design-next-steps.md) for the full argument, and the [audit index](docs/audits/README.md) for where the codebase stands against them today.

- **Render-agnostic.** The ASCII/console client is the *reference* renderer, not the target. The server, model, and protocol must never assume glyphs, fonts, colors, or character grids — every perception payload is semantic (entity kind, state, orientation, material, lighting, animation cue) so a terminal, a 2D tilemap, or a 3D isometric client can each bind it to its own asset pack.
- **Continuous, speed-based simulation — not alternating turns.** There is no "your turn / my turn." Each actor has an independent action budget that refills at its own speed; idle players never pause the world, and monsters keep hunting, patrolling, and decaying. Every player acts independently against one continuous simulation clock.
- **Genre-agnostic content.** The engine is a substrate for fantasy, sci-fi, post-apocalyptic, horror, or historical games. Mechanics are data-driven (abilities with resource pools and effect types, not hard-coded "spells"); a campaign or mod defines what the content means.

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
- **[docs/audits/README.md](docs/audits/README.md)** — dated audit rounds: subsystem audits & improvement plan (2026-07-03), engine gap-analysis & roadmap (2026-07-06)
- **[docs/history/](docs/history/)** — archived point-in-time status documents

## Project status

The original subsystem-audit improvement plan is complete (foundation, live-path correctness, convergence, test depth, and the Phase-5 feature slices — combat, quests, instances, agent live-play, PCG placement). The forward-looking work is captured in the [2026-07-06 engine gap-analysis](docs/audits/2026-07-06-engine-gap-analysis/design-next-steps.md): deeper gameplay systems (a continuous action pipeline, combat depth, abilities, NPC AI, factions) and an authoring/scripting layer. See the [audit index](docs/audits/README.md) for the current state against the vision above.

## Spec-driven development

Requirements live in [openspec/specs/](openspec/specs/) (what IS built) and change proposals in [openspec/changes/](openspec/changes/) (what SHOULD change). See [openspec/AGENTS.md](openspec/AGENTS.md) for the workflow and [openspec/project.md](openspec/project.md) for project conventions.

## License

Aetherium is licensed under the [Apache License 2.0](LICENSE) — free for
commercial and non-commercial use, with an explicit patent grant. See
[NOTICE](NOTICE) for attribution details.

Contributions are welcome under the same license; see
[CONTRIBUTING.md](CONTRIBUTING.md) for the workflow (commits require a
[DCO](https://developercertificate.org/) sign-off: `git commit -s`).
