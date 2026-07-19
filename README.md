# Aetherium

Aetherium is a server-authoritative multiplayer simulation engine built on .NET 10, Microsoft Orleans, and SignalR. The server simulates an entire game world — procedural generation (from dungeons to whole planets), field-of-view and lighting, entities and combat, interactions, weather and seasons, flight and 3D vertical space, economies, AI agents with memory and recognition, and emergent narrative — and streams each player only what their character can perceive. Clients are thin renderers over one semantic perception protocol: a Spectre.Console terminal client (the reference renderer), a Unity client (built on the reusable `Aetherium.Client` library and `com.aetherium.unity` package), and a planned Unreal Engine client. Games are defined as data — `Data/Games/` already ships a sci-fi station-crawler (**Aphelion**), a walkable H3 planet (**Aphelion-H3**), and a fantasy world (**Emberfall**) — so the engine is a substrate for many genres, not a single game.

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
| [Aetherium.Client](Aetherium.Client/) | Reusable .NET client library: SignalR connection lifecycle, perception subscription, session resume — the basis for the Unity package and future clients |
| [Aetherium.Console](Aetherium.Console/) | Terminal client: Spectre.Console UI, NAudio audio, WebSocket monitoring server |
| [com.aetherium.unity](clients/unity/com.aetherium.unity/) | Reusable Unity client package (grid/depth rendering, follow camera, tile themes) built on `Aetherium.Client` |
| [samples/unity/Aphelion](samples/unity/Aphelion/) | Aphelion sample game — a co-op sci-fi station/planet crawler wiring the Unity package to a live server |
| [Aetherium.Unity](Aetherium.Unity/) | Legacy Unity 2D tilemap scaffold (mock-replay or live SignalR) — superseded by the package + sample above |
| [Aetherium.Dashboard](Aetherium.Dashboard/) | Blazor Server dashboard for agent telemetry, training progress, PCG, and world management |
| [Aetherctl](Aetherctl/) | Operator CLI (`aetherctl`): sessions, agents, worlds, worldgen, headless driving, scripted actions, telemetry, monitoring |
| [WorldGenCLI](WorldGenCLI/) | Procedural-generation client library used by Aetherctl and the Dashboard |
| [Aetherium.Test](Aetherium.Test/) | Server/engine test suite (xUnit + NUnit, includes Orleans TestingHost integration tests) |
| [Aetherium.Client.Tests](Aetherium.Client.Tests/) | Client-library test suite |
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
- **[docs/design/README.md](docs/design/README.md)** — design suite for the newer systems: flight & 3D depth, transit, boardable vehicles, planetary H3 worldgen, and the Unity sample
- **[docs/audits/README.md](docs/audits/README.md)** — dated audit rounds: subsystem audits & improvement plan (2026-07-03), engine gap-analysis & roadmap (2026-07-06)
- **[docs/history/](docs/history/)** — archived point-in-time status documents

## Project status

The original subsystem-audit improvement plan is complete (foundation, live-path correctness, convergence, test depth, and the Phase-5 feature slices — combat, quests, instances, agent live-play, PCG placement), and much of the forward-looking work from the [2026-07-06 engine gap-analysis](docs/audits/2026-07-06-engine-gap-analysis/design-next-steps.md) has since landed on `develop`:

- a data-driven **game-definition loader** — games as YAML bundles under `Data/Games/`, each instantiable as any number of concurrent worlds;
- a **combat & death** pipeline, **ECA visual scripting**, and agent **memory / individual recognition / interoception**;
- durable **SQLite persistence** (world snapshots + grain storage) alongside the in-memory default;
- **flight and 3D vertical worlds** — altitude bands, flight plans, occluded multi-Z perception, and depth-cued rendering on the console and Unity clients;
- **planetary H3 worldgen** — sphere-native terrain, rivers, ~320 tiered settlements, and roads — plus its **living systems**: a biome economy trading over the road/rail graph, orbiting satellites, and rail + subway transit.

The [audit index](docs/audits/README.md) carries authoritative build/test ground truth; the [design suite](docs/design/README.md) records the intent behind each system. Deeper gameplay layers (abilities, NPC behavior trees, factions, a continuous action pipeline) remain in progress — see the [OpenSpec changes](openspec/changes/) for what is proposed vs. built.

## Spec-driven development

Requirements live in [openspec/specs/](openspec/specs/) (what IS built) and change proposals in [openspec/changes/](openspec/changes/) (what SHOULD change). See [openspec/AGENTS.md](openspec/AGENTS.md) for the workflow and [openspec/project.md](openspec/project.md) for project conventions.

## License

Aetherium is licensed under the [Apache License 2.0](LICENSE) — free for
commercial and non-commercial use, with an explicit patent grant. See
[NOTICE](NOTICE) for attribution details.

Contributions are welcome under the same license; see
[CONTRIBUTING.md](CONTRIBUTING.md) for the workflow (commits require a
[DCO](https://developercertificate.org/) sign-off: `git commit -s`).
