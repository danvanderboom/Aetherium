# Architecture: Unity Client ↔ Aetherium Server

*Part of the [Unity sample design suite](README.md). Status: proposed design; server-side elements shown are shipped unless marked as gaps.*

This is the end-to-end picture: how the Aphelion Unity client's subsystems relate to the Aetherium server/cluster's subsystems, what flows over the wire, and how the same architecture spans one laptop or a cloud cluster. Component internals: [unity-client-library.md](unity-client-library.md). Server internals: [docs/architecture/server.md](../../architecture/server.md).

## The one-diagram version

Server-authoritative, perception-based, data-driven: the server simulates everything and streams each player a *view*; the client turns views into light and sound; a YAML bundle defines what any of it means.

```mermaid
flowchart LR
    subgraph unity ["Unity client — samples/unity/Aphelion"]
        direction TB
        INPUT[Input & intents]
        HUD[HUD · lobby UI · score]
        MUSIC[Music director<br/>adaptive stems]
        subgraph pkg ["com.aetherium.unity"]
            BEH[AetheriumClientBehaviour<br/>+ MainThreadDispatcher]
            VIEWS[GridMapView ·<br/>EntityViewRegistry]
            THEME[ThemeAsset<br/>content id → asset]
            AUDBIND[AudioBinder]
        end
        subgraph core ["Aetherium.Client (core)"]
            CONN[AetheriumConnection]
            TOOLS[ToolClient]
            STORE[PerceptionStore<br/>anchor · diff · memory]
            LOBBY[LobbyClient]
        end
    end

    subgraph server ["Aetherium.Server host (ASP.NET Core + Orleans silo)"]
        direction TB
        HUB["GameHub /gamehub<br/>ExecuteTool · JoinWorld · ListWorlds"]
        GSM[GameSessionManager<br/>one GameSession per connection]
        PERC[PerceptionService<br/>FOV · lighting · vision modes · audio]
        REG[AgentToolRegistry<br/>move · attack · use · …]
        subgraph grains ["Orleans grains"]
            MGMT[GameManagementGrain<br/>definitions → instances]
            WG[WorldGrain]
            MAPG[GameMapGrain<br/>canonical World · ECS ·<br/>combat · ECA runtime · NPC AI]
        end
        TICK[WorldTickService ≈1 Hz]
        LOADER[GameDefinitionRegistry<br/>+ compilers: content · ECA ·<br/>abilities · factions · progression]
    end

    BUNDLE[("Data/Games/aphelion/*.yaml<br/>world · content · rules ·<br/>abilities · factions · progression")]
    CLI[aetherctl<br/>game create/list]

    INPUT --> TOOLS
    LOBBY --> HUB
    TOOLS -- "SignalR JSON:<br/>ExecuteTool(toolId, args)" --> HUB
    HUB -- "ReceivePerceptionUpdate<br/>(full PerceptionDto frames)" --> CONN
    CONN --> STORE
    STORE --> BEH --> VIEWS
    VIEWS --> THEME
    BEH --> AUDBIND --> MUSIC
    STORE --> HUD

    HUB --> REG --> GSM
    GSM --> PERC
    HUB <--> MAPG
    MGMT --> WG --> MAPG
    TICK --> WG
    LOADER --> MGMT
    BUNDLE --> LOADER
    CLI --> MGMT
    MAPG -- "delta fan-out →<br/>recompute per session" --> GSM
    GSM -- frames --> HUB
```

The load-bearing properties:

1. **No rules on the client.** The Unity side contains zero game logic — damage, deaths, ECA reactions, faction standing all resolve in `GameMapGrain`. The client can be wrong about nothing; at worst it renders late.
2. **The wire is views, not state.** Clients receive only what their character perceives (FOV, lighting, vision mode), always player-relative. Two co-op partners get *different* frames from the same mutation.
3. **Meaning and presentation meet at content ids.** The bundle names things (`custodian`, `arc-cutter`, `burning`); perception carries those ids; the ThemeAsset binds them to prefabs and sounds. Adding a creature to the game = one YAML block + one theme row.

## Sequence: session bootstrap

```mermaid
sequenceDiagram
    autonumber
    participant U as Unity (game code)
    participant C as Aetherium.Client
    participant GH as GameHub
    participant MG as GameManagementGrain
    participant MAP as GameMapGrain
    participant SS as GameSession (server-side)

    Note over MG: earlier: aetherctl game create aphelion<br/>bundle → CreateWorldRequest → world + maps live
    U->>C: Connect(url)
    C->>GH: SignalR negotiate + connect
    GH->>SS: create session (private world until join)
    GH-->>C: ReceiveGameState · ReceivePerceptionUpdate
    U->>C: Lobby: ListWorlds()
    C->>GH: ListWorlds
    GH-->>C: [WorldInfo…] → lobby UI
    U->>C: JoinWorld(worldId)
    C->>GH: JoinWorld(worldId)
    GH->>MAP: JoinPlayerAsync(sessionId)
    MAP->>MAP: spawn Character at dock<br/>fan out EntityAdded to others
    GH->>MAP: GetWorldSnapshotForJoiner
    MAP-->>GH: snapshot (recipe + entities)
    GH->>SS: hydrate session mirror ·<br/>gateway = GrainMutationGateway(mapId)
    GH-->>C: JoinWorldResult {spawn} + first frame
    C->>C: PerceptionStore re-anchors at spawn
    C-->>U: FrameReceived → render docking bay
```

A client may skip the lobby by connecting with `?worldId=<id>` in the query string — the hub auto-joins on connect. That's the "join my friend's station" deep-link path.

## Sequence: the co-op loop (one action, two players)

What makes multiplayer *feel* shared: any mutation on the map re-renders everyone on it, each through their own perception.

