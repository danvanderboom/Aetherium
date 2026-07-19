# Aetherium Arcade — a first-class generalized Unity client

**Status: proposed design, not yet implemented.** This document proposes the capability set that turns
the Aphelion Unity work into a *generalized* Aetherium client: one Unity app that connects to a server,
shows the shelf of declaratively-defined demo games, and lets the player play any of them — an arcade
whose catalog grows every time a new YAML bundle lands in `Data/Games/`, with **zero client releases per
new game**.

It builds directly on two shipped foundations and cites them throughout:

- the **Aphelion / unity-sample initiative** (`docs/design/unity-sample/`) — the two-layer client
  (`Aetherium.Client` core + `com.aetherium.unity` package), the Aphelion sample at its "First Light"
  playtest state, and the engine-gaps ledger (`docs/design/unity-sample/engine-gaps.md`);
- the **grid-topologies seam** (`docs/grid-topologies.md`) — square/hex/tri/H3 as per-world data,
  P0–P7 built and engine-proven, with the client layout math already mirrored in
  `Aetherium.Client/Contracts/GridCellLayout.cs`.

When implementation begins, this document should split into a suite (sibling files in this directory)
and pair with an OpenSpec change (`openspec/changes/add-arcade-client/`) carrying spec deltas against
`client-server-communication` and `game-management-grain`.

---

## 1. Vision

> Launch the app. It connects to an Aetherium server and shows a shelf of games — Aphelion, Emberfall,
> Hexhaven, Neonveil, Trigrove, and whatever was added last week. Pick one. You're playing in seconds,
> alongside anyone else who picked the same world. Nobody rebuilt or updated the client for any of it.

Five properties define "first-class generalized client":

1. **Catalog-driven.** The game list is discovered from the server at runtime. A new bundle dropped in
   `Data/Games/` appears on the shelf on next registry load, playable immediately.
2. **Topology-generic.** Square, hexagon, and triangle worlds render and play correctly; the seam that
   makes H3/sphere possible later is respected (no square assumptions reintroduced).
3. **Presentation always resolves.** Every game is *renderable* the moment it exists (procedural
   theming from wire data), and *beautiful* when someone invests in a curated theme (Aphelion is the
   flagship). Unknown content degrades loudly, never invisibly.
4. **Interaction is discovered, not baked.** Input, action menus, and multi-option selection derive
   from `ListAvailableTools` and per-frame affordances, so a game with novel verbs is playable without
   client changes.
5. **Server-authoritative, perception-pure.** The client stays a frame consumer with local
   intelligence (anchoring, memory, tweening). No game logic client-side — this is precisely what makes
   one-client-many-games tractable.

**Non-goals (v1):** real-time/twitch gameplay; user-generated content or asset marketplaces;
server-served binary assets (deferred — see §5, Tier 2); auth hardening beyond the existing adaptive
policy; WebGL/mobile targets; retiring the console client (it remains the reference generic renderer).

---

## 2. Where we stand (baseline inventory)

Everything below is shipped on `develop` unless noted. This is the platform the arcade builds on.

### Client stack (from the unity-sample initiative)

| Layer | Location | State |
|---|---|---|
| Pure-.NET core | `Aetherium.Client/` | Real SignalR (`AetheriumConnection`), `PerceptionStore` (anchoring, diffing, memory), `ToolClient` (typed verbs over `ExecuteTool`), `LobbyClient` (`ListWorlds`/`GetWorldInfo`/`JoinWorld`…), mirror DTOs pinned by build-breaking drift tests, 10 in-proc integration tests that boot the real server. |
| UPM package | `clients/unity/com.aetherium.unity/` | `AetheriumClientBehaviour`, `MainThreadDispatcher`, `GridMapView` (3D terrain prefabs + memory dimming), `EntityViewRegistry` (spawn/tween/vanish + player avatar), `ThemeAsset` (content-id → prefab, magenta-capsule fallback). |
| Flagship sample | `samples/unity/Aphelion/` | Unity 6 URP project at the committed **First Light** playtest state: maze decks as 3D prefabs, five creatures bound to models, WASD/arrows/Space input, camera rig. |
| Legacy prototype | `Aetherium.Unity/` | Superseded; retires at M1 per `docs/design/unity-sample/milestones.md`. Holds the only gamepad + usage-option-selection UI code (to be ported forward, §7). |

### Games as data

