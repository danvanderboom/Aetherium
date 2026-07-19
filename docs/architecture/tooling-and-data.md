# Tooling, Data & Developer Workflow

*Last updated: 2026-07-19. Covers `Aetherctl`, `WorldGenCLI`, dev scripts, monitoring endpoints, the `Data/` directory, and the OpenSpec workflow.*

## aetherctl (`Aetherctl/`)

The unified operator CLI, built on System.CommandLine. It is stateless — all state lives on the server — and speaks four protocols depending on the command: Orleans grain calls (telemetry, worlds, agents), SignalR `ManagementHub` (authenticated writes), HTTP (PCG, status), and WebSocket (console-client frame monitoring).

The command surface grew substantially with the operator-tooling wave — headless driving, scripted actions, runtime worldbuilding, telemetry, and cognition inspection. Command areas (by `Aetherctl/Commands/*`):

| Command area | Purpose |
|---|---|
| `server` | Status, diagnostics |
| `session` | List/create/close game sessions |
| `game` | **Headless session driving** — spawn/join a world, operator perception, capture a world snapshot |
| `action-script` | **Scripted / batch action execution** for one or more characters |
| `world` | Create/list/delete/configure worlds, plus **runtime worldbuilding** (spawn/edit live worlds) |
| `worldgen` | Run procedural generation, render PNG previews (SkiaSharp) |
| `scenario` | Set up scripted scenarios |
| `agent` | List agents, telemetry, pause/resume |
| `tools` | List/register tool definitions |
| `vision` / `perception` | Perception/FOV debugging, visibility queries, operator perception |
| `combat` | Combat inspection |
| `quest` | Quest activation/inspection |
| `instance` | Dungeon-instance operations |
| `memory` / `recognition` | Inspect character memory and individual recognition |
| `narrative` | Narrative event scheduling and triggers |
| `telemetry` | Telemetry inspection commands |
| `prompts` | Prompt registry operations |
| `monitor` | Attach to `ws://localhost:5001/monitor` |
| Global | `--json`, `--verbose`, `--quiet`; Orleans connection via `ORLEANS_GATEWAY` / `ORLEANS_CLUSTER_ID` / `ORLEANS_SERVICE_ID` |

Auth: Azure AD B2C device flow (`Auth/`) for management writes when the server has auth enabled.

Aetherctl pins `Microsoft.Orleans.Client` **9.2.1**, matching the server (previously pinned to 8.0.0 — fixed on `develop`; see [docs/audits/2026-07-03-initial-subsystem-audit/tooling-testing-devex.md](../audits/2026-07-03-initial-subsystem-audit/tooling-testing-devex.md)).

## WorldGenCLI (`WorldGenCLI/`)

Despite the name, this is a **library**, not a standalone CLI: a typed client for the server's PCG API, consumed by `aetherctl worldgen` and the Dashboard's PCG page. It provides `GenerateRequest`/`GenerateResponse`, `TemplateDto` (cellular automata, BSP, hybrid layouts), anchor-based `HybridLayout` room placement, constraint descriptors/schema building (NJsonSchema), and `RenderMapper` for visualization.

## Dev scripts

| Script | Purpose |
|---|---|
| `start-game-test.ps1 -TimeoutSeconds N` | Launch server + console client in separate windows; persists PIDs to `.game-run-pids.json`; auto-cleans on timeout |
| `stop-game.ps1 [-All]` | Kill tracked (or all) Aetherium processes |
| `scripts/monitor-game.ps1 [-DisplayAsciiMap] [-SaveToFile]` | Full-featured monitor client for `ws://localhost:5001/monitor` |
| `scripts/monitor-lite.ps1 [-MaxFrames N]` | Minimal monitor (no Unicode borders) — good for CI-ish assertions |
| `scripts/run-client-ui-tests.ps1` | Launch console client in `--ui-selftest` mode |
| `scripts/start-llm-agents.ps1` | Initialize and run LLM-based agents |
| `scripts/repro-move-down.ps1` | Scripted repro of a specific action sequence |