```mermaid
sequenceDiagram
    autonumber
    participant A as Client A (acting)
    participant GH as GameHub
    participant MAP as GameMapGrain
    participant GSM as GameSessionManager
    participant PA as Session A perception
    participant PB as Session B perception
    participant B as Client B (watching)

    A->>GH: ExecuteTool("attack", {custodian})
    GH->>MAP: AttackAsync(sessionA, target)
    MAP->>MAP: damage pipeline → custodian dies<br/>XP + faction deltas (HALCYON −10)<br/>ECA: custodian-burst → spawn 2 scrap-mites
    MAP-->>GH: result {damage, defeated:true}
    MAP--)GSM: deltas: EntityRemoved(custodian),<br/>EntityAdded(mite ×2)
    GSM->>PA: apply to A's mirror → recompute FOV frame
    GSM->>PB: apply to B's mirror → recompute FOV frame
    GSM--)A: ReceivePerceptionUpdate (A's view)
    GSM--)B: ReceivePerceptionUpdate (B's view)
    GH-->>A: attack result (damage number, kill credit)
    Note over B: B sees the custodian vanish and mites appear<br/>only if they're in B's FOV — else nothing
```

The same fan-out path carries NPC behavior: `WorldTickService` (≈1 Hz) ticks each world → `GameMapGrain.StepNpcsAsync()` runs behavior trees → movement/attack deltas → fresh frames to whoever can perceive them. **If nothing changes near you, no frames arrive** — an idle dark corridor costs no bandwidth, and the client's ambient beauty (dust, hum, music) is deliberately self-sustaining between frames.

## Data flow: from YAML to pixels

The full life of one line of game data — the row `custodian` — across every boundary:

```mermaid
flowchart TD
    Y["content.yaml<br/>id: custodian · health: 30 ·<br/>behavior: wander-melee · glyph: c"]
    L[GameDefinitionLoader + validator<br/>strict parse, cross-ref checks]
    M[GameDefinitionMapper →<br/>CreateWorldRequest.ContentConfig]
    G[GameMapGrain: ContentCompiler<br/>→ Monster entity + CreatureTypeTag +<br/>tile 'Creature:custodian' · behavior tree]
    P["PerceptionService →<br/>CharacterDto {Id, Name:'Creature:custodian',<br/>Tile{glyph c, colors}, IsHostile, rel loc}"]
    S[PerceptionStore: diff →<br/>EntityAppeared'custodian']
    T["ThemeAsset lookup: custodian →<br/>drone prefab · servo voice · death VFX"]
    V[EntityViewRegistry: spawn view,<br/>tween moves, worklight flicker]

    Y --> L --> M --> G --> P -- "SignalR JSON" --> S --> T --> V
```

Three different consumers read the same id at different stages: the **validator** (is `custodian` referenced by spawns/loot/rules real?), the **ECA runtime** (`creature_type_is: custodian`), and the **theme** (which prefab?). One name, defined once, in data.

## Deployment topologies

The client is topology-blind — it sees one URL. Everything below is server-side arrangement:

| Topology | Arrangement | Use |
|---|---|---|
| **Dev laptop** | One `Aetherium.Server` process (co-hosted silo), Unity editor pointed at `http://localhost:5000`. Anonymous auth. `aetherctl game create aphelion` to stand up a station. | Daily development; the M0 default |
| **LAN co-op** | Same single process, friends connect to the host's LAN address (`ASPNETCORE_URLS` bind). | Playtests; M0 acceptance runs |
| **Cloud** | Multiple silos, Orleans clustering; SignalR scales across silos via the already-referenced `UFX.Orleans.SignalRBackplane` (a client's hub connection can land on any host while its map grain lives on another). JWT auth (Azure AD B2C) switches on via config — the same `GameClient` policy the hub already carries; the client library's token provider fills the gap. Grain persistence currently memory-only — durable storage is a known engine-level TODO, orthogonal to this design. | Later; nothing in the client design changes |

Management stays a separate plane in all topologies: `aetherctl`/REST (API-key gated in prod) create and operate worlds; the game client only lists/joins.

## Performance & responsiveness model

- **Action feedback:** full round-trip (input → grain → fresh frame) targets the protocol spec's ≤100 ms on LAN. Presentation starts on input (wind-ups, sounds); state changes only on server confirmation.
- **Frame sizes:** a full perception frame for a 15×11 view with a handful of entities is a few KB of JSON; at human action rates plus 1 Hz ambient ticks this is trivially cheap. If it ever isn't (spectating dense fights), the lever is server-side — FOV-filtered delta fan-out is already spec'd as a future phase — with no client API change (the store already consumes diffs it computes itself).
- **Smoothness between 1 Hz world ticks:** entity tweens are timed to the observed inter-frame gap; the game reads as deliberate, weighty motion (fitting the fiction) rather than as lag. Player's own actions round-trip much faster than the tick, so *you* always feel immediate.
- **Reconnects:** `WithAutomaticReconnect` + rejoin flow (the session's world binding survives brief drops; on new session, the lobby re-join path re-anchors cleanly).

## Trust & failure model

- The client is untrusted by construction — it can only invoke player-profile tools on its own session; world-edit/admin tools are profile-blocked at the hub (`AgentToolProfile.Player`), and the management plane runs on a different hub/REST surface with its own auth.
- Server restarts: Orleans grains reactivate from persisted state (map state, content/ECA configs re-compile on activation — the shipped reactivation path); clients reconnect and rejoin. With memory-only storage today, a full host restart means fresh worlds — acceptable for the sample's session-length runs.
- Client crashes/disconnects: the hub removes the player character from the map (others see them leave); rejoining spawns fresh at the dock.
