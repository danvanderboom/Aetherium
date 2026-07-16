# Hexagonal Tiles — Exploratory Design

*Status: exploratory design, not scheduled. Grounded in a code survey of the engine (2026-07-16, `develop` @ df65e5d + design branch); every claim below about current behavior carries a file reference. Companion asset analysis: [docs/design/unity-sample/assets.md](design/unity-sample/assets.md).*

> **Superseded in part by [grid-topologies.md](grid-topologies.md)** — the approved generalized design (square/hex/triangle, H3-ready). That document owns the topology abstraction (its `IGridTopology` replaces the sketch below, adding per-cell direction sets and the `Delta` embedding); this one remains the hexagon deep-dive: why hex, the hex-specific FOV/worldgen analysis, and the hex asset landscape.

## The question

What would it take for Aetherium to support hexagonal tiles instead of (or alongside) square tiles — and what would that do to the 3D asset pipeline?

The short answer, ahead of the detail: **less than the folklore suggests.** Two architectural facts make hex unusually tractable here — the engine stores the world as a coordinate-keyed dictionary rather than indexed 2D arrays, and its FOV is per-cell raycasting rather than square-octant shadowcasting. The genuinely hard parts are the line-drawing/lighting algorithms and world generation; almost everything else is a direction table and a distance function hiding behind a seam that mostly already exists.

## The governing idea: topology is per-world data

Consistent with the engine's core principle (configurable behavior is per-world *data*), hex support should not be a global switch. It's a **topology** the world declares:

```yaml
# game.yaml
world:
  topology: hex          # default: square
  generatorType: hex-station
  size: { width: 64, height: 64, depth: 3 }
```

Threaded through the same recipe every per-world config family uses (`GameDefinition` → `WorldTemplate` → `WorldConfig` → grain state). Square worlds are untouched — Emberfall, Neonveil, and Aphelion keep running byte-identically while a hex world runs beside them on the same server.

## What is already topology-agnostic (the leverage)

The survey's happiest findings — none of these change at all:

| Fact | Where | Why it matters |
|---|---|---|
| World storage is `Dictionary<WorldLocation, …>` — **no `y*width+x` flattening exists anywhere** | `Core/World.cs:20,35` | `WorldLocation(X,Y,Z)` reinterprets directly as **axial hex coordinates** (X=q, Y=r). No storage migration, no new coordinate type on the wire |
| Heading is **integer degrees 0–359**, and rotation adds **arbitrary degrees** | `Components/HasHeading.cs:17-21`, `GameMapGrain.cs:1837-1860` | 60° turning needs no new representation — degrees already express it |
| The directional-vision cone is **angle/vector math** (dot product vs `cos(halfFov)`), not cell patterns | `Perception/DirectionalFovCalculator.cs:45-111` | Vision cones work on hex unchanged |
| Perception `Visuals` keys are opaque `"relX,relY,relZ"` strings | `PerceptionService.cs:288` | The wire format carries axial deltas without change; clients interpret per topology |
| Heat trails, portals, spawn selection, teleports are coordinate- or ID-keyed with no offset math | `HeatTrailTracker.cs:24-181`, `GameMapGrain.cs:866-905` | Whole subsystems come along for free |
| The Z axis (levels, stairs, lifts) is fully orthogonal to the XY tiling | `World.cs:486-550` | Multi-deck hex stations need zero vertical-travel work |
| Relative, embodied movement (F/B + rotate) is the protocol's primary movement surface | `MoveTool.cs:82-98` | The fairness constraint pays again: clients never needed absolute square directions, so hex movement is the *same client API* over a different turn step |

## The seam: `ITopology`

Nearly all remaining square-ness is inlined direction tables and `Math.Abs(dx)+Math.Abs(dy)` distance checks, duplicated at a handful of call sites. The design move is one per-world service:

```csharp
public interface IGridTopology
{
    int DirectionCount { get; }                              // 4 (square) / 6 (hex)
    int RotationStepDegrees { get; }                         // 90 / 60
    IEnumerable<WorldLocation> Neighbors(WorldLocation loc);
    WorldLocation Step(WorldLocation loc, int directionIndex);
    int Distance(WorldLocation a, WorldLocation b);          // Manhattan / hex cube distance
    int HeadingToDirectionIndex(int degrees);                // 4 buckets / 6 buckets
    int ResolveRelative(RelativeMove move, int headingDegrees); // F/B/L/R… → direction index
    IEnumerable<WorldLocation> Line(WorldLocation a, WorldLocation b);   // Bresenham / cube-lerp
    IEnumerable<WorldLocation> Range(WorldLocation center, int radius);  // rect / hex disc
}
```

