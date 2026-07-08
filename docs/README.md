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
- **[Multi-world ecosystems](multiworld-ecosystems.md)** — clusters, portals, cross-world economy, meta-progression, hub worlds
- **[Instances](instances.md)** — dungeon instances, lockouts, party/raid grains
- **[Procedural audio](PROCEDURAL_AUDIO_IMPLEMENTATION.md)** — biome audio profiles and the audio generation pass
- **[PCG tools](pcg-tools.md)** — world generation via `aetherctl worldgen`
- **[Monitoring](monitoring.md)** — real-time game monitoring quick start
- **[Agent training](training/README.md)** — telemetry, curricula, benchmarks, and the training dashboard

### Clients
- **[Unity 2D client](unity/README.md)** — tilemap rendering, Xbox controller support · **[Unity testing](unity/testing.md)**
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