- Six bundles in `Data/Games/`: **aphelion** (square/maze), **emberfall** (square/maze, uses every
  config section), **hexhaven** (hex/hex-caves), **neonveil** (square/rooms-and-corridors),
  **trigrove** (tri/perlin-terrain), **nightjar** (hex/hex-caves — the cat-burglar heist added by
  this design, §C.6).
- `GameDefinitionLoader`/`Validator`/`Registry` (`Aetherium.Server/Games/`) load and validate bundles
  (strict YAML, cross-section referential integrity, topology×generator compatibility).
- `ContentCompiler` (`Aetherium.Server/Content/ContentCompiler.cs`) turns YAML creatures/items into
  entities and registers per-creature **`Creature:<id>`** tile types carrying glyph/color settings —
  the semantic vocabulary clients theme against.

### Server surface

- **Player-facing** (`GameHub`, `/gamehub`, adaptive auth): `ListWorlds`, `GetWorldInfo`,
  `JoinWorld(worldId, mapId?)` (fully implemented — swaps the session onto the grain-authoritative
  shared map, `GameHub.cs:452–534`), `ExecuteTool`, `ListAvailableTools` (Player tool profile enforced
  at the boundary), `UsePortal`. Multiplayer on a shared map is shipped: deltas fan out per-session as
  FOV-filtered `ReceivePerceptionUpdate` frames.
- **Admin-only** (API-key REST, `ManagementController`): `GET /api/management/games`,
  `POST /api/management/games/{id}/instances`. **There is no player-facing way to list game
  definitions or create an instance** — this is engine gap **G6** and the arcade's central server ask.

### Topology

- Seam P0–P7 built; movement, FOV, light, melee, ECA adjacency all resolve through `IGridTopology`
  (verified engine-level, e.g. `HexTopologyTests.Engine_TryMoveSteps_Walks_Hex_Edges`).
- Wire: `PerceptionDto.Topology` + `SelfCellParity` (tri); `Visuals` keys stay `"relX,relY,relZ"`
  (axial q/r deltas on hex). `GridCellLayout.CellLayoutPosition` is mirrored client-side and already
  consumed by `GridMapView` and `EntityViewRegistry` — hex/tri *rendering* is structurally supported.