Selected per map from `WorldConfig.Topology`, compiled once at grain init (the same pattern as `ContentCompiler`/`EcaRuntime`). The migrations it absorbs, from the survey:

| Call site | Today | Under `ITopology` |
|---|---|---|
| `World.TryMove`/`TryMoveSteps` direction switches (`World.cs:277-302,431-477`) | hardcoded N=(0,−1) etc. | `Step(loc, dir)` |
| Melee reach (`GameMapGrain.cs:2296-2302`), ability range (`:2436-2439`), interaction reach (`InteractionSystem.cs:160-172`), monster melee (`MonsterBehaviors.cs:81-103`) | inline Manhattan | `Distance(a,b) <= r` |
| `DegreesToCardinal` (`GameMapGrain.cs:3117-3124`), `RotateRelativeByHeading` (`:3126-3149`), enum rotate tables (`Extensions.cs:22-56`) | 4-bucket snaps, quarter-turn cycles | `HeadingToDirectionIndex`, `ResolveRelative`, `RotationStepDegrees` |
| Central neighbor helper (`WorldGen/WorldLocationNeighbors.cs:19-51`), ECA spawn-cell offsets (`GameMapGrain.cs:2168-2181`), monster wander (`Monster.cs:97-120`) | `Cardinal4Offsets` | `Neighbors(loc)` |

All of these are **mechanical** once the interface exists — and the square implementation is a pure refactor with zero behavior change, testable by the existing 1351-test suite.

### Coordinates: axial, in the fields we already have

