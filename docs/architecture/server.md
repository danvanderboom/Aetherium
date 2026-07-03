# Server Architecture

*Last updated: 2026-07-03. Covers `Aetherium.Server` and `Aetherium.Model`. See [overview.md](overview.md) for the system-level picture and [docs/audits/](../audits/) for known issues per subsystem.*

`Aetherium.Server` is a single ASP.NET Core process that hosts the game engine, three SignalR hubs, REST controllers, and (unless `DISABLE_ORLEANS=1`) a co-hosted Orleans silo. Game logic never lives in clients; everything below runs server-side.

## Hosting & composition (`Program.cs`)

- **DI composition**: `GameSessionManager`, `PerceptionService`, simulation services (`WorldClock`, `SeasonManager`, `WeatherSystem`, `SpawnManager`, `TemporalModifierRegistry`), worldgen services (`MapGeneratorRegistry`, `MapValidator`, `PrefabLibrary`), hub-world loaders, `AgentToolRegistry` (reflection discovery over the executing assembly), and `PromptRegistry` are host singletons.
- **Grain access to host services** uses a co-hosting bridge pattern: each needed singleton is re-registered inside the silo's service collection with a factory that resolves it from the outer `IHost` (see `Program.cs:276-348`).
- **Silo config**: localhost clustering, memory grain storage under three named stores (`narrativeStore`, `worldStore`, `mapStore`), memory streams (`Default`). Azure Table Storage support is scaffolded but currently commented out (`Program.cs:247-273`).
- **Auth**: Azure AD B2C JWT bearer auth activates only when `AzureAdB2C` config is present; an `Admin` role policy guards management writes. An `ApiKeyMiddleware` runs ahead of routing for REST calls.
- **Known gaps** (see audits): `WorldTickService` is registered but intentionally never started as a hosted service (`Program.cs:162-164`); `BuilderModifier` is constructed with a null `World` pending `BuildStructureAsync` (`Program.cs:136-140`); prefab file loading is a TODO (`Program.cs:207`); `/dashboard` is a stub endpoint.

## Client-facing surface

### SignalR hubs (`GameHub.cs`, `Hubs/`)

- **GameHub** (`/gamehub`) — the gameplay contract:
  - *Client → server*: `ExecuteTool(toolId, args)` and `ListAvailableTools()` (the unified action API), interaction methods (`Pickup`, `Drop`, `Use`, `Open`, `Close`), plus legacy `[Obsolete]` methods (`MovePlayer`, `RotatePlayer`, `RotatePlayerDegrees`, `ToggleDirectionalVision`, `ChangeLevel`, …) retained for backward compatibility.
  - *Server → client*: `ReceiveGameState(GameStateDto)` (player ID + heading only) and `ReceivePerceptionUpdate(PerceptionDto)`. (Interaction methods return their `InteractionResultDto` as the RPC result rather than via a separate push; the client has no dedicated error channel — see [audit](../audits/client-server-protocol.md).)
  - Forwards interaction events to the narrative consequence engine best-effort (failures are swallowed by design — and in practice the engine never runs because `session.WorldId` is null for real sessions; see [audit](../audits/narrative-and-multiworld.md)).
- **ManagementHub** (`/managementHub`) — world/session administration for `aetherctl` and the dashboard; write operations require the `Admin` role when auth is configured.
- **AgentDashboardHub** (`/agentDashboardHub`) — streams agent telemetry to the dashboard.

### DTO contracts (`Aetherium.Model`)

Pure data contracts, no logic: `PerceptionDto` (visible tiles + entities + inventory + affordances + audio), `VisualDto`, `GameStateDto`, `InventoryDto`/`ItemDto`, `AffordanceDto`, `InteractionResultDto`, `ToolDtos`, `ManagementDtos`, `NavigationDataDto`, `AudioPerceptionDto`, `SharedEnums` (LightingMode, VisionMode, RelativeDirection), plus contract folders for `Events/`, `Groups/`, `Instances/`, `Worlds/`. There is no explicit protocol versioning; evolution is additive.

## Game session & ECS core