- **Known limits** (the arcade's hex work list, §6): client anchor advancement is square-only
  (`ToolClient.AnchoringIsExact`, `Aetherium.Client/ToolClient.cs:224`); `RotateTool` hardcodes ±90°
  presets (hex needs 60° via `TurnStepDegrees` — flagged as deferred work in
  `Aetherium.Server/Agents/Tools/Movement/RotateTool.cs`); **no hex world has ever been driven through
  the live grain/SignalR/client stack** — all hex coverage is unit/engine-level.

---

## 3. Architecture overview

```
┌─────────────────────────── Unity: Aetherium Arcade app ───────────────────────────┐
│  Arcade shell (new)                      Game surface (evolved from Aphelion)     │
│  ┌──────────────┐  ┌───────────────┐     ┌─────────────┐  ┌────────────────────┐  │
│  │ Server picker │→│ Game shelf UI │──→  │ GridMapView │  │ EntityViewRegistry │  │
│  └──────────────┘  └───────────────┘     └──────┬──────┘  └─────────┬──────────┘  │
│                        │  ListGames               └── ThemeResolver ─┘            │
│                        │  PlayGame                (procedural │ curated │ hints)  │
│  ┌─────────────────────┴──────────────────────────────────────────────────────┐  │
│  │ HUD + input (new): tool registry ← ListAvailableTools; affordance/option UI │  │
│  └────────────────────────────────────────────────────────────────────────────┘  │
├──────────────────── com.aetherium.unity (package, unchanged role) ────────────────┤
├──────────────────── Aetherium.Client core (protocol, anchoring) ──────────────────┤
└──────────────────────────────── SignalR /gamehub ─────────────────────────────────┘
                                        │
        ListGames / PlayGame (NEW, §4)  │  JoinWorld / ExecuteTool / frames (existing)
                                        ▼
   GameDefinitionRegistry → GameManagementGrain → WorldGrain/GameMapGrain (existing)
```

The load-bearing choice: **the arcade is a shell around the existing two-layer client, not a new
client.** Everything below the shelf UI — connection, perception, anchoring, rendering, theming
hooks — is the shipped package; the arcade adds discovery, entry, generic theming, and generic
interaction UI on top.

---

## 4. Capability A — Arcade server surface: discovery and entry

### A.1 Player-facing game catalog

Add to `GameHub` (and mirror in `LobbyClient`):

```csharp
Task<List<GameDefinitionSummaryDto>> ListGames();          // registry summaries, player-visible
Task<GameShelfEntryDto> GetGameDetails(string gameId);     // summary + live instance info
```

`GameDefinitionSummaryDto` (`Aetherium.Model/Games/GameDefinition.cs`) already carries
Id/Name/Version/Description/Tags. Extend it (append-only) with the shelf's needs:

```csharp
[Id(5)] public string Topology { get; set; } = "square";   // shelf badge; from World.Topology
[Id(6)] public int MaxPlayers { get; set; }
[Id(7)] public Dictionary<string, string> Presentation { get; set; } = new();
        // optional cosmetic hints: "accentColor", "coverArtId" — same open-string-dict
        // pattern as TileTypeDto.Settings; empty is fine
```

`GetGameDetails` adds live data the shelf card shows on focus: running instance count, joinable
instance count, total players in-game (from `ListGameInstancesAsync` filtering, already implemented on
`GameManagementGrain`).

### A.2 The arcade verb: `PlayGame`

The player should not manage instances. One verb does the right thing:

```csharp
Task<JoinWorldResult> PlayGame(string gameId, PlayGameOptionsDto? options = null);
```

Server-side resolution, in order:

1. Find `Active` instances of `gameId` (by `GameDefinitionId`) with player headroom
   (`joined < MaxPlayers`), **joinable per policy** (below). If found → existing `JoinWorld` flow.
2. Otherwise, if the bundle permits player instantiation → `CreateGameInstanceAsync` (existing
   mapper path), wait for `Active`, then join.
3. Otherwise → `JoinWorldResult.Fail("...")` with a human-readable reason for the shelf UI.

`PlayGameOptionsDto` (all optional): `PreferPrivate` (force step 2 even when step 1 matches),
`InstanceName`. Nothing else in v1.

This subsumes engine gap **G6** and follows its ledger recommendation: instance creation delegates to
the management grain, gated by **per-bundle data**, keeping operators in control of what players can
spin up.

### A.3 Per-bundle instancing policy (data, not code)

New optional `game.yaml` section, honoring the per-world-data principle:

```yaml
instancing:
  playerEntry: shared        # shared (default) | private | disabled
  allowPlayerInstances: true # may PlayGame create instances? default false
  idleShutdownMinutes: 30    # 0 = never; reaping policy, see A.4
```

- `shared`: step-1 reuse applies — players pool into instances below capacity (the arcade default;
  makes the demo social).
- `private`: every `PlayGame` creates a fresh instance (roguelike-style runs, e.g. Neonveil's
  permadeath fits this).
- `disabled`: listed on the shelf but joinable only via explicit `ListWorlds`/`JoinWorld` of
  operator-created instances (kept for curated events).

Validator additions: enum values, non-negative minutes, `allowPlayerInstances: true` required when
`playerEntry: private`.

### A.4 Instance reaping (required before a public demo)

Today game-instance worlds tick forever (`WorldTickService` ticks every `Active` world; the only
shutdown is admin `DELETE`). An arcade that creates instances on demand needs the reverse flow:

- Track `LastPlayerActivityAt` on the world (updated on join/leave/tool execution — the map grain
  already sees all three).
- A periodic sweep (the tick service or a directory-level timer) transitions worlds with zero joined
  sessions past `idleShutdownMinutes` through the existing `ShutdownWorldAsync` path.
- Persisted-state worlds survive reaping by design (Orleans state remains; `PlayGame` can recreate).

The dungeon-instance system (`Aetherium.Server/Instances/DungeonInstanceGrain.cs`) already models
`LastActivityAt` + idle deactivation — reuse its shape, not a new mechanism.

### A.5 Server identity handshake (small, do it now)

There is no protocol version on the wire; the client library's safety net is build-breaking drift
tests, which protect *lockstep* builds but not an arcade binary meeting an older/newer server. Add one
cheap method:

```csharp
Task<ServerInfoDto> GetServerInfo();   // { ServerVersion, ProtocolCapabilities: ["interoception", "topology", ...] }
```

The arcade gates features on capability strings rather than version arithmetic (e.g. hides the HUD
vitals panel when `"interoception"` is absent). Append-only forever.

---

## 5. Capability B — Presentation that always resolves

The generalization problem in one sentence: **`ThemeAsset` is hand-authored per game** (Aphelion's
bootstrap binds its five creature ids to Quaternius models), but the shelf holds games nobody authored
a theme for — and next month's game doesn't exist yet.

Design: a **three-tier resolution chain**, each tier falling through to the next, so every content id
in every game resolves to *something visible* — and the tiers are exactly the investment ladder a new
game climbs.

### Tier 0 — Procedural theme (always available)

Derive visuals from what the wire already carries. Every tile type arrives with
`Settings["MapCharacter"/"ForegroundColor"/"BackgroundColor"]` (terrain authored in bundles;
creatures via `ContentCompiler.EnsureTileType`). The procedural theme renders:

- **Terrain**: a flat cell mesh (per-topology footprint, §6) tinted by `ForegroundColor`, with a
  category shape heuristic keyed on tile-type *name* conventions already used across all five bundles
  (`Wall`/`Door`/`Water`/`Floor`…): walls extrude, doors get a frame, liquids get a shader, everything
  else is floor. Unrecognized names render as tinted floor — playable, if plain.
- **Creatures/items**: a simple body primitive tinted by color, with the glyph billboarded above it —
  the console client's legibility, in 3D.

This is the console client's guarantee ("any game renders day one") promoted to the 3D client. It is
also the *only* tier Hexhaven, Trigrove, Neonveil, and Emberfall need to be shelf-playable at A0.

### Tier 1 — Curated themes (the flagship path)

A `ThemeCatalog` ScriptableObject maps `gameId → ThemeAsset` (plus tag-keyed genre defaults:
`sci-fi`, `fantasy` → shared prefab sets). Resolution per content id: curated theme → genre default →
Tier 0. Aphelion's First Light theme becomes the first catalog entry, unchanged in authoring model —
this is deliberately **not** a rewrite of `ThemeAsset`, only a lookup layer above it.

The magenta `LoudPlaceholder` remains the terminal fallback for a *content id the theme claims but
cannot resolve* — loud beats invisible — but Tier 0 means it should now be rare.

### Tier 2 — Bundle-shipped presentation hints (deferred, sketched)

Engine gap **G12**'s ledger recommendation is to defer server-shipped presentation, and this design
keeps that call: the semantic-id + client-side-theme model is cleaner, and `TileTypeDto.Settings` is
already an open string dictionary if a hint channel is ever wanted. The sketch, so bundles can grow
toward it without schema churn:

```yaml
# presentation.yaml (future, optional, purely advisory)
tiles:
  Wall: { archetype: wall, palette: "#5a6a7a" }
creatures:
  scrap-mite: { archetype: swarm-small, palette: "#c8a04a" }
```

Hints flow through `Settings` (e.g. `Settings["Archetype"]`), improving Tier 0's shape heuristic from
name-guessing to declared semantics. Actual downloadable binary assets (sprites/meshes served by the
server) remain out of scope until a second visual client or a modding story demands them.

---

## 6. Capability C — Topology-generic client (hexagon first)

The seam is built and engine-proven; the client work is finishing the last mile and then **actually
running a hex world through the live stack for the first time**.

### C.1 Rendering

- **Cell placement** is already generic: both views call
  `GridCellLayout.CellLayoutPosition(topology, relX, relY, selfCellParity)` and map to Unity space as
  `(x·cellSize, z·cellSize_level, −y·cellSize)`. Nothing to change.
- **Cell footprint meshes** are new: themes (and Tier 0) need per-topology floor/wall footprints —
  square quad; pointy-top hexagon (vertices at 30°+k·60°, matching the axial embedding
  `x = q + r/2, y = (√3/2)·r`); up/down triangle selected by `CellParity` (client recovers parity from
  `SelfCellParity` + `relX+relY`, already in `GridCellLayout.CellParity`). `ThemeAsset` gains an
  optional per-topology prefab variant slot; Tier 0 generates the meshes procedurally.
- **Camera**: the orbit rig follows `HeadingDegrees` already; hex's 60° facings need no camera change
  (degrees are the engine-wide source of truth — `docs/grid-topologies.md`).

### C.2 Anchor advancement on hex (core library work)

`ToolClient.AdvanceAnchorForMove` currently bails on non-square topology
(`AnchoringIsExact`, `ToolClient.cs:224–228`) — on hex, rendering works but remembered terrain cannot
keep client-space identity across moves. Fix in the core, mirroring the server's resolution exactly:

- Mirror the hex step table (`HexTopology.StepTable`: six `(dq,dr)`/heading pairs) and the
  `AngularEdgeSelection` tie-break rules into `Aetherium.Client` (tri's table + parity rules likewise).
- Advance the anchor with the same snap-heading → select-edge → step logic the server applies in
  `TryMoveSteps`.
- Pin equivalence with a **drift test** (the established pattern): client-mirrored resolution must
  match `HexTopology.ResolveRelative` across all headings × F/B/L/R × several cells, to the exact cell.

H3 stays excluded from exact anchoring (its steps aren't a lattice table); that matches the seam's
"H3 is an implementation, not a redesign" posture and blocks nothing on the shelf today.

### C.3 Rotation granularity (server fix, already flagged)

`RotateTool` hardcodes `clockwise ? 90 : -90` with an explicit comment deferring per-topology presets.
Implement the flagged fix: resolve the preset server-side from the player's cell via
`Topology.TurnStepDegrees(cell)` (60 on hex, 120 on tri, 90 on square; per-cell for H3 pentagons).
Free-form `rotate degrees:` already works and is unaffected. Without this, hex players turn in 90°
steps that fight the 60° lattice (movement stays legal — `ResolveRelative` snaps — but facing feels
broken).

### C.4 Input on hex

The move tool's schema is topology-independent by design (same `F/B/L/R/N/E/S/W` vocabulary
everywhere; L/R resolve to the ±60° edges on hex; direction indices are banned from the wire). So the
input layer needs no per-topology verbs — only presentation awareness:

- Keyboard: `W/S` forward/backward, `A/D` (or `Q/E`) turn one step (60° on hex once C.3 lands),
  strafe on `L`/`R` relative moves. Compass keys (N/E/S/W) remain but note E/W are exact hex edges
  while N/S snap — the HUD compass should show the six real facings on hex.
- Gamepad: left stick → nearest relative direction (the legacy project's mapping, ported in §7);
  shoulders turn by one step.

### C.5 Live validation ladder (the "needs to be tested" answer)

Every hex/tri claim today rests on unit/engine tests; the full grain → `PerceptionService` → SignalR →
client path has never carried a hex frame. Close that in order, cheapest first:

1. **In-proc integration tests** (`Aetherium.Client.Tests/InProcServerIntegrationTests.cs` pattern):
   boot the server, create a **hexhaven** instance via the management grain, `JoinWorld`, assert
   `Topology == "hex"` on the live frame, walk all six edges via rotate+forward, assert anchoring
   (post-C.2) and memory coherence; repeat blocked-move and FOV-shape assertions. A trigrove twin
   asserts `SelfCellParity` flips correctly.
2. **Console self-test**: extend `ConsoleUiSelfTest` to run its script against a hexhaven instance —
   the honeycomb renderer (`ClientConsoleMapView` + `GridCellLayout` stagger) gets its first live
   exercise, catching server-side key-generation bugs before Unity is involved.
3. **Unity playtest**: hexhaven on the shelf, played in the arcade with Tier-0 theming.

### C.6 The hex flagship: Nightjar

**Aphelion stays square.** Its fiction (station decks, salvage crawl) reads as a rectilinear maze, its
First Light theme is authored against square footprints, and bending it to hex would muddy the flagship
we already have working. Instead, hex gets a **purpose-built game** — `Data/Games/nightjar/` — designed
for the six-way lattice from the start.

**Nightjar** is a co-op cat-burglar heist: a crew works the vaulted undercroft of a private museum
where every gallery is a six-sided chamber that opens six ways. The hex adjacency *is* the gameplay —
six approaches, six sightlines, six ways for a patrol to round on you — which is exactly why it earns
its own game rather than a reskin. It also deliberately occupies a different niche from **Neonveil**
(cyberpunk *hacking*): Nightjar is physical infiltration, so "hacker vs. cat burglar" becomes two
distinct games rather than one theme twice.

- **Topology/generator**: `topology: hex` on `hex-caves` (the only shippable hex generator today —
  `maze` and `rooms-and-corridors` don't support hex, so a rectilinear-gallery generator would be new
  work). `hex-caves` is fictioned as the honeycomb of stone vaults beneath the manor; a bespoke
  "gallery" generator is a later nicety, not a blocker.
- **Content (v0.1, shipped alongside this design)**: a threat ladder of `housecat` (yowls, raises no
  real danger but sells the stealth fiction) → `nightwatch` → `guard-hound` (fast, flanks) → `warden`
  → `clockwork-sentry`, with a blackjack (weapon), nerve-tonic (heal), lockpicks/keys, and the haul
  (`sapphire`). Death is co-op down/respawn at the fence's safehouse with partial drop-on-death — get
  pinched, lose part of the haul. All `wander-melee` for M0 (the only shipped behavior preset); true
  patrol/alarm behavior is a natural follow-up once a `patrol` preset lands.

Nightjar is the **curated-hex (Tier-1) flagship** and the live-validation vehicle for C.1–C.5, where
hexhaven exercises the procedural (Tier-0) path. Building a *new* game to prove hex — rather than
mutating Aphelion — is itself the strongest demonstration of the arcade thesis: a whole new game is a
YAML bundle plus a theme, no engine or client release.

---

## 7. Capability D — Discovery-driven interaction: input, HUD, options

The core already surfaces everything; the presentation layer is unbuilt. Three pieces:

### D.1 Runtime action registry

On join, call `ListAvailableTools` (already wrapped in `ToolClient`) and build the input map from it:

- **Core verbs** (`move`, `rotate`, `attack`, `pickup`, `use`, `open`, `close`, `changelevel`…) bind
  to their standard keys/buttons *when present*. A game that omits a verb simply has no binding — no
  dead keys, no client knowledge of which games have which verbs.
- **Everything else** (game-specific tools, M1 abilities) lands in a generic action menu/radial built
  from `ToolInfoDto` (label from `ToolId`/`Description`, prompts generated from `ParameterSchema` —
  enum-valued params become option lists, the common case).
- Frames' `Affordances` drive contextual prompts ("Press E — Open door") — the affordance already
  names action + target.

### D.2 Multi-option selection UI

`ToolInfoDto.UsageOptions` / `AffordanceDto.UsageOptions` → selection list → re-invoke with `usageId`.
The legacy project has the complete interaction model to port: the option-selection state machine and
gamepad navigation in `Aetherium.Unity/Assets/Scripts/Rendering/PlayerController.cs` (nav/confirm/
cancel, tested in `OptionSelectionTests.cs`/`GamepadInputTests.cs`). Porting these forward into the
package is the last value locked in the legacy project; its retirement (already scheduled M1) follows.

### D.3 HUD from interoception + inventory

`PerceptionDto.Interoception` (HP/max, statuses, pools, cooldowns) is live on the wire and verified
through the hub push, but nothing renders it. The HUD panel set, all driven by existing DTO fields:
vitals (interoception), inventory (`Inventory`/`VisibleItems`), affordance prompts, downed/revive/died
flow (`ReceiveDowned`/`ReceiveRespawn`/`ReceiveDied` events already surfaced by
`AetheriumClientBehaviour`), and the compass (topology-aware, §C.4). Gate the vitals panel on the
`"interoception"` capability string (§A.5).

---

## 8. Capability E — The arcade app itself

### E.1 UX flow

```
Boot ─→ Server picker ─→ Shelf ─────────→ Loading ─→ In-game ─→ (pause) ─→ Shelf
        localhost default   ListGames        PlayGame    frames      leave world
        + recent servers    cards w/ live    join-or-create
                            counts, badges
```

- **Shelf cards**: name, description, tags, topology badge (□/⬡/△), live "N playing" count, curated
  vs procedural-theme indicator. Ordered: curated first, then alphabetical.
- **Failure surfaces**: `PlayGame` failure reasons render on the card (world capacity, instancing
  disabled); connection loss returns to the picker with the automatic-reconnect state shown.

### E.2 Project structure and Aphelion's role

The arcade is a **game-agnostic client app**; Aphelion is a **game** (bundle + curated theme). Today
those are conflated in one sample project. Restructure:

```
clients/unity/com.aetherium.unity/     (package — unchanged role, gains D.1–D.3 + theme chain)
clients/unity/Arcade/                  (NEW: the arcade app — shell UI, ThemeCatalog, generic Game scene)
samples/unity/Aphelion/                (becomes: Aphelion's curated ThemeAsset + any Aphelion-specific
                                        polish, consumed by the Arcade via the ThemeCatalog; its
                                        standalone scene remains as the minimal-integration example)
```

The First Light scene's bootstrap logic (theme binding, camera, lighting) migrates into the Arcade's
generic Game scene; `AphelionSceneBootstrap`'s hand-wiring becomes the Aphelion catalog entry. The
legacy `Aetherium.Unity/` retires on schedule once D.2 ports its option/gamepad UI.

(Alternative considered: grow the arcade *inside* the Aphelion project. Rejected — it bakes the
flagship game into the generic client, exactly the coupling this design exists to remove.)

### E.3 Distribution

Unchanged from the unity-sample design: the package vendors `Aetherium.Client.dll` via
`scripts/pack-unity-client.ps1`. The arcade adds no new distribution mechanism in v1; tagging
`com.aetherium.unity` v0.1 (already an M1 item) plus a tagged Arcade project export is the demo
deliverable.

---

## 9. Testing & verification strategy

| Layer | What | Vehicle |
|---|---|---|
| Protocol | New DTOs (`GameShelfEntryDto`, `ServerInfoDto`, summary extensions) round-trip | Extend `ProtocolDriftTests` (build-breaking, existing pattern) |
| Topology math | Client-mirrored hex/tri step tables ≡ server resolution | New drift tests vs `HexTopology`/`TriangleTopology.ResolveRelative` (§C.2) |
| Server surface | `ListGames` filtering, `PlayGame` join-or-create matrix (shared/private/disabled × headroom), reaping sweep | New `Aetherium.Test` fixtures against `GameManagementGrain` + hub |
| Live stack | Hex/tri worlds through grain→SignalR→client for the first time | In-proc integration tests on hexhaven/trigrove instances (§C.5.1) |
| Console | Honeycomb/parity rendering against a live hex world | `ConsoleUiSelfTest` hexhaven run (§C.5.2) |
| Unity | Theme resolution chain, procedural meshes, action registry, option UI | Package EditMode tests + PlayMode smoke on a **MockPerceptionProvider** replaying recorded square *and hex* frames — building the mock provider + frame recorder promised in `unity-client-library.md` (still unbuilt) is a prerequisite here |
| Acceptance | All six bundles playable from the shelf; Nightjar playable end-to-end on hex | Manual playtest checklist per bundle, per milestone |

OpenSpec traceability applies: each new spec requirement carries its `**Verified by:**` test line per
`openspec/project.md`.

---

## 10. Milestones

Sequenced to interleave with the existing Aphelion ladder (M1 "Reclaimer Kit", M2 "The Long Dark")
rather than replace it — the arcade milestones are client+surface work; Aphelion milestones remain
game work.

### A0 — "The Shelf" (arcade exists; every square game playable)

- Server: `ListGames`/`GetGameDetails`/`PlayGame` + `instancing:` section + validator rules (G6);
  `GetServerInfo`; idle reaping (A.4).
- Client: Arcade project + shelf UI + server picker; `LobbyClient` additions; Tier 0 procedural theme
  + `ThemeCatalog` with Aphelion as the curated entry; generic Game scene.
- Exit: fresh checkout → server up → arcade shows six games → Aphelion (curated) and
  Emberfall/Neonveil (procedural) each playable to death/respawn, two clients sharing one instance.

### A1 — "Six Ways" (hex is real)

- Core: hex/tri anchor advancement + drift tests (C.2). Server: `TurnStepDegrees` rotate presets
  (C.3). Package: per-topology cell footprints (C.1).
- Validation ladder C.5 complete (in-proc hex tests → console self-test → Unity playtest).
- `nightjar` bundle (shipped with this design) gets its curated (Tier-1) theme; hexhaven and trigrove
  playable with Tier 0.
- Exit: hexhaven, trigrove, and Nightjar playable in the arcade; hex turn/facing feel correct
  (60° steps); memory/anchoring coherent on hex walks.

### A2 — "Any Game, Properly" (interaction generalizes)

- Action registry + generic action menu (D.1); option-selection UI + gamepad ported from legacy
  (D.2); HUD with interoception/inventory/affordances/downed-flow (D.3). Legacy project retires
  (fulfilling the M1 item).
- Theme polish: genre-default themes; Tier-2 hint sketch revisited only if a concrete need emerged.
- Exit: a *new* sixth bundle authored during A2 (as a test, not shipped content) is playable —
  discovered, themed procedurally, all its verbs reachable — with zero client edits. That sixth-bundle
  test is the definition of done for "generalized client."

**Nightjar polish** — a bespoke hexagonal-gallery generator (replacing the `hex-caves` stand-in) and a
`patrol`/alarm behavior preset (replacing `wander-melee`) — slots after A1 as normal game/engine
slices, once hex feel is validated. **H3/sphere** stays gated on its own prerequisites
(`docs/h3-topology.md`: sphere-aware perception keys, h3 worldgen) and is unaffected by this design.

---

## 11. Risks & open questions

**Risks**

1. *First live hex transit.* `PerceptionService` key generation, FOV shape, and delta fan-out have
   never carried hex — C.5's ordering (in-proc tests before any UI) exists to surface engine bugs at
   the cheapest layer. Budget A1 assuming some server fixes fall out.
2. *Instance sprawl before reaping lands.* A0 intentionally ships reaping in the same milestone as
   `PlayGame`; do not demo publicly with creation enabled and reaping absent.
3. *Procedural theme legibility.* Tier 0 could look bad enough to undermine the demo story. Mitigate
   with the archetype heuristic + one shared "generic" material set designed once, well — and lean on
   the shelf's curated/procedural indicator to set expectations.
4. *Unreconciled branch tips.* Two commits on `origin/add-interoception-channel` (light-gradient pool
   rendering, light wall faces) sit ahead of develop; reconcile before A0 branches, or the client work
   forks.
5. *Auth posture.* `PlayGame` creates server load on an anonymous-in-dev hub. Fine for demos; revisit
   rate/instance limits per connection before any internet-exposed deployment.

**Open questions (decide at A0 kickoff)**

1. Shelf identity: is `GameDefinitionSummaryDto.Presentation` enough for cover art (client-side
   lookup by `coverArtId`), or do we accept image bytes over the hub? (Recommend: client-side lookup;
   no binary over SignalR.)
2. Does `PlayGame` on a `shared` bundle ever create instance #2 (when #1 is full) in v1, or fail with
   "world full"? (Recommend: create up to a per-bundle `maxInstances`, default 1, so demos stay
   predictable.)
3. Where does the recent-servers list persist (PlayerPrefs vs config file)? Trivial, but decide once.
4. Do we tag/publish the package before or after A0? (Recommend: after A1 — the anchor API surface
   changes for hex; tag when it settles.)

---

## Appendix — interface sketches

New/changed surfaces in one place; all names provisional.

```csharp
// GameHub additions (player-facing; Player tool profile unaffected)
Task<List<GameDefinitionSummaryDto>> ListGames();
Task<GameShelfEntryDto>              GetGameDetails(string gameId);
Task<JoinWorldResult>                PlayGame(string gameId, PlayGameOptionsDto? options = null);
Task<ServerInfoDto>                  GetServerInfo();

public class GameShelfEntryDto {           // summary + live occupancy
    public GameDefinitionSummaryDto Summary;
    public int RunningInstances; public int JoinableInstances; public int PlayersInGame;
}
public class PlayGameOptionsDto { public bool PreferPrivate; public string? InstanceName; }
public class ServerInfoDto { public string ServerVersion; public List<string> ProtocolCapabilities; }

// LobbyClient (Aetherium.Client) — mirrors of the four methods above, same mirror-DTO + drift-test pattern

// com.aetherium.unity — theme resolution chain
public interface IThemeSource {            // chain: ThemeCatalog(curated) → GenreDefaults → ProceduralTheme
    GameObject? ResolveTerrain(string tileTypeName, string topology);
    GameObject? ResolveCreature(string creatureTypeId);
    GameObject? ResolveItem(string itemId);
}

// Aetherium.Client — hex anchoring (mirrors HexTopology.StepTable + AngularEdgeSelection tie-breaks)
internal static class ClientTopologySteps {
    public static (int dq, int dr, int newHeading)? ResolveRelative(
        string topology, int cellParity, int headingDegrees, string relativeDirection);
}
```

```yaml
# game.yaml — new optional section (validator-enforced)
instancing:
  playerEntry: shared            # shared | private | disabled   (default: shared)
  allowPlayerInstances: true     # default: false
  maxInstances: 1                # cap for shared join-or-create  (default: 1)
  idleShutdownMinutes: 30        # 0 = never                      (default: 30)
```