Hex worlds interpret `(X, Y)` as axial `(q, r)` ([the standard convention](https://www.redblobgames.com/grids/hexagons/)); cube coordinates (`s = −q−r`) are derived inside topology math only. Hex distance is `(|dq| + |dr| + |dq+dr|) / 2`. A hex region still fits a rectangular bounding box in axial space — which means even the FOV stack's `bool[height,width]` working buffers (`FovCalculator.cs`, `VisionSystem.cs:60-120`) can keep their shape with axial indexing; only *iteration* (rect scan → `Range()` disc) and *lines* change.

## The two genuinely hard problems

### 1. FOV, lighting, and lines *(new algorithms, same architecture — M)*

The survey's best news: `FovCalculator` is **per-cell raycasting with Bresenham lines** (`FovCalculator.cs:22-160` — it explicitly rejected octant shadowcasting), and lighting does per-cell raycasts with Euclidean falloff (`LightCalculator.cs:20-205`). Octant shadowcasting is deeply square; per-cell raycasting generalizes directly:

- **Line drawing:** replace Bresenham with cube-space linear interpolation + rounding (the standard hex line algorithm) inside `ITopology.Line`.
- **Range/iteration:** replace rectangle scans (`VisionSystem.cs:60-120`, `InfraredVisionSystem.cs:38-59`) with `Range(center, radius)` enumeration.
- **Falloff:** Euclidean distance on hex centers is still meaningful for light attenuation (hex centers are points in the plane) — the falloff math (`LightCalculator.cs:81`) may need no change at all, just the cell enumeration around it.

Same architecture, two swapped primitives. The cost is careful testing (FOV symmetry, wall-hugging cases), not invention.

### 2. World generation *(per-topology generators — M/L)*

Rect rooms, L-corridors, maze carving, and prefab stamping are inherently square (`RoomsAndCorridorsGenerator.cs:52-152`, `AdvancedDungeonGenerator.cs`, `PrefabStamper.cs`). But the generator registry already treats generators as named, per-world choices (`generatorType` is per-world data today) — so hex worlds don't *convert* existing generators, they get **new ones**:

- `hex-station` / `hex-caves`: rooms as hex discs (`Range(c, r)`), corridors as hex lines, growth by neighbor flood — all natural on hex, arguably *more* organic than rect+L.
- Prefab stamping generalizes later (axial-parallelogram or disc footprints); not needed for a first hex world.
- Perlin-based terrain assignment is already continuous-space (`PerlinTerrainGenerator`) — it only needs hex cell layout, making an outdoor hex world surprisingly cheap.

## Protocol & client impact (small)

| Surface | Change |
|---|---|
| `WorldDirection` enum (`SharedEnums.cs:5-13`) | Degrees (`HeadingDegrees`) become the documented source of truth (they already ship in every frame); the 4-cardinal enum stays for square worlds; hex worlds either add a 6-value enum or simply rely on degrees + direction index. Recommendation: **degrees-first, enums as per-topology cosmetics** |
| Relative movement | `F`/`B` work on hex (every hex direction has an opposite); `L`/`R` as *moves* have no perpendicular neighbor — they become either 60°-turn-then-move composites or new relative moves (`FL`/`FR`/`BL`/`BR` at ±60°/±120°). Turn presets change from ±90° to ±60° (`RotateTool.cs:76`). The client API shape is unchanged — this is the fairness constraint's dividend |
| `VisibleBounds` (`RectangleDto`) | Stays as the axial bounding box (still true and still useful for buffer sizing); clients derive the hex disc from the `Visuals` keys themselves |
| New: `Topology` field | One string on `WorldInfo`/`GameStateDto` so clients pick their layout math |
| Client library (`GridMapView`) | Axial→world transform (`x = size·(√3·q + √3/2·r)`, `z = size·3/2·r` for pointy-top), hex tile prefabs in the ThemeAsset, 60° facing snaps. Contained in the presentation layer by design |

## Asset impact (summary — full analysis in [assets.md](design/unity-sample/assets.md))

- **Characters, creatures, items, props are grid-agnostic.** Everything animated in the Aphelion slice works on hex unchanged — only *structural* tiles (floors/walls/doors) are grid-shaped.
- **Free CC0 hex terrain exists and is good**: [Kenney Hexagon Kit](https://kenney.nl/assets/hexagon-kit) (70 models), [Hexagon Pack](https://kenney.nl/assets/hexagon-pack) (310), [Hexagon Tiles](https://kenney.nl/assets/hexagon-tiles) (90), and the [KayKit Medieval Hexagon Pack](https://kaylousberg.itch.io/kaykit-medieval-hexagon) (200+) — all nature/fantasy/strategy-flavored, ideal for *outdoor* hex worlds.
- **Sci-fi hex interiors don't exist as free packs** — but a hex modular kit is *easier* to author than a square one: all six edges of a hex are identical by symmetry, so one floor + one wall-edge + one door-edge + one window-edge composes every room shape with no corner/T/cross piece explosion. And honeycomb architecture is iconically sci-fi — a hex station would look *more* like a space station, not less.
- Orientation/scale conventions (pointy-top vs flat-top, hex radius) differ across packs — a one-time import normalization per pack, same as any kit.

## Sizing & phasing

| Phase | Content | Size |
|---|---|---|
| **H0 — the seam** | Introduce `IGridTopology`; route the ~12 surveyed call sites through it; square implementation only; zero behavior change (existing suite is the regression net) | M (mechanical) |
| **H1 — first hex world** | Hex topology impl (neighbors/distance/lines/ranges/60° buckets); FOV+lighting on hex; one `hex-caves` generator; `topology:` bundle field threaded; a test bundle | M/L |
| **H2 — parity & polish** | Hex prefab stamping, richer hex generators (`hex-station`), client GridMapView hex layout + theme support, relative-move vocabulary decision | M |
| Not planned | Converting existing square content; mixed-topology maps in one world | — |

## Recommendation

Don't build it now — Aphelion and the client library come first, and nothing in them fights a future hex world. But two cheap behaviors *now* keep the door open:

1. **Adopt the `ITopology` seam opportunistically** — any slice that already touches adjacency/distance code (the G1/G2 perception channels won't, but a future hazard or AI slice might) should route through the central helper rather than adding new inline `Math.Abs` sums.
2. **Keep degrees as the heading source of truth** in anything new (already the design stance in the client library).

The deeper point the survey surfaced: Aetherium's data-driven architecture accidentally did most of the hex work already. Coordinate-keyed storage, degree-based headings, angle-based cones, opaque perception keys, and per-world generator selection mean *topology* is just one more thing a world can declare — which is exactly how this engine likes its features.
