# Vision & Design: H3 Spherical Worlds and Sphere-Aware Procedural Generation

**Status:** Living design. **Slice 1 is built and green, and the planet is walkable with real sight** —
the worldgen pipeline is topology-aware, a sphere-native `H3TerrainGenerator` terraforms a whole planet
at a chosen H3 resolution, a switchable sci-fi planet bundle (`aphelion-h3`) ships alongside the square
Aphelion, **player-relative perception works on the sphere** (a `gridDisk` viewport with `cellToLocalIj`
keys), **and field-of-view + lighting are sphere-native** — mountains/forests/walls occlude line of
sight, the directional cone restricts the forward arc, and light sources form pools with darkness
shrinking the view, all via the same tested `ObstructsView`/topology-ray model as the square grid. A
player can join and walk the planet and only sees what they should. Sphere-native settlements/rivers/
roads, transit, and economy wiring are the phased roadmap in [§7](#7-roadmap).
**Depends on / builds on:** [`h3-topology.md`](../h3-topology.md), [`grid-topologies.md`](../grid-topologies.md),
[`economy-simulation.md`](../economy-simulation.md), [`transit-networks.md`](transit-networks.md).
**Audience:** engine maintainers, OpenSpec proposal authors, game/campaign designers.

---

## 1. Vision

Aetherium already tiles a sphere. [`H3Topology`](../../Aetherium.Server/Topology/H3Topology.cs) — a
hexagon-and-pentagon tiling of any planet, moon, or planetoid at 16 nested resolutions — has been
built, registered as `"h3"`, and passes the full topology-invariant harness. What it lacked was a
*game on it*: something to generate a planet's surface and a world to switch into. This design adds
the first of both.

**The parallel sci-fi planet.** The square [Aphelion](../../Data/Games/aphelion/game.yaml) is a co-op
salvage crawl through a hibernating mega-station — a confined 48×48×3 arena. Its natural companion is
the world the station orbits: **Aphelion Prime**, the whole planet, rendered as a single H3 sphere at
**resolution 4 (~288,122 cells)**. The square Aphelion is unchanged; Aphelion Prime
([`aphelion-h3`](../../Data/Games/aphelion-h3/game.yaml)) is a **switchable alternative**, chosen the
same way every other tiling is — one line of bundle data (`world.topology: h3`). No station, no
walls, no border: an open planetary surface you roam end to end, wrapping around the globe.

**Why a sphere (and not a bigger square).** A square map has edges, a date-line, and pole distortion;
a planet has none of these. On H3 you can sail west and arrive from the east, distance is great-circle
distance, and the 12 pentagons are the only seams — and they are just ordinary cells to gameplay. For
the systems we most want to exercise next — **economies, trade, and transportation networks** — a
finite, borderless surface with real distance between places is exactly the substrate that makes
logistics *mean* something (see [§3](#3-is-288k-cells-a-big-enough-world)).

---

## 2. How do we make procedural generation H3- (and sphere-) aware?

The runtime engine was already fully topology-abstracted; **worldgen was the half that still assumed a
rectangle.** Every generator scanned `for x in 0..W, for y in 0..H` and sampled 2-D noise at `(x, y)`.
The [`h3-topology.md`](../h3-topology.md) design *assumed* a `GeneratorContext.Topology` seam existed;
it did not. Slice 1 closes exactly that gap. Five moving parts, smallest to largest:

### 2.1 Thread the tiling into worldgen (the missing seam)
`GeneratorContext.Topology` (an `IGridTopology`) and `WorldGenerationRequest.Topology` (the name) now
carry the world's tiling into generation, resolved once in
[`WorldGenerationOrchestrator`](../../Aetherium.Server/WorldGen/WorldGenerationOrchestrator.cs) and
populated from the bundle's `world.topology` all the way through
[`GameMapGrain.InitializeAsync`](../../Aetherium.Server/MultiWorld/GameMapGrain.cs). It is also
persisted on the `WorldRecipe`, so a grain that regenerates on reactivation rebuilds on the **same**
tiling rather than silently reverting to square. A generator no longer has to guess what it is building.

### 2.2 Sample noise on the sphere, in 3-D
A square map decides terrain by thresholding **2-D** Perlin noise at a cell's `(x, y)`. A sphere has no
such plane — mapping `(latitude, longitude)` to a 2-D noise domain tears at the date-line and pinches
at the poles. The fix is to sample **3-D noise over each cell's centre *unit vector*** on the sphere:
[`PerlinNoise`](../../Aetherium.Server/WorldGen/Algorithms/Noise/PerlinNoise.cs) gained
`Noise(x,y,z)` / `FractalNoise(x,y,z)` (Ken Perlin's improved-noise 3-D gradient over the same seeded
permutation table). Because the domain is a point *on* the sphere, the field is seamless everywhere —
no wrap, no pole, and the pentagons are unremarkable.

### 2.3 A sphere-native terrain generator
[`H3TerrainGenerator`](../../Aetherium.Server/WorldGen/Generators/Outdoor/H3TerrainGenerator.cs)
(`SupportedTopologies = ["h3"]`) enumerates the **entire** shell — every resolution-0 base cell
expanded to its descendants at the target resolution (`GetChildrenForResolution`, which correctly
yields 7 children per hexagon and 6 per pentagon) — samples two 3-D fields (**elevation** and
**moisture**) at each cell centre, classifies a biome, and writes terrain keyed by the packed H3 index.
It is modelled on the hex-native [`HexCavesGenerator`](../../Aetherium.Server/WorldGen/Generators/HexCavesGenerator.cs):
consult the topology, don't scan a rectangle.

### 2.4 Choose biome shares by *percentile*, not fixed thresholds
The square overworld made oceans by drowning the map's **edges** — a trick a borderless sphere can't
use, and raw 3-D fractal noise clusters near 0.5, so fixed cut-offs collapse to a single biome. Instead
the generator picks a **global sea level at a target ocean percentile**, then splits land and lowland
the same way. This is not a hack — *ocean coverage as a design knob* is precisely how you author a
planet. Defaults are Earth-ish and every share is a generator parameter:

| Parameter | Default | Meaning |
|---|---|---|
| `oceanFraction` | 0.55 | fraction of the planet below sea level |
| `mountainLandFraction` | 0.10 | highest tenth of *land* → Mountain |
| `hillLandFraction` | 0.20 | next fifth of land → Hills |
| `desertLowlandFraction` | 0.30 | driest third of *lowland* → Desert |
| `forestLowlandFraction` | 0.30 | wettest third of lowland → Forest |
| `h3Resolution` | 4 | shell resolution (≈288k cells) |
| `elevScale` / `moistScale` / `octaves` | 1.7 / 2.4 / 5 | feature size + detail on the unit sphere |

A res-4 planet at these defaults comes out **Water 55 %, Plains 13 %, Forest 9 %, Desert 9 %,
Hills 9 %, Mountain 5 %** — a believable world with real oceans and all six biomes, deterministic per
seed. (Generation cost: ~2 s at res 3 / ~41k cells, ~10 s at res 4 / ~288k cells — a one-time world
build.)

### 2.5 Gate the square-only passes off the sphere (for now)
Terrain, biome theming, audio, and population are topology-agnostic (they read the terrain dictionary)
and run on H3 unchanged. But rivers, roads, cities, story anchors, portals, and connectivity
**validation** are still written in square-grid geometry (L-shaped Manhattan carves, rectangular
bounding boxes, 4/6-neighbour deltas, a single start→objective route). Rather than let them corrupt a
sphere, `IWorldGenerationPass` gained a default `SupportsTopology(topology) => true`, and the
square-only passes override it to opt out of `"h3"`. The orchestrator skips them; existing square/hex/tri
worlds are **byte-identical** (the gate only ever excludes `"h3"`). Their sphere-native replacements
are [§7](#7-roadmap).

**In one sentence:** worldgen learned the tiling, learned to sample the sphere in 3-D, got a generator
that enumerates cells instead of scanning a rectangle, chooses biomes by percentile so a borderless
world still has oceans, and quarantines the passes that still think in squares.

---

## 3. Is ~288k cells a big enough world?

**Yes — comfortably, for exactly the systems named.** Resolution 4 is **288,122 cells**, roughly twice
the 384² (147,456-cell) [Overworld](../../Data/Games/overworld/game.yaml) that already hosts three
cities, wilderness, rivers, and a road network. The economy's scale isn't bounded by cell count but by
**how many settlements, markets, and routes** fit with real distance between them — and 288k cells is
generous:

- **Settlements & markets.** The shipped macro-economy (`ClusterGrain` + `ClusterEconomyState`, see
  [economy-simulation.md](../economy-simulation.md)) keeps a market per settlement. Even a dense planet
  (a settlement per ~few-thousand cells) yields **dozens of markets** with hundreds of cells of
  wilderness between them — the friction that makes local prices and hauling matter (the Albion lesson).
- **Trade & transport as geography.** With ~55 % ocean, landmasses are naturally separated by sea —
  instant "sea lanes vs land routes," ports, and island economies fall out of the terrain for free.
  Great-circle distance means a caravan raided on the far side of the world genuinely can't be
  re-supplied quickly; scarcity becomes causal ([economy §2](../economy-simulation.md)).
- **Transportation networks.** [transit-networks.md](transit-networks.md) builds rail/road/subway/bus
  as PCG features with stations; a planet gives them continental scale to span and multiple cities to
  connect.
- **Headroom.** If a game needs more, resolution 5 is ~2M cells and 6 is ~14M — the same generator, one
  parameter. If it needs less (faster dev), res 3 is ~41k and res 2 ~5.9k. The tests run at low
  resolutions for speed; the bundle ships at 4.

**Recommendation:** resolution 4 is the right default for a planetary economy sandbox — large enough
that logistics is content, small enough to generate in seconds and hold in memory. Adopt it.

---

## 4. What's built now (Slice 1)

| Area | Change | File |
|---|---|---|
| Worldgen seam | `Topology` on `GeneratorContext` + `WorldGenerationRequest`; resolved in the orchestrator; threaded through `GameMapGrain`; persisted on `WorldRecipe` | `WorldGen/GeneratorContext.cs`, `WorldGenerationRequest.cs`, `WorldGenerationOrchestrator.cs`, `MultiWorld/GameMapGrain.cs`, `MultiWorld/WorldSnapshot.cs` |
| 3-D noise | `Noise`/`FractalNoise`/normalized 3-D overloads for spherical sampling | `WorldGen/Algorithms/Noise/PerlinNoise.cs` |
| Sphere generator | `H3TerrainGenerator` — full-shell enumeration, 3-D elevation+moisture, percentile biomes | `WorldGen/Generators/Outdoor/H3TerrainGenerator.cs` |
| Pipeline routing | `OutdoorLayoutPass` runs the H3 generator on `"h3"`; `IWorldGenerationPass.SupportsTopology` gate; four square-only passes opt out of `"h3"` | `WorldGen/IWorldGenerationPass.cs`, `Passes/OutdoorLayoutPass.cs`, `OutdoorValidationPass.cs`, `OutdoorInteractionsPass.cs`, `PortalNetworkPass.cs`, `EnvironmentalStoryPass.cs` |
| Perception (walkable) | `IGridTopology.RelativeCoords` (raw diff default; H3 `cellToLocalIj` override); `PerceptionService` routes H3 worlds to a `gridDisk` viewport with perceiver-anchored local-i/j keys | `Topology/IGridTopology.cs`, `Topology/H3Topology.cs`, `PerceptionService.cs` |
| FOV + lighting (sphere-native) | `H3VisionLighting` runs the square occlusion/light ray model (reusing `FovCalculator.GetCellOpacity`, `Topology.Line`/`Delta`) over the H3 disk: LOS occlusion, directional cone, point-light pools + darkness range | `Perception/H3VisionLighting.cs`, `PerceptionService.cs` |
| Rivers (sphere-native) | `H3RiverCarver` — steepest descent down the elevation field from spaced high headwaters to the sea, widening downstream into multi-lane channels; carved as Water | `WorldGen/Generators/Outdoor/H3RiverCarver.cs` |
| Settlements (sphere-native) | `H3SettlementPlanner` + `Settlement` component / `SettlementEntity` — tiered (Capital→Village), great-circle-spaced, coastal-leaning; persistent entities the economy hooks; built-up cores | `WorldGen/Generators/Outdoor/H3SettlementPlanner.cs`, `Components/Settlement.cs`, `Entities/SettlementEntity.cs` |
| Roads (sphere-native) | `H3RoadNetwork` — MST backbone + k-nearest loops over great-circle distance, carved wide along `gridPathCells`, bridging water; highways (city trunk) wider than feeders | `WorldGen/Generators/Outdoor/H3RoadNetwork.cs` |
| Economy (T2 biome flows) | `Producer`/`Consumer`/`LocalMarket`/`TradeLinks` components + `EconomySystem` (on the map tick) + `EconomySeeder`/`Goods`: settlements produce by biome, consume a universal basket, price from stock-vs-target, and goods arbitrage cheap→dear along the road graph with distance friction | `Components/Producer.cs`, `Consumer.cs`, `LocalMarket.cs`, `TradeLinks.cs`, `Economy/EconomySystem.cs`, `EconomySeeder.cs`, `Goods.cs`, `MultiWorld/GameMapGrain.cs` |
| Bundle | `aphelion-h3` — the switchable sci-fi planet (topology h3, generator h3-terrain, res 4): 320 tiered settlements, wide rivers + road corridors between them | `Data/Games/aphelion-h3/game.yaml` |
| Tests | H3 full-shell coverage, pentagon handling, biome variety, determinism, spawn, neighbour packing; 3-D noise range/determinism/continuity; relative-coord origin/injectivity; H3 perception frame + walk-recentre; **rivers flow downhill to sea + widen, settlements tiered/persistent + capital spawn, roads MST-connect + bridge water**; bundle validates with `topology: h3` | `Aetherium.Test/WorldGen/H3TerrainGeneratorTests.cs`, `PerlinNoise3DTests.cs`, `H3SphereFeaturesTests.cs`, `Perception/H3PerceptionTests.cs`, `Games/GameDefinitionRegistryTests.cs` |

Everything above is green in the full suite, and every non-H3 world is unaffected.

---

## 5. How square and H3 coexist

The two worlds share **one engine and one content model**; only the tiling differs, and the tiling is
data. This is the whole point of the topology seam:

- **Selection is one line of bundle YAML** (`world.topology`), validated at load: an unknown tiling, or
  a generator that doesn't declare support for the chosen tiling, is a hard error
  ([`GameDefinitionValidator`](../../Aetherium.Server/Games/GameDefinitionValidator.cs)).
- **The square Aphelion is untouched.** Aphelion Prime is additive — a second bundle. A game can ship
  both and let players pick, or a campaign can portal between them (a station in orbit ↔ the planet
  below) via the existing topology-agnostic portal system, since H3 is `IHierarchicalGridTopology` and
  cross-scale travel is already a portal, not new movement code.
- **Movement/FOV/reach/facing already work on H3** (built and invariant-tested); a creature walks the
  sphere through `World.Topology` exactly as it walks a square grid.

---

## 6. Testing

- **Generation** (`H3TerrainGeneratorTests`): the shell is *complete* (distinct cell count equals the
  exact H3 count `2 + 120·7^res`, which doubles as a coverage proof); resolution 0 contains all 122
  base cells including exactly **12 pentagons**; every cell carries a known biome; oceans, land, and ≥3
  biomes are present; identical seeds are byte-identical and distinct seeds differ; the spawn is
  passable; and a cell's H3 neighbours are themselves real cells of the shell (proving the packed X/Y
  round-trips through H3 adjacency).
- **Noise** (`PerlinNoise3DTests`): deterministic per seed, normalized output in `[0,1]`, spatially
  varied (not constant), seed-sensitive, and continuous under small steps — sampled over a
  golden-spiral spread of unit-sphere points.
- **Bundle** (`GameDefinitionRegistryTests`): the shipped `aphelion-h3` loads and validates with **zero
  error diagnostics**, binding `topology: h3` and `generatorType: h3-terrain` — the canary that the H3
  generator is discoverable and declares sphere support.

---

## 7. Roadmap

Slice 1 is a terrained, biome-varied, populated **planet you can generate, switch to, and walk**.
Getting to a *played* planetary economy is a sequence of self-contained slices, each on machinery that
already exists:

- **P0 — Perception on the sphere (playability gate). ✅ BUILT.** `PerceptionService` emitted
  player-relative keys by subtracting raw lattice coords — correct for square/hex/tri, meaningless for a
  packed H3 index. It now routes H3 worlds through a dedicated path (`PerceptionService.ComputeH3Perception`)
  that enumerates the visible set as a `gridDisk` around the perceiver and keys each cell by
  perceiver-anchored local i/j via the topology's `RelativeCoords` (H3's
  [`cellToLocalIj`](../h3-topology.md#perception-on-a-sphere-celltolocalij-keys)), player at `0,0,0`.
  Same fairness contract (opaque offsets, no absolute coordinates); cells with no stable local frame (a
  pentagon at extreme range) are omitted rather than throwing. Slice-1 scope matches the sample world:
  daylight, 360°, full-disk visibility — **FOV occlusion and non-daylight lighting on the sphere are the
  remaining perception work** (a natural extension of P1, using `topology.Line` raycasts, which H3
  already implements). Tested by `H3RelativeCoordsTests` (origin, injectivity over a disk) and
  `H3PerceptionFrameTests` (H3 frame centred on the player, disk of terrained cells, frame recentres as
  the player walks).
- **P0.5 — FOV + lighting on the sphere. ✅ BUILT.** `PerceptionService.ComputeH3Perception` no longer
  shows a flat, fully-lit disc: `Aetherium.Server/Perception/H3VisionLighting.cs` runs the engine's
  existing occlusion and light-propagation model — which was already topology-general (occlusion via
  `Topology.Line`, distance/cone via `Topology.Delta`, opacity via the shared
  `FovCalculator.GetCellOpacity`) — over the H3 gridDisk instead of a rectangle. Result: mountains,
  forests (cumulative 0.5 opacity), and walls occlude line of sight; the directional cone (heading + FOV)
  restricts the forward arc; light sources cast distance-attenuated, occlusion-blocked pools and darkness
  shrinks the effective view range — identical behaviour to the square grid. Cost is per-cell raycast
  (~45 ms for a radius-20 daylight frame at res 4, comparable to the square path and within the
  perception debounce). Tested by `H3FovLightingTests` (a mountain ring blocks everything beyond; a lone
  obstacle only casts a wedge; the cone hides what's behind you; darkness collapses the view while a
  torch lights a pool). **Not yet sphere-native:** the vertical 3-D occlusion slab (`ColumnViewOpacity`/
  `VerticalVisibleBands`) is Z-axis engine machinery orthogonal to the XY tiling — it applies to H3
  unchanged for a single surface level, and multi-level H3 is future work.
- **P1 — Sphere-native rivers, coasts, and roads. ✅ BUILT.** `H3RiverCarver` traces rivers by steepest
  descent over `topology.Neighbors` down the elevation field (spaced high headwaters → sea), widening
  downstream into multi-lane channels. `H3RoadNetwork` connects settlements along `topology.Line`
  (great-circle cell paths) as wide corridors that bridge water, using an MST backbone plus k-nearest
  loops weighted by great-circle distance. Both run inside `H3TerrainGenerator` after the biome pass, so
  the square-only river/road passes stay gated off. Coasts are read from the `Water` field for siting.
- **P2 — Settlements on the sphere. ✅ BUILT (connectivity validation deferred).** `H3SettlementPlanner`
  sites tiered settlements (Capital→Village) over the shell: buildable-lowland candidates, great-circle
  spacing per tier, a mild coastal lean, cores stamped via `topology.Range`. Each is a persistent
  `SettlementEntity`+`Settlement` — the hook the economy attaches to. Explicit reachability validation
  (walking `World.Topology`) is still open, but the MST guarantees every settlement is road-connected by
  construction.
- **P3 — Economy (T2 "real flows"). ✅ BUILT (first slice).** `Producer`/`Consumer`/`LocalMarket`/
  `TradeLinks` components seeded onto every settlement by biome + population (`EconomySeeder`, `Goods`):
  plains grow grain, forests cut timber, hills mine ore, coasts land fish; everyone eats and builds.
  `EconomySystem` ticks on the map (`GameMapGrain.TickAsync`) — production/consumption, stock-vs-target
  pricing, and goods arbitraging cheap→dear along the road graph with throughput/distance friction, so a
  forest's timber spreads to the plains and prices converge modulo distance. Per-map and player-facing;
  the cluster macro-economy is untouched. **Remaining:** goods/recipes as per-world data (not hard-coded),
  player buy/sell + currency, and feeding the cluster market for cross-world trade.
- **P4 — Transportation networks ([transit-networks.md](transit-networks.md)).** Rail/road/sea/air
  services spanning the continents, stations, scheduled and hailed transport — logistics as gameplay at
  planetary scale. Subways run a negative band, satellites the high bands; grade separation is the
  z-altitude work below.
- **P4.5 — Satellites + full z-altitude on the sphere.** The H3 perception path is single-Z today; the
  vertical slab (below) lifts that so orbits overhead and subway tunnels underfoot are perceivable.
  Satellites orbit high bands (H3 rings, never colliding), detectable only through a tuned radio — an
  extra channel of perception granted by an item.
- **P5 — Climate & biome expansion.** Latitude-banded climate (Hadley-cell deserts, polar ice caps,
  tropical belts) using the latitude the sphere makes meaningful — needs new terrain types (ice/tundra)
  and client theming. Optionally hierarchical resolutions (survey from orbit at a coarse resolution,
  land at a fine one) via the portal-linked `IHierarchicalGridTopology` already in place.

Each slice keeps the square games byte-identical and moves one gated pass to a sphere-native
implementation.

---

## 8. Risks & open questions

- **Generation cost at scale.** Res 4 is ~10 s and ~a few hundred MB of terrain entities (one-time, at
  world creation; the per-tick loop is already O(creatures), not O(cells), after the large-world perf
  work). Res 5+ would want a chunked/lazy generation-and-hydration story before use.
- **`pocketken.H3` maturity.** `GridDistance`/`GridPathCells` are documented as unreliable *across* a
  pentagon (already isolated in the topology tests); the sphere-native path/validation slices (P1–P2)
  must keep pentagon-adjacent routing on the safe APIs (`GridDisk`, children/neighbours). Fallback is
  the vendored index-math subset behind the topology seam.
- **Perception experimental API.** `cellToLocalIj` is marked experimental upstream; the documented
  fallback is a `gridDisk` + local axial fit needing no experimental call.
- **Content theming for a sci-fi planet.** Slice 1 reuses the natural biome palette (water/desert/
  forest/…); a sci-fi skin (client models/materials for an alien world) is client-side theming, not an
  engine change.
