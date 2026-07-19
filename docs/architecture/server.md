# Server Architecture

*Last updated: 2026-07-19. Covers `Aetherium.Server` and `Aetherium.Model`. See [overview.md](overview.md) for the system-level picture and [docs/audits/](../audits/) for known issues per subsystem.*

`Aetherium.Server` is a single ASP.NET Core process that hosts the game engine, three SignalR hubs, REST controllers, and (unless `DISABLE_ORLEANS=1`) a co-hosted Orleans silo. Game logic never lives in clients; everything below runs server-side.

## Hosting & composition (`Program.cs`)

- **DI composition**: `GameSessionManager`, `PerceptionService`, simulation services (`WorldClock`, `SeasonManager`, `WeatherSystem`, `SpawnManager`, `TemporalModifierRegistry`), worldgen services (`MapGeneratorRegistry`, `MapValidator`, `PrefabLibrary`), hub-world loaders, `AgentToolRegistry` (reflection discovery over the executing assembly), and `PromptRegistry` are host singletons.
- **Grain access to host services** uses a co-hosting bridge pattern: each needed singleton is re-registered inside the silo's service collection with a factory that resolves it from the outer `IHost` (see `Program.cs:276-348`).
- **Silo config**: localhost clustering; grain storage under named stores (`narrativeStore`, `worldStore`, `mapStore`) is in-memory by default or **durable SQLite** when `ORLEANS_STORAGE=sqlite` (or `AETHERIUM_DATA_DIR` is set), with an always-in-memory `PubSubStore`. The snapshot store (`IWorldSnapshotStore`) mirrors that choice — `SqliteWorldSnapshotStore` vs. `MemoryWorldSnapshotStore`. Azure Table Storage support is scaffolded but currently commented out.
- **Auth**: Azure AD B2C JWT bearer auth activates only when `AzureAdB2C` config is present; an `Admin` role policy guards management writes. An `ApiKeyMiddleware` runs ahead of routing for REST calls.
- **Game definitions**: `GameDefinitionRegistry` loads YAML game bundles from `GAMES_PATH` (default `./Data/Games`) at startup; each bundle is instantiable as any number of concurrently-running worlds (see [Game definitions](#game-definitions-games-datagames) below).
- **Known gaps** (see audits): `BuilderModifier` is constructed with a null `World` pending `BuildStructureAsync`, so AI-driven construction stays inert; `/dashboard` is a stub endpoint. *Previously-noted gaps now closed:* `WorldTickService` runs as a hosted service when Orleans is enabled, and prefab file loading is implemented.

## Client-facing surface

### SignalR hubs (`GameHub.cs`, `Hubs/`)

- **GameHub** (`/gamehub`) — the gameplay contract:
  - *Client → server*: `ExecuteTool(toolId, args)` and `ListAvailableTools()` (the unified action API), interaction methods (`Pickup`, `Drop`, `Use`, `Open`, `Close`), plus legacy `[Obsolete]` methods (`MovePlayer`, `RotatePlayer`, `RotatePlayerDegrees`, `ToggleDirectionalVision`, `ChangeLevel`, …) retained for backward compatibility.
  - *Server → client*: `ReceiveGameState(GameStateDto)` (player ID + heading only) and `ReceivePerceptionUpdate(PerceptionDto)`. (Interaction methods return their `InteractionResultDto` as the RPC result rather than via a separate push; the client has no dedicated error channel — see [audit](../audits/2026-07-03-initial-subsystem-audit/client-server-protocol.md).)
  - Forwards interaction events to the narrative consequence engine best-effort (failures are swallowed by design — and in practice the engine never runs because `session.WorldId` is null for real sessions; see [audit](../audits/2026-07-03-initial-subsystem-audit/narrative-and-multiworld.md)).
- **ManagementHub** (`/managementHub`) — world/session administration for `aetherctl` and the dashboard; write operations require the `Admin` role when auth is configured.
- **AgentDashboardHub** (`/agentDashboardHub`) — streams agent telemetry to the dashboard.

### DTO contracts (`Aetherium.Model`)

Pure data contracts, no logic: `PerceptionDto` (visible tiles + entities + inventory + affordances + audio), `VisualDto`, `GameStateDto`, `InventoryDto`/`ItemDto`, `AffordanceDto`, `InteractionResultDto`, `ToolDtos`, `ManagementDtos`, `NavigationDataDto`, `AudioPerceptionDto`, `SharedEnums` (LightingMode, VisionMode, RelativeDirection), plus contract folders for `Events/`, `Groups/`, `Instances/`, `Worlds/`. There is no explicit protocol versioning; evolution is additive.

## Game session & ECS core

- **GameSessionManager / GameSession** — one session per connected player; owns the player's `World` reference, character entity, and per-session settings (vision modes, time scale). This is the classic single-process path that predates and coexists with the Orleans multi-world path.
- **Entity/Component model** (`Core/`, `Components/`, `Entities/`) — ECS-lite: `Entity` carries a component list with `Get<T>()`/`Set<T>()` (no removal API; `Get<T>()` throws on a missing component — guard with `Has<T>()`). ~39 entity types (Character, Monster, Zombie, Snake, Item, Key, Door, Lever, Button, LightEntity, PortalEntity, Bomb, SecretDoor, SatelliteEntity, …) and ~52 component types across movement (`ObstructsMovement`, `Climbable`), inventory (`Carriable`, `Inventory`), interaction (`OpensAndCloses`, `Key`, `Lockpick`, `PressureSensitive`), vision (`ObstructsView`, `LightSource`), flight (`Flight`, `FlightPlan`), cognition (`Memory`, `MemoryProfile`, `IndividualRecognition`, `RecognitionProfile`, `SpaceTimeMemory`), combat (`CombatStatComponents`, `Health`, `DeathState`, `Downed`), and state (`HeatSignature`, `Hidden`, `Goal`, `Perception`). Tilings are per-world — each `World` carries an `IGridTopology` (square/hex/triangle/H3); see [Grid topology](#grid-topology-topology).
- **InteractionSystem** — validates and executes pickup/drop/use/open/close, emits `WorldEvent`s, and surfaces **affordances** (possible actions, with required keys) that flow to clients through perception.
- **ContextEvaluator** (`Core/ContextEvaluator.cs`) — evaluates situational context tags (e.g., `in-combat`, `near-door`, `in-forest`, `indoors`) for narrative/AI. Combat detection is live now that the combat system exists — `in-combat` is tagged when a living hostile is adjacent (the former `if (false)` placeholder is gone).

## Grid topology (`Topology/`)

Every `World` carries an `IGridTopology` defining its cell adjacency and per-cell direction set, so the same movement, perception, and worldgen code runs on **square, hexagonal, triangular, or H3** (Uber's hierarchical planetary hexagons + 12 pentagons) tilings. `GridTopologyRegistry` resolves a topology by name; `H3Topology` implements `IHierarchicalGridTopology`, `WorldLocation` packs an H3 cell index, and `Delta` is interpreted as an azimuthal projection on the sphere. Worlds that never touch the seam stay byte-identical to the original square path. Deep dives: [grid topologies](../grid-topologies.md), [H3 topology](../h3-topology.md).

## Simulation (`Simulation/`)

- **WorldClock** — configurable tick rate and in-game day length; source of in-game time.
- **SeasonManager**, **WeatherSystem** — season cycling and stochastic weather (affects visibility, spawns, atmosphere).
- **TemporalModifierRegistry** — pluggable time/weather-sensitive behaviors; currently `SpawnModifier` (weighted creature spawning) and `BuilderModifier` (AI-driven construction; presently inert, see hosting gaps).
- **WorldTickService** — background world-tick driver; runs as a hosted service driving the world tick when Orleans is enabled (a no-op placeholder in `DISABLE_ORLEANS` test runs).
- Simulation options come from the `Simulation` section of `appsettings.json` (TickHz, DayLengthMinutes, RegionSize, EnableWeather/Seasons/AgentChanges/ProceduralEvents).

## Perception & vision (`Perception/`, `Lighting/`, `PerceptionService.cs`)

The heart of the server-authoritative model. `PerceptionService.ComputePerception()` runs per player per action:

1. **FOV** — `FovCalculator` (octant shadow-casting, ray-casting fallback), range-limited (Chebyshev), opacity-aware; `DirectionalFovCalculator` restricts to a configurable cone (default 60°) when directional vision is on.
2. **Lighting** — `LightingSystem` propagates per-source light with falloff; `SunlightCalculator` derives outdoor light from `WorldClock` time-of-day/season/weather. Modes: Torch, Lantern, Sunlight, Nightvision, Infrared.
3. **Vision modes** — the `VisionMode` enum defines two values: Normal and Infrared (`InfraredVisionSystem` + `HeatTrailTracker` render heat signatures and movement trails). The `LightingMode` enum defines three: Torch, Ambient, Sunlight. (Older docs referencing 5 lighting modes or an "echolocation" vision mode are inaccurate.)
4. **Rendering** — tiles resolved to icons/colors and packed into `PerceptionDto`.
5. **Interoception** — an added self-sense channel folds the character's own internal state into perception, so a client can render "what my body senses," not only what is outside it (`add-interoception-channel`).
6. **3D vertical perception (optional)** — for worlds with altitude bands, perception resolves a multi-Z **slab** with per-band occlusion (see the bird overhead, but not through the bridge — yes through a skylight) and reports a **flight envelope** (floor/ceiling) consumed by the depth camera and altitude gauge. Obstruction has three independent facets: movement (`ObstructsMovement`), sight (`ObstructsView.Opacity`), light (`BlocksLight`).
7. **Sphere-native H3** — on H3 worlds, `H3VisionLighting` computes FOV and lighting over the hexagonal sphere; the walkable viewport is a `gridDisk` around the player, keyed by `cellToLocalIj`.

Audio perception (`Sound`, `HearingFrame`, `AudioPerceptionDto`) is modeled but only partially integrated. The FOV/rotation history is documented in `docs/history/FOV_*.md`; current status is assessed in [docs/audits/2026-07-03-initial-subsystem-audit/perception-fov-lighting.md](../audits/2026-07-03-initial-subsystem-audit/perception-fov-lighting.md).

## World generation (`WorldGen/`, `WorldBuilders/`)

- **Pipeline**: `GeneratorPipeline` → `GeneratorContext` (seed, options) → generators (City, Outdoor, Dungeon, AdvancedDungeon, …) composed of **phases** (initialize, place structures, populate, story elements, validate) and **features** (RiverCarver, ItemDistribution, SpawnNPCs, PlaceLoreFragments, Ruins, EnsureExits, …), then post-processing **passes** and `GenerationValidationService`.
- **Algorithms**: Perlin noise, Poisson disc sampling, minimum spanning tree connectivity, flood fill.
- **H3 sphere generators** (`WorldGen/Generators/Outdoor/H3*`) — sphere-native planetary worldgen: `H3TerrainGenerator` (3D noise over the sphere via `H3SphereGeo`), `H3RiverCarver`, `H3SettlementPlanner` (~320 tiered towns), `H3RoadNetwork`, `H3TransitNetwork` (rail + subway), and `H3SatelliteSeeder`. The result is a walkable planet with real sight, rivers, roads, and living systems.
- **WorldBuilders** — hand-authored/diagnostic builders (`DungeonCrawlerWorldBuilder`, `TorusWorldBuilder`, `FovDiagnosticWorldBuilder`, `TestMazeWorldBuilder`, `AudioTestWorldBuilder`, …) that construct specific worlds; `MapStandards.md` (same directory) defines boundary, lighting, start-location, and terrain-registration requirements every map must meet.
- **Prefabs** (`WorldGen/Prefabs`, `Data/Prefabs/*.json`) — JSON building/terrain templates managed by `PrefabLibrary`; file loading is implemented, so disk prefabs load from `PREFAB_PATH` in Development or when `PREFAB_STORAGE=file`.
- **Hub worlds** (`HubWorld/`, `Data/Hubs/central-hub.json`) — loaded at startup by `HubWorldLoader` for cross-world travel hubs.

## Combat & death (`Combat/`)

A data-driven combat pipeline: `DamagePacket` → `DamagePipeline` → `DamageResolution` applies damage against `CombatStatComponents`/`Health`; `DeathSystem` transitions entities through `Downed`/`DeathState`, and `CorpseExpirySystem` ages and removes corpses (`CorpseAge`). Exposed to players and agents through `AttackTool`. Death/respawn and combat depth are configured per world (threaded through world creation as data) rather than hard-coded.

## Flight & 3D depth (`Flight/`, `Components/Flight*.cs`)

Vertical worlds are built on **altitude bands** and **flight plans**. `FlightController` moves airborne entities; `FlightPlanSystem`/`FlightPlanGenerator` follow one of four plan sources — Patterned (orbit/wander), AdHoc (summon/pick destination), Scheduled (timetables), Manual (piloting). `FlyerInteractionSystem` handles land/takeoff and flyer affordances, with agent tools under `Agents/Tools/Flight/` (`SummonTool`, `HackTool`, `AttackFlyerTool`, `FlyerAffordancesTool`). Perception renders the resulting multi-Z world through the depth slab described above. *Note:* `FlightPlanSystem` currently follows the square/grid path; H3 flight is a follow-on. Design: [flying-entities](../design/flying-entities.md), [adaptive-depth-visualization](../design/adaptive-depth-visualization.md).

## Economy, satellites & transit (`Economy/`, `Satellites/`)

The planetary "living systems," all opt-in so non-planetary games are unaffected:

- **Economy** (`EconomySystem`, `EconomySeeder`, `Goods`) — biome producers and consumers seed markets that trade goods over the road/rail/subway graph, with prices responding to supply/demand so arbitrage emerges along transport routes.
- **Satellites** (`SatelliteSystem`, `SatelliteRegistry`, `Entities/SatelliteEntity`) — a constellation orbits the high bands, never colliding; detection is radio-gated and satellites can be hacked. Seeded by `H3SatelliteSeeder`.
- **Transit** (`WorldGen` `H3TransitNetwork` + the flight-plan follower) — a rail backbone and underground subways connect settlements across bands; scheduled services are just flight plans.

Design: [H3 sphere-worldgen](../design/h3-sphere-worldgen.md), [transit-networks](../design/transit-networks.md).

## Cognition: memory & recognition (`Components/Memory*.cs`, `Recognition*`, `Core/*Policy.cs`)

Characters now remember and recognize. `Memory`/`MemoryProfile` record what a character perceives at perception time and expose a read API; `MemoryPolicy` governs **memory dynamics** — reinforcement on repeat exposure, permanence, and forgetting. `IndividualRecognition`/`RecognitionProfile` + `RecognitionPolicy` let a character recognize specific individuals over time rather than just "a person." `SpaceTimeMemory` retains last-seen locations (the client's "ghost" of a creature). Design intent lives in the `add-memory-dynamics`, `add-identity-recognition`, and `add-character-memory` OpenSpec changes.

## Game definitions (`Games/`, `Data/Games`)

`GameDefinitionRegistry` loads YAML **game bundles** from `GAMES_PATH` at startup. A bundle is a `game.yaml` plus optional `rules.yaml` (ECA scripting), `content.yaml`, `abilities.yaml`, `factions.yaml`, and `progression.yaml`; each is instantiable as any number of concurrently-running worlds. Shipped bundles: `aphelion` (sci-fi), `aphelion-h3` (planetary), `emberfall` (fantasy). This is how the engine stays a genre-agnostic substrate — the meaning of content is data, not code.

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

- **Tool registry** — `AgentToolRegistry` discovers `[AgentTool]` classes via reflection; each `IAgentTool` declares ID, description, categories, required capabilities, and a JSON parameter schema. ~45 tools spanning Movement, Interaction, Vision, WorldBuilding, MultiWorld, **Combat** (`AttackTool`), **Flight** (`SummonTool`, `HackTool`, `AttackFlyerTool`, `FlyerAffordancesTool`), **Memory**, and **Quest** categories, plus compound tools.
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

`IEventScheduler`/`EventScheduler` (host service) plus `EventSchedulerGrain`/`EventInstanceGrain`/`SpawnControllerGrain` schedule and run world events (spawn waves, timed occurrences). The spawn-integration work — handler↔`SpawnControllerGrain` wiring, region→map resolution, and despawn-on-completion — is complete; the original plan is archived at [history/events-spawn-integration-plan.md](../history/events-spawn-integration-plan.md).

## Monitoring & telemetry

Server side: `AgentTelemetryGrain`, `AgentTelemetryController` (REST), `AgentDashboardHub` (SignalR). Client side: the console client embeds a dependency-free WebSocket server (`ws://localhost:5001/monitor`) broadcasting per-frame perception + rendered ASCII maps to PowerShell monitors — see [tooling-and-data.md](tooling-and-data.md#monitoring).

## Persistence (`Persistence/`)

`IWorldSnapshotStore` has two implementations selected by storage mode: `MemoryWorldSnapshotStore` (default) and **`SqliteWorldSnapshotStore`** for durable on-disk snapshots. Setting `ORLEANS_STORAGE=sqlite` (or `AETHERIUM_DATA_DIR`) also switches grain storage to `SqliteGrainStorage`, giving a durable disk path. Snapshots carry the world's topology through rehydration, and `PersistenceVersionMismatchException` guards schema drift; the `Persistence` config section tunes snapshot compaction cadence + threshold. Azure/cloud storage remains scaffolded-but-commented.