- **GameSessionManager / GameSession** — one session per connected player; owns the player's `World` reference, character entity, and per-session settings (vision modes, time scale). This is the classic single-process path that predates and coexists with the Orleans multi-world path.
- **Entity/Component model** (`Core/`, `Components/`, `Entities/`) — ECS-lite: `Entity` carries a component list with `Get<T>()`/`Set<T>()` (no removal API). ~33 entity types (Character, Monster, Zombie, Snake, Item, Key, Door, Lever, Button, LightEntity, PortalEntity, Bomb, SecretDoor, …) and ~48 component types across movement (`ObstructsMovement`, `Climbable`), inventory (`Carriable`, `Inventory`), interaction (`OpensAndCloses`, `Key`, `Lockpick`, `PressureSensitive`), vision (`ObstructsView`, `LightSource`), and state (`Health`, `HeatSignature`, `Hidden`, `Goal`, `Perception`, `SpaceTimeMemory`).
- **InteractionSystem** — validates and executes pickup/drop/use/open/close, emits `WorldEvent`s, and surfaces **affordances** (possible actions, with required keys) that flow to clients through perception.
- **ContextEvaluator** (`Core/ContextEvaluator.cs`) — evaluates situational context (e.g., "in combat", "near light") for narrative/AI; combat detection is currently a stub (always false — there is no combat system yet).

## Simulation (`Simulation/`)

- **WorldClock** — configurable tick rate and in-game day length; source of in-game time.
- **SeasonManager**, **WeatherSystem** — season cycling and stochastic weather (affects visibility, spawns, atmosphere).
- **TemporalModifierRegistry** — pluggable time/weather-sensitive behaviors; currently `SpawnModifier` (weighted creature spawning) and `BuilderModifier` (AI-driven construction; presently inert, see hosting gaps).
- **WorldTickService** — background world-tick driver; registered but not started (manual ticking only).
- Simulation options come from the `Simulation` section of `appsettings.json` (TickHz, DayLengthMinutes, RegionSize, EnableWeather/Seasons/AgentChanges/ProceduralEvents).

## Perception & vision (`Perception/`, `Lighting/`, `PerceptionService.cs`)

The heart of the server-authoritative model. `PerceptionService.ComputePerception()` runs per player per action:

1. **FOV** — `FovCalculator` (octant shadow-casting, ray-casting fallback), range-limited (Chebyshev), opacity-aware; `DirectionalFovCalculator` restricts to a configurable cone (default 60°) when directional vision is on.
2. **Lighting** — `LightingSystem` propagates per-source light with falloff; `SunlightCalculator` derives outdoor light from `WorldClock` time-of-day/season/weather. Modes: Torch, Lantern, Sunlight, Nightvision, Infrared.
3. **Vision modes** — the `VisionMode` enum defines two values: Normal and Infrared (`InfraredVisionSystem` + `HeatTrailTracker` render heat signatures and movement trails). The `LightingMode` enum defines three: Torch, Ambient, Sunlight. (Older docs referencing 5 lighting modes or an "echolocation" vision mode are inaccurate.)
4. **Rendering** — tiles resolved to icons/colors and packed into `PerceptionDto`.

Audio perception (`Sound`, `HearingFrame`, `AudioPerceptionDto`) is modeled but only partially integrated. The FOV/rotation history is documented in `docs/history/FOV_*.md`; current status is assessed in [docs/audits/perception-fov-lighting.md](../audits/perception-fov-lighting.md).

## World generation (`WorldGen/`, `WorldBuilders/`)

- **Pipeline**: `GeneratorPipeline` → `GeneratorContext` (seed, options) → generators (City, Outdoor, Dungeon, AdvancedDungeon, …) composed of **phases** (initialize, place structures, populate, story elements, validate) and **features** (RiverCarver, ItemDistribution, SpawnNPCs, PlaceLoreFragments, Ruins, EnsureExits, …), then post-processing **passes** and `GenerationValidationService`.
- **Algorithms**: Perlin noise, Poisson disc sampling, minimum spanning tree connectivity, flood fill.
- **WorldBuilders** — hand-authored/diagnostic builders (`DungeonCrawlerWorldBuilder`, `TorusWorldBuilder`, `FovDiagnosticWorldBuilder`, `TestMazeWorldBuilder`, `AudioTestWorldBuilder`, …) that construct specific worlds; `MapStandards.md` (same directory) defines boundary, lighting, start-location, and terrain-registration requirements every map must meet.
- **Prefabs** (`WorldGen/Prefabs`, `Data/Prefabs/*.json`) — JSON building/terrain templates managed by `PrefabLibrary`; file loading is currently stubbed, so disk prefabs are not yet reachable at runtime.
- **Hub worlds** (`HubWorld/`, `Data/Hubs/central-hub.json`) — loaded at startup by `HubWorldLoader` for cross-world travel hubs.

## Orleans grain layer

~46 grain types organize the distributed side:

| Group | Grains | Notes |
|---|---|---|
| Core game | `WorldGrain` (reentrant), `GameMapGrain`, `MapRegionGrain`, `GameManagementGrain` | World/map/region state; management grain exposes session tracking, world lifecycle, and tool execution |
| Multi-world infra | `WorldDirectoryGrain`, `WorldAclGrain`, `WorldInviteGrain`, `ContentCatalogGrain`, `ClusterGrain` | Registry, ACLs, invites, shared content, world clusters |
| Instances & groups | `InstanceAllocatorGrain`, `DungeonInstanceGrain`, `PartyGrain`, `RaidGrain`, `LockoutLedgerGrain` | Instance allocation honoring lockouts, party/raid membership |
| Agents & training | `AgentGrain`, `AgentRunnerGrain`, `AgentTelemetryGrain`, `BehaviorAnalysisGrain`, `PromptRegistryGrain`, `CurriculumProgressionGrain` | Agent lifecycle & telemetry (game-join integration incomplete) |
| Narrative & events | `NarrativeGrain`, `NarrativeStateGrain`, `AdaptiveNarrativeGrain`, `EventSchedulerGrain`, `EventInstanceGrain`, `SpawnControllerGrain` | Narrative state/branching, scheduled world events |
| Progression | `MetaProgressionGrain` | Cross-world discoveries/unlocks |

Persistence uses `[PersistentState]` against the three memory stores. Planned-but-unbuilt grain families (travel network, housing, factions, territory) are catalogued in `docs/history/ORLEANS_IMPLEMENTATION_PLAN.md`.

## Agents & tool system (`Agents/`)

- **Tool registry** — `AgentToolRegistry` discovers `[AgentTool]` classes via reflection; each `IAgentTool` declares ID, description, categories, required capabilities, and a JSON parameter schema. 31 tools across Movement (4), Interaction (5), Vision (4), WorldBuilding (5), MultiWorld (5), and compound tools.
- **Access control** — `AgentToolProfile` grants tools by category *and* capability; predefined profiles: Explorer, Player, WorldBuilder, NarrativeDesigner, Admin, FullAccess. Admin-only tools (e.g., `JumpToLocationTool`) require explicit capability grants.
- **Execution paths** — the same tools execute via `GameHub.ExecuteTool` (players) or via management/agent grains (AI, CLI).
- **Agent runtime** — `AgentGrain`/`AgentRunnerGrain` hold lifecycle and prompt state; connecting agents into live game sessions and wiring actual LLM calls (`MicrosoftAgentAdapter`) are incomplete (TODOs in code).
- **Prompts** — markdown templates in `Prompts/` (`agent_explorer.md`, `explorer.md`, `combat.md`) loaded by `PromptRegistry` at startup.
- **Telemetry & training** — `AgentTelemetryGrain` + `AgentDashboardHub` + REST controllers feed the dashboard; benchmarks (`Data/Benchmarks`) and curricula (`Data/Curricula`) drive training scenarios; `BehaviorAnalysisGrain` aggregates behavior patterns.

## Narrative & multi-world (`Narrative/`, `MultiWorld/`, `MetaProgression/`)

- **Narrative grains** store branching state and player consequences; `NarrativeConsequenceEngine` maps gameplay events (item pickups, door openings, defeats) onto narrative state. It is currently invoked only from `GameHub` interactions, best-effort.
- **Procedural narrative** — `LoreGenerator` and `NarrativeGraphGenerator` exist but are not yet integrated into the main pipeline.
- **Cross-world** — `CrossWorldConstraint`/`Resolver` model consequences across worlds; portals (`PortalEntity` + registration tools) and trade routes are partially stubbed; `ClusterGrain` (shared economy/events per world cluster) is minimal.
- **Meta-progression** — `MetaProgressionGrain` tracks discoveries/unlocks that persist across worlds. Design docs: [docs/multiworld-ecosystems.md](../multiworld-ecosystems.md), [docs/narrative-systems.md](../narrative-systems.md), [docs/instances.md](../instances.md).

## Events (`Events/`)

`IEventScheduler`/`EventScheduler` (host service) plus `EventSchedulerGrain`/`EventInstanceGrain`/`SpawnControllerGrain` schedule and run world events (spawn waves, timed occurrences). Integration plan: [docs/events-spawn-integration-plan.md](../events-spawn-integration-plan.md).

## Monitoring & telemetry

Server side: `AgentTelemetryGrain`, `AgentTelemetryController` (REST), `AgentDashboardHub` (SignalR). Client side: the console client embeds a dependency-free WebSocket server (`ws://localhost:5001/monitor`) broadcasting per-frame perception + rendered ASCII maps to PowerShell monitors — see [tooling-and-data.md](tooling-and-data.md#monitoring).

## Persistence (`Persistence/`)

`IWorldSnapshotStore` with a memory implementation (`MemoryWorldSnapshotStore`) for world snapshots; grain state persists to the named memory stores. There is no durable (disk/cloud) persistence path wired up today.