## Monitoring

- **Game frames**: console client's `MapFrameMonitor` WebSocket server — `ws://localhost:5001/monitor` (JSON frames: timestamp, frame number, raw `PerceptionDto`, ASCII map), `http://localhost:5001/health`, `http://localhost:5001/config`. Zero external dependencies; multiple concurrent subscribers; optional file logging. No authentication — dev-only by design.
- **Agent telemetry**: server-side `AgentTelemetryGrain` → `AgentDashboardHub` (SignalR) and `AgentTelemetryController` (REST), consumed by the Dashboard.

## Data directory (`Data/`)

| Folder | Contents | Loaded by | Status |
|---|---|---|---|
| `Benchmarks/` | navigation-basic, puzzle-keys, combat-survival JSON | `BenchmarkController` (REST, read-only) | Working |
| `Curricula/` | beginner-dungeon, advanced-combat | `CurriculumController` | Partially loaded (TODOs) |
| `Narratives/` | dungeon-exploration, emergent-storytelling-example, tutorial-village | Narrative grains | Working |
| `Prefabs/` | Buildings (shop, small-house), Terrain (forest-cluster, small-pond) | `PrefabLibrary` | Working — loads from `PREFAB_PATH` in Development or when `PREFAB_STORAGE=file` |
| `Games/` | YAML game bundles: `aphelion`, `aphelion-h3`, `emberfall` (`game.yaml` + optional `rules`/`content`/`abilities`/`factions`/`progression`) | `GameDefinitionRegistry` (`GAMES_PATH`, default `./Data/Games`) | Working |
| `Hubs/` | central-hub.json | `HubWorldLoader` (async at startup, `HUB_PATH` overridable) | Working |
| `Audio/` | BiomeAudioProfiles.json | `JsonAudioProfileRepository` | Working |

Each folder has its own README describing the JSON format. Training docs: [docs/training/](../training/).

## OpenSpec workflow

Aetherium uses spec-driven development. `openspec/specs/<capability>/spec.md` is current truth (~32 capabilities: engine-core, perception, perception-vision, client-server-communication, console-view, world-building, world-entities, pcg-* (7), narrative, interaction, audio, geometry-maze, game-management-grain, demo-game, multiworld, meta-progression, agent-telemetry, training-* (3), plus **flight, character-memory, identity-recognition, eca-scripting, aetherctl, client** — most added when the recent feature wave was archived on 2026-07-19); `openspec/changes/<id>/` holds proposals — still several dozen, spanning some **older completed changes** awaiting archival and **forward proposals** (abilities, factions, NPC behavior trees, continuous action pipeline, boardable vehicles, arcade-client, …). Note `add-arcade-client` is a design proposal (1/29 tasks), not yet built. Conventions and the full workflow: [openspec/AGENTS.md](../../openspec/AGENTS.md) and [openspec/project.md](../../openspec/project.md). Note: the `openspec` CLI is not required to read/edit specs — it validates and archives changes when available.

## Test projects

- **Aetherium.Test** — engine/server suite (xUnit + NUnit side by side, Orleans TestingHost for grain tests, Moq). Areas: FOV/vision, lighting, interaction/inventory, client-server hubs, worldgen, geometry, grains, tools, UI self-test.
- **Aetherctl.Test** — command-structure, common-options, session commands, worldgen PNG rendering.
- **Unity** — EditMode (JSON parsing) + PlayMode (input/gamepad/tilemap) suites inside the Unity project.
- Current ground-truth results and runtime caveats: [docs/audits/2026-07-03-initial-subsystem-audit/README.md](../audits/2026-07-03-initial-subsystem-audit/README.md). Client-side console UI logic (widgets, themes, audio) still lacks a dedicated test project (`Aetherium.Test/CLIENT_TESTS_README.md` has templates).
