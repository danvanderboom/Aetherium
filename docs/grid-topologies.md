# Grid Topologies — Pluggable World Tilings (Square · Hex · Triangle · H3-ready)

*Status: approved design, implementation not yet scheduled — this document defines the phased backlog (P0–P3) to be pulled when a milestone needs it. Grounded in the 2026-07-16 engine survey (file references verified against `develop` @ df65e5d). The hexagon-specific deep dive lives in [hexagonal-tiles.md](hexagonal-tiles.md); this document supersedes its interface sketch with the generalized abstraction.*

## The goal

Aetherium worlds should be able to declare their **tiling**: square (today's), hexagonal, triangular — and the abstraction must be shaped so that **Uber's H3** hierarchical geospatial grid (hexagons with 12 pentagons per resolution, multiple nested resolutions, spherical coverage of planets, moons, and planetoids) can be added later **without reshaping the abstraction again**. Topology, like every other configurable behavior in this engine, is **per-world data**:

```yaml
# game.yaml
world:
  topology: hex        # "square" (default) | "hex" | "tri" | (later) "h3"
  generatorType: hex-caves
  size: { width: 64, height: 64, depth: 3 }
```

A hex world, a triangle world, and Emberfall run side by side on one server; omitting the field means square, byte-identically.

## Why this is cheaper than it sounds

Two architectural facts (verified in the survey, detailed in [hexagonal-tiles.md](hexagonal-tiles.md#what-is-already-topology-agnostic-the-leverage)):

1. **World storage is coordinate-keyed** (`Dictionary<WorldLocation, …>`, `Core/World.cs:20,35`) — no `y*width+x` flattening exists anywhere. `WorldLocation(X,Y,Z)` reinterprets as hex axial `(q,r)` or triangle coordinates without a storage or wire change.
2. **Headings are integer degrees** with arbitrary-angle rotation (`Components/HasHeading.cs:17-21`, `GameMapGrain.cs:1837-1860`), the vision cone is dot-product angle math, and perception keys are opaque `"relX,relY,relZ"` strings — the *continuous* half of the engine never knew about squares.

What remains square is a finite, surveyed list: ~12 direction-table/Manhattan-distance call sites, the Bresenham line primitive inside FOV/lighting, rectangle scans, and the worldgen generators. That list is this design's work plan.

## The abstraction

New namespace `Aetherium.Topology` (folder `Aetherium.Server/Topology/`). Implementations are stateless singletons, resolved once at grain init from `WorldConfig.Topology` (the `ContentCompiler`/`EcaRuntime` compile-once pattern) onto a new `World.Topology` property.

```csharp
// Hot-path mirror of WorldLocation (which is a mutable class with string-based
// GetHashCode — a perf hazard topology math routes around). Convert at the seam
// boundary; WorldLocation's type, wire shape, and hashing are unchanged.
public readonly record struct GridCoord(int X, int Y, int Z);

// One outgoing edge of a cell. DirectionIndex is stable per (topology, cell)
// in-process but is NEVER persisted or sent on the wire — persist headings
// (degrees) or locations instead. HeadingDegrees = compass heading (0 = north,
// clockwise) of crossing this edge in the local embedding.
public readonly record struct EdgeStep(int DirectionIndex, GridCoord Target, int HeadingDegrees);

public readonly record struct RelativeMoveResolution(
    bool Success, EdgeStep Step, int NewHeadingDegrees, string? FailReason);

public interface IGridTopology
{
    string Name { get; }                 // "square" | "hex" | "tri" | (later) "h3"
    bool HasUniformDirections { get; }   // square/hex: true — enables table fast paths
    int MaxDirectionCount { get; }       // 4 / 6 / 3 / 6 — buffer sizing only

    // ---- per-cell direction machinery (the triangle/pentagon-proof core) ----
    int DirectionCount(GridCoord cell);
    EdgeStep GetStep(GridCoord cell, int directionIndex);
    IEnumerable<EdgeStep> Steps(GridCoord cell);
    IEnumerable<GridCoord> Neighbors(GridCoord cell);

    // ---- metric & geometry (same-Z; the Z axis stays engine-level) ----
    int Distance(GridCoord a, GridCoord b);                      // graph metric
    IEnumerable<GridCoord> Line(GridCoord a, GridCoord b);       // connected under Neighbors
    IEnumerable<GridCoord> Range(GridCoord center, int radius);  // exactly the Distance<=r ball

    // ---- heading machinery — degrees remain the engine-wide source of truth ----
    int SnapHeading(GridCoord cell, int degrees);   // nearest legal facing at this cell
    int TurnStepDegrees(GridCoord cell);            // 90 / 60 / 120 — rotate-preset granularity
    int? HeadingToDirectionIndex(GridCoord cell, int degrees);
    RelativeMoveResolution ResolveRelative(GridCoord cell, int headingDegrees,
                                           RelativeDirection move);

    // ---- local planar embedding for cones/falloff — the H3-proofing method ----
    // Vector from->to in the local tangent plane, in cell-size units. Planar
    // topologies: cell-center difference. H3 later: azimuthal projection of the
    // great-circle displacement. Only ever called at perception range — exactly
    // where a tangent plane is valid on a sphere.
    (double X, double Y) Delta(GridCoord from, GridCoord to);
}

// Planar-only extras (square/hex/tri — NOT implemented by H3). Worldgen and
// debug tooling may use absolute centers; runtime systems must use Delta.
public interface IPlanarGridTopology : IGridTopology
{
    (double X, double Y) CellCenter(GridCoord cell);
}

public static class GridTopologyRegistry
{
    public static IGridTopology Get(string name);   // "square" default; throws on unknown
    public static bool TryGet(string name, out IGridTopology topology);
    public static IReadOnlyCollection<string> Names { get; }
}
```

### The two structural rules that make it general

**Rule 1 — every direction query takes the cell.** Uniform grids don't need it; the grids we're protecting the future for do:

| Topology | Neighbors | Direction set | Notes |
|---|---|---|---|
| Square | 4, uniform | N/E/S/W (table) | `HasUniformDirections` fast path |
| Hex (axial) | 6, uniform | 6 × 60° (table) | fast path |
| **Triangle** | **3, parity-dependent** | up-cells (`(X+Y)&1==0`) cross edges at `{60°,180°,300°}`; down-cells at `{0°,120°,240°}` | the generalization proof |
| **H3** | **6, except 12 pentagons with 5** | per-cell via H3 library | same machinery, zero new concepts |

**Rule 2 — all relative movement resolves angularly against degree headings.** `ResolveRelative(cell, heading, move)` computes the target angle (`F=+0°, R=+90°, B=+180°, L=+270°` — the existing 4-value `RelativeDirection` wire enum is kept unchanged on *every* topology), picks the outgoing edge nearest that angle, with deterministic tie-breaks: (a) toward forward, then (b) clockwise. It returns the chosen edge and the new heading (= the edge's heading); the caller decides whether the actor's heading updates. Consequences:

- **Square** — exact everywhere; byte-identical to today's `DegreesToCardinal`/`RotateRelativeByHeading` (`GameMapGrain.cs:3117-3149`), pinned by golden tests.
- **Hex** — F/B exact (every hex edge has an opposite); L/R resolve to the ±60° "forward-side" edges via tie-break (a); turn preset becomes 60°.
- **Triangle** — headings snap server-side to the current cell's three edge headings (`SnapHeading`); the turn preset is 120° (cycling the cell's own edges — 60° would face a non-edge). Forward is always exact after snapping. **Backward genuinely has no opposite edge**: heading+180° lands exactly between two edges and even tie-break (a) ties, so (b) picks the clockwise one — documented, deterministic, golden-tested. Crossing an edge flips parity, and the returned heading is automatically legal in the destination cell (the shared edge exists on both sides), so headings never desync.
- **H3 pentagon** (later) — the missing sixth edge simply isn't a candidate. No new machinery.

### Invariants (the property-test harness)

Every implementation — including a deliberately irregular CI mock (see P3) — must pass:

1. **Neighbor symmetry**: `b ∈ Neighbors(a) ⇔ a ∈ Neighbors(b)`; every edge has a reverse edge.
2. **`Distance` is a true metric**, with `Distance(a,b)==1 ⇔ adjacent`.
3. **`Range(c,r)` is exactly** `{ x : Distance(c,x) ≤ r }`.
4. **`Line(a,b)` is connected** under `Neighbors`, starts at `a`, ends at `b`.
5. `3 ≤ DirectionCount(cell) ≤ MaxDirectionCount ≤ 8`; callers never assume it constant.
6. **Direction indices are ephemeral** — never persisted, never on the wire (headings/locations are).
7. Edge `HeadingDegrees` is consistent with `Delta(cell, edge.Target)`.
8. **Topology governs XY only** — the Z axis (levels, stairs, lifts) is orthogonal and untouched.

## Cell identity: `WorldLocation` stays (with the H3 packing plan on record)

`WorldLocation(int X,Y,Z)` remains the universal cell key across storage, protocol, deltas, and persistence through P0–P2: hex uses axial `(q,r)` in X/Y; triangle derives parity from `X+Y`. Introducing an opaque `CellId` now would churn Orleans schemas, the SignalR protocol, and `MapState` persistence for zero in-scope benefit.

**H3 packs losslessly when its day comes**: a valid H3 index's top bit is reserved-zero, so `X = (int)(index >> 32)` is always non-negative and `Y = (int)(index & 0xFFFFFFFF)` round-trips exactly — `Z` stays the vertical level, and `WorldLocation`'s three-int wire shape never changes. For player-relative perception on a sphere (where "relative delta" has no global meaning), H3's `cellToLocalIj` yields perceiver-anchored local coordinates: perception keys become `"relI,relJ,relZ"`, preserving the opaque-string, no-absolute-coordinates contract. (`cellToLocalIj` is valid within a base-cell neighborhood — comfortably beyond perception radii — and is marked experimental upstream; recorded as a P3 dependency note.)

P0 guardrail: `WorldLocation.X/Y` get doc-comments declaring them **topology-interpreted opaque integers**, and the `Delta`/`CellCenter` split keeps geometry consumers off raw X/Y arithmetic.

## H3: how much of a stretch?

Directly answering the question — **given this abstraction, H3 is an implementation, not a redesign.** The four things H3 adds, and where each lands:

| H3 property | Where it lands | Stretch |
|---|---|---|
| 12 pentagons per resolution (5 neighbors) | Rule 1 (per-cell direction sets) — same as triangle parity | None — designed for |
| 64-bit hierarchical cell index | The documented `WorldLocation` packing (above) | Small, deferred |
| Spherical geometry (no global plane) | `Delta` as azimuthal projection; `Line`/`Range` via `h3Line`/`gridDisk`; falloff on tangent-plane distance | Contained in one class |
| Multiple resolutions | `IHierarchicalGridTopology : IGridTopology { Resolution(cell); Parent(cell); Children(cell); }` implemented **only** by H3; one resolution per map; **cross-resolution travel is the existing topology-agnostic portal system** linking maps at different resolutions; client zoom is rendering-only | The real design work, and it maps onto existing engine concepts |
| Planetary/lunar coverage | A world whose map *is* a shell of H3 cells at resolution R over a body — worldgen samples elevation/biome noise on the sphere | New generator, same pipeline |

Dependency: [`pocketken.H3`](https://github.com/pocketken/H3.net) — a maintained pure-C# port of H3 v4 (Apache-2.0, NuGet, netstandard2.0/2.1+; no native interop). Nothing in the solution references it today. Risks: small maintainer community, .NET 10 compatibility unverified; fallback is vendoring the minimal index-math subset behind the topology seam.

## Wiring: the seventh application of the config-threading recipe

`topology` threads exactly like death/abilities/progression/factions/content/eca before it: `GameWorldDefinition` (new `[Id]`, default `"square"`) → `WorldTemplate` → `WorldConfig` + `CreateWorldRequest` → `GameDefinitionMapper` → `GameManagementGrain.CreateWorldAsync` (both paths) → `OrleansWorldHost` → `WorldGrain` state → `GameMapGrain.InitializeAsync` **and** `OnActivateAsync` (reactivation resolves from persisted `MapState.Topology`) → `World.Topology`, plus `GeneratorContext.Topology` for worldgen. All Orleans `[Id(n)]`s append-only; every new field defaults `"square"` so pre-topology persisted state reactivates correctly.

```mermaid
flowchart LR
    subgraph consumers [Seam consumers — surveyed call sites]
        MV["World.TryMove / TryMoveSteps"]
        HD["GameMapGrain heading snap ·<br/>relative turns · RotateTool preset"]
        AD["Melee / ability range /<br/>interaction reach / ECA offsets"]
        AI["MonsterBehaviors wander+melee ·<br/>Monster direction candidates"]
        FOV["FovCalculator · LightCalculator<br/>(lines) · Vision/Infrared/Lighting<br/>(range scans)"]
        WG["Generators (via GeneratorContext)"]
    end
    subgraph topo [IGridTopology per world]
        STEP["Steps / Neighbors /<br/>ResolveRelative / SnapHeading"]
        MET["Distance / Line / Range"]
        EMB["Delta (tangent-plane vector)"]
    end
    MV --> STEP
    HD --> STEP
    AD --> MET
    AI --> STEP
    FOV --> MET
    FOV --> EMB
    WG --> STEP & MET
    CFG[("world.topology<br/>(bundle YAML)")] -->|"registry, once at grain init"| topo
```

## Phased backlog

### P0 — the seam *(size M; square-only; zero behavior change)*

Create `IGridTopology` + `SquareTopology` + `GridTopologyRegistry` + the property harness. Route the surveyed call sites: `World.TryMove`/`TryMoveSteps` (`Core/World.cs:277-302,431-477`), `Monster.GetValidCardinalDirections` (`Entities/Monster.cs:97-120`), legacy `GameSession.MoveView` (pinned to square), `GameMapGrain` heading-snap/relative-turn (`:3117-3149`), melee (`:2296-2302`), ability range (`:2436-2439`), ECA spawn offsets (`:2168-2181`), `MonsterBehaviors.cs:81-103`, `InteractionSystem.cs:160-172`, `FovCalculator`/`LightCalculator` lines (square `Line` *is* today's Bresenham, moved verbatim), `VisionSystem`/`InfraredVisionSystem`/`LightingSystem` rectangle scans → `Range` (behavior-pinning snapshots resolve rectangle-vs-disc questions in favor of today's output), `Extensions.cs:22-56` + `HasHeading.ToWorldDirection` marked square-legacy, `RotateTool` preset → `TurnStepDegrees`. Thread the config field; validator accepts only `"square"`.
**Gate:** the full existing test suite green, untouched, plus golden-master equivalence tests for every rewritten table.

### P1 — hexagon *(M/L)*

`HexTopology` (axial coords, cube distance, cube-lerp lines, hex-disc ranges, 60° facings); `HexCavesGenerator` declaring `SupportedTopologies=["hex"]`; a `hexhaven` test bundle under `Data/Games/`; validator accepts `"hex"`; hex property/golden/FOV-symmetry tests; a bundle-boot integration test; client-library doc note (hex layout math in `GridMapView`). Full hex rationale: [hexagonal-tiles.md](hexagonal-tiles.md).

### P2 — triangle *(M)*

`TriangleTopology` (parity machinery, BFS-reference-tested distance, supercover lines, BFS-ring ranges, `SnapHeading`, 120° turns). **No new generator** — mark `PerlinTerrainGenerator` topology-compatible (continuous noise is the free any-lattice terrain) and add a triangle test bundle. One protocol addition: `SelfCellParity` (0/1) on the perception frame — relative deltas alone can't tell a client whether its cell points up or down, and absolute coordinates are deliberately hidden.

### P3 — H3 stage-setting *(S; doc + one test artifact)*

`docs/h3-topology.md` (the packing plan, `cellToLocalIj` perception keys, hierarchy-via-portals posture, `Delta`-as-azimuthal-projection, dependency assessment with vendoring fallback) **plus one code artifact**: a mock `PentagonishTopology` — an intentionally irregular grid — running through the property harness in CI as the permanent guard that no uniform-direction assumption regresses into the seam.

## YAML, validator, worldgen, protocol

- **Bundle:** `world.topology:` optional string, default `"square"` — Emberfall, Neonveil, and Aphelion are untouched and byte-identical.
- **Validator:** topology must be in the registry (error); the chosen generator must support it (error).
- **Worldgen compatibility:** optional `ITopologyAwareGenerator { IReadOnlyCollection<string> SupportedTopologies }`; generators that don't implement it are implicitly `["square"]` — zero edits to existing generators; checked at bundle load and again at `CreateWorldAsync`.
- **Protocol:** add `Topology` (string) to `WorldInfo`/`GameStateDto` so clients pick their layout math. Unchanged: `Visuals` relative keys, `RelativeDirection`, `VisibleBounds` (still the axial bounding box). `WorldDirection` remains as square-world cosmetics; **degrees are the documented source of truth**. Per-topology cell layout (axial→world transforms, mesh orientation) is client-side, in the Unity client library's `GridMapView`/ThemeAsset ([design suite](design/unity-sample/unity-client-library.md)).

## Test plan

- **P0:** golden-master equivalence for every rewritten direction/distance table + ASCII FOV fixtures captured *before* refactoring; the existing full suite as the regression net.
- **Every topology:** the 8-invariant property harness; per-topology golden cases (Red Blob hex references; triangle parity-flip and tie-break tables); FOV symmetry tests.
- **Config:** validator tests (unknown topology, generator mismatch, omitted→square); Orleans round-trip + grain reactivation tests for the new field; per-topology bundle-boot integration tests.

## Non-goals

No conversion of existing square content; no mixed topologies within one map; no H3 implementation or dependency now; no client rendering implementation; no pathfinding rework beyond neighbor substitution; no change to `WorldLocation`'s type, wire shape, or hashing; no FOV architecture change (per-cell raycast stays).

## Risk register

| Risk | Mitigation |
|---|---|
| P0 behavior drift across 12+ rewritten call sites | Golden masters captured before each refactor; full-suite gate; ambiguities resolved in favor of today's output |
| Orleans serialization breakage (Id reuse; pre-topology persisted state) | Append-only `[Id]`s; `"square"` defaults everywhere; round-trip + reactivation tests |
| Hot-path perf (`WorldLocation` string hash; enumerator allocation in FOV loops) | `GridCoord` struct for all topology math; uniform-direction table fast paths; FOV benchmark before/after P0 |
| Game-feel disputes (hex L/R, triangle Backward) | All semantics live in one method (`ResolveRelative`) with documented deterministic tie-breaks; degrees-first protocol lets clients build richer turn UIs without server changes |
| H3 lock-in (a hidden uniform-grid or global-plane assumption surviving to the H3 phase) | Per-cell direction APIs only; `Delta` not `CellCenter` in runtime systems; direction indices banned from persistence/wire; the CI pentagon-mock harness |

## Asset note

Tiling changes touch only *structural* tile assets — characters, creatures, props, and audio are grid-agnostic (see [hexagonal-tiles.md](hexagonal-tiles.md#asset-impact-summary--full-analysis-in-assetsmd) for the hex asset landscape: good CC0 terrain packs exist; sci-fi interiors are make-in-project either way). Triangle tiles have essentially **no** free-pack ecosystem — a triangle world's kit is make-in-project by necessity, though it's tiny (one up-tri floor, one down-tri floor, one edge wall). H3 planetary worlds would be procedural terrain, not kit-based.

## When to pull this

Not before the Aphelion sample ships its M0 (per project direction). The cheap insurance meanwhile, unchanged from the hex doc: route any *new* adjacency/distance code through the central helpers rather than adding inline offset math, and keep degrees as the heading source of truth in everything new. When a game concept actually wants hex (tactics, overworld) or H3 (planetary survey, moon bases — the user has already run H3 on lunar coordinates), P0 is the first slice pulled, and it pays for itself in regression safety even if no second topology ever ships.
