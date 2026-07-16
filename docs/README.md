# Aetherium Documentation

Aetherium is a server-authoritative multiplayer simulation engine (.NET 10, Orleans, SignalR). The server simulates the world and streams each player only semantic perception data; clients are thin renderers. See the root [README](../README.md) for the project overview, **vision & goals**, and quick-start.

This is the documentation index. Start with the section for your role.

## For players

The console/terminal client is the reference renderer — ASCII, with dynamic lighting, heat vision, and a day/night cycle.

- **[Console user guide](console/user/README.md)** — overview and feature tour
- **[Quick reference](console/user/quick-reference.md)** — controls and symbols at a glance
- **[Controls](console/user/controls.md)** · **[Gameplay](console/user/gameplay.md)** · **[Dynamic world](console/user/dynamic-world.md)** · **[Temporal modifiers](console/user/temporal-modifiers.md)**

## For developers

### Start here
- **[Development guide](development.md)** — setup, building, testing, workflow, debugging
- **[Architecture overview](architecture/overview.md)** — runtime topology, protocol, data flow, configuration

### Architecture
- **[Server](architecture/server.md)** — grains, ECS simulation, perception, worldgen, agents, narrative, events
- **[Clients](architecture/clients.md)** — console, Unity 2D, Dashboard, and the planned Unreal client
- **[Tooling & data](architecture/tooling-and-data.md)** — `aetherctl`, WorldGenCLI, scripts, `Data/` assets

### Subsystems
- **[Agent system](agents/README.md)** — LLM/heuristic agents, profiles, and the shared tool API · **[Tool catalog](agents/TOOLS.md)** · **[Tool profiles](agents/TOOL_PROFILES.md)**
- **[Narrative systems](narrative-systems.md)** — procedural quests, consequence engine, emergent storytelling
- **[Factions & reputation](factions-reputation.md)** — design vision: doctrine tags, standing bands, maturity ladder, ECA graduation path
- **[Party & shared play](party-shared-play.md)** — design vision: credit sharing, pings as perception, shared senses, LLM companions
- **[Economy simulation](economy-simulation.md)** — design vision: goods/flows/markets/transport layers, functional currency, LLM merchants
- **[Live event orchestrator](live-events.md)** — design vision: pressure signals, intensity-budget direction, persistent outcomes, the LLM game-master
- **[Gameplay telemetry](gameplay-telemetry.md)** — design vision: event records at chokepoints, rollups/heatmaps/funnels, synthetic playtesting, the LLM analyst
- **[Localization](localization.md)** — design vision: TextRef ids, per-locale catalogs and grammars, native procedural prose, LLM locale packs
- **[ECA scripting](eca-scripting.md)** — shipped T0 runtime: when/if/do rules as data, `creature_died` trigger, reflectable vocabulary registry, `rules.yaml` bundle section
- **[Grid topologies](grid-topologies.md)** — **built (P0–P3):** pluggable per-world tilings (square/hex/triangle) behind an `IGridTopology` seam with per-cell direction sets, threaded via `world.topology`; shaped for Uber's H3 hierarchical planetary grids
- **[H3 topology](h3-topology.md)** — **built:** Uber's H3 (planetary/lunar hexagons + 12 pentagons, nested resolutions) as a registered `IGridTopology`/`IHierarchicalGridTopology` on `pocketken.H3` — `WorldLocation` index packing, `Delta`-as-azimuthal-projection, invariant-green incl. pentagons; the sphere-aware perception keys + h3 generator remain the last mile for a playable planetary world
- **[Hexagonal tiles](hexagonal-tiles.md)** — the hexagon deep-dive behind the grid-topologies design: hex-specific FOV/worldgen analysis and the hex asset landscape
- **[Multi-world ecosystems](multiworld-ecosystems.md)** — clusters, portals, cross-world economy, meta-progression, hub worlds
- **[Instances](instances.md)** — dungeon instances, lockouts, party/raid grains
- **[Procedural audio](PROCEDURAL_AUDIO_IMPLEMENTATION.md)** — biome audio profiles and the audio generation pass
- **[PCG tools](pcg-tools.md)** — world generation via `aetherctl worldgen`
- **[Monitoring](monitoring.md)** — real-time game monitoring quick start
- **[Agent training](training/README.md)** — telemetry, curricula, benchmarks, and the training dashboard

### Clients
- **[Unity client library & Aphelion sample — design suite](design/unity-sample/README.md)** — proposed design: reusable `com.aetherium.unity` package, `samples/` layout, and a co-op sci-fi station-crawler sample game
- **[Unity 2D client](unity/README.md)** — legacy tilemap scaffold (superseded by the design suite above) · **[Unity testing](unity/testing.md)**
- **[Unreal client guide](clients/unreal-client-guide.md)** — forward-looking migration guide (client not yet built)

## Audits & status

Dated audit rounds live under **[docs/audits/](audits/README.md)**:
- **[2026-07-03 subsystem audit](audits/2026-07-03-initial-subsystem-audit/README.md)** — ten subsystem audits, the [recommendations register](audits/2026-07-03-initial-subsystem-audit/RECOMMENDATIONS.md), and the [improvement plan](audits/2026-07-03-initial-subsystem-audit/IMPROVEMENT_PLAN.md) (now complete)
- **[2026-07-06 engine gap-analysis](audits/2026-07-06-engine-gap-analysis/design-next-steps.md)** — forward-looking roadmap of engine-level gameplay systems, plus the [authoring](audits/2026-07-06-engine-gap-analysis/design-authoring-and-scripting.md) and [ECA visual-scripting](audits/2026-07-06-engine-gap-analysis/design-eca-visual-scripting.md) design sketches

The audit index carries the authoritative build/test ground truth. **[docs/history/](history/README.md)** holds superseded, point-in-time status reports and plans — accurate for their date, not current.

## Spec-driven development

Requirements live in [openspec/specs/](../openspec/specs/) (what IS built) and change proposals in [openspec/changes/](../openspec/changes/) (what SHOULD change). Follow the [OpenSpec workflow](../openspec/AGENTS.md) for capability, architecture, or breaking changes; small bug fixes and wiring go straight in.

## API surfaces

- **SignalR hubs** — `GameHub` (`/gamehub`, gameplay), `ManagementHub` (`/managementHub`, world management with Azure AD B2C auth), `AgentDashboardHub` (`/agentDashboardHub`, telemetry). Method signatures and client usage: [architecture/server.md](architecture/server.md) and [development.md](development.md#unified-cli-aetherctl).
- **REST** — `/api/cluster` (clusters, portals, economy), `/api/metaprogression/{playerId}`, `/api/management/worlds`, `/api/benchmark`, `/api/curriculum`, `/api/agenttelemetry`. Auth model: reads open for the dashboard; mutations gated by API key (see [architecture/server.md](architecture/server.md)).
