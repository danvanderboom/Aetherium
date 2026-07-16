# H3 as an Aetherium Topology — Stage-Setting

*Status: forward-looking design (P3 of [grid-topologies.md](grid-topologies.md)). No H3 code or dependency exists yet. The [seam](grid-topologies.md#the-abstraction) (P0), hex (P1), and triangle (P2) are built and green; this document records exactly how Uber's **H3** hierarchical geospatial grid drops into that same `IGridTopology` when a planetary/lunar game wants it — and the one code artifact that already ships to keep the seam ready: the [pentagon-mock CI guard](#the-pentagon-mock-already-in-ci).*

## Why H3, and why it fits

[H3](https://h3geo.org/) tiles a sphere with hexagons at 16 nested resolutions, with exactly **12 pentagons per resolution** (an unavoidable consequence of tiling a sphere with hexagons). It is normally used for Earth analytics, but the projection is body-agnostic — the user has already run H3 over **lunar** coordinates, and it applies to any planet, moon, or planetoid. An Aetherium "world" whose map is a shell of H3 cells at a chosen resolution is a station-free, open-world planetary surface.

The generalized abstraction was shaped so that H3 is **an implementation, not a redesign**. The four things H3 adds each land on machinery that already exists and is already tested:

| H3 property | Lands on | Already proven by |
|---|---|---|
| 12 pentagons per resolution (5 neighbors) | Rule 1 — per-cell `DirectionCount`/`Steps` | Triangle's parity direction sets **and** the [pentagon-mock](#the-pentagon-mock-already-in-ci) running the full invariant harness |
| 64-bit hierarchical cell index | The [`WorldLocation` packing](#cell-identity-packing-h3-into-worldlocation) | — (documented below; no wire change) |
| Spherical geometry (no global plane) | `Delta` as azimuthal projection; `Line`/`Range` via H3's own `gridPathCells`/`gridDisk` | The seam already forbids `CellCenter` in runtime systems (`IPlanarGridTopology` is not implemented by H3) |
| Multiple nested resolutions | `IHierarchicalGridTopology`; cross-resolution travel via the **existing portal system** | Portals are already topology-agnostic |

## Cell identity: packing H3 into `WorldLocation`

`WorldLocation(int X, int Y, int Z)` stays the universal key — no Orleans schema, SignalR protocol, or `MapState` change. A valid H3 index is a 64-bit value whose **top bit is reserved and always zero**, so the split is lossless:

```csharp
// H3 index  (ulong, top bit 0)  <->  WorldLocation(X, Y, Z)
static (int x, int y) Pack(ulong h3)   => ((int)(h3 >> 32), (int)(h3 & 0xFFFFFFFF));
static ulong          Unpack(int x, int y) => ((ulong)(uint)x << 32) | (uint)y;
// X = (int)(h3 >> 32) is always non-negative (reserved top bit); Y round-trips exactly.
// Z stays the vertical level (surface / sub-surface / orbital), orthogonal as always.
```

`GridCoord` (the hot-path struct) carries the same two ints; conversion at the seam boundary is unchanged. Direction indices remain **ephemeral** (invariant 6) — a pentagon simply has five, and nothing persists or wires the index.

## Perception on a sphere: `cellToLocalIj` keys

Player-relative perception is the engine's fairness contract: the client never learns absolute coordinates, only `"relX,relY,relZ"` deltas from its own cell. On a sphere a global "delta" has no meaning, but H3 provides **perceiver-anchored local coordinates**: `cellToLocalIj(origin, cell)` yields integer `(i, j)` in the neighborhood of `origin`. Perception keys become `"relI,relJ,relZ"` — still opaque strings, still no absolute coordinates, identical contract.

- `cellToLocalIj` is valid within a base-cell neighborhood, comfortably larger than any perception radius, so it never fails inside a viewport.
- It is marked **experimental** upstream — recorded here as a dependency risk; the fallback is to anchor perception on `gridDisk(origin, r)` + a local axial fit, which needs no experimental API.

## Geometry: `Delta` as azimuthal projection

`Delta(from, to)` is the only geometry runtime systems (vision cones, light falloff) may call — the seam quarantines absolute `CellCenter` onto `IPlanarGridTopology`, which **H3 does not implement**. For H3, `Delta` is the azimuthal (gnomonic) projection of the great-circle displacement between the two cell centers onto the tangent plane at `from`, in cell-size units. This is only ever evaluated at perception range, exactly where a tangent plane is an excellent approximation of the sphere. `Line`/`Range` delegate to H3's `gridPathCells`/`gridDisk`, which are defined directly on the sphere and need no plane at all.

## Multiple resolutions and hierarchy

One resolution per map — the same "one tiling per world" rule every topology follows. When multi-scale travel is wanted (survey from orbit → land at a base), it maps onto concepts the engine already has:

```csharp
public interface IHierarchicalGridTopology : IGridTopology
{
    int Resolution(GridCoord cell);
    GridCoord Parent(GridCoord cell);
    IEnumerable<GridCoord> Children(GridCoord cell);
}
```

- Implemented **only** by H3; square/hex/triangle never see it.
- Cross-resolution travel is **the existing topology-agnostic portal system** linking a coarse-resolution map to a fine-resolution one — no new hierarchy-traversal code in the movement path.
- Client zoom between resolutions is a rendering concern, not a server one.

## Worldgen

A planetary/lunar world is procedural, not kit-based: a generator declaring `SupportedTopologies=["h3"]` (via the shipped [`ITopologyAwareGenerator`](grid-topologies.md#p1--hexagon-ml--topology-built-dedicated-generatorbundle-remain-as-polish) seam) samples elevation/biome noise on the sphere (3-D noise over the cell-center unit vector) and assigns terrain per H3 cell. `PerlinTerrainGenerator`'s any-lattice property (already declared for square/hex/tri) extends naturally once it samples by cell center rather than raw `(x, y)`.

## Dependency assessment: `pocketken.H3`

[`pocketken.H3`](https://github.com/pocketken/H3.net) is a maintained **pure-C# port of H3 v4** — Apache-2.0, on NuGet, `netstandard2.0/2.1`+, **no native interop** (so it works in the Orleans silo and, importantly, in a future CoreCLR Unity client without a native plugin). Nothing in the solution references it today.

- **Needed calls:** `latLngToCell`/`cellToLatLng`, `gridDisk`, `gridPathCells`, `gridDistance`, `cellToLocalIj`, `cellToParent`/`cellToChildren`, `isPentagon`, `cellToBoundary`.
- **Risks:** small maintainer community; .NET 10 compatibility unverified until we try it.
- **Fallback:** vendor the minimal index-math subset (index bit-layout, `gridDisk`, `gridDistance`, pentagon detection) behind the topology seam — everything H3-specific is already confined to one `IGridTopology` implementation, so a vendored subset changes nothing outside `H3Topology`.

## The pentagon-mock (already in CI)

The one P3 artifact that ships now, so the seam cannot rot before H3 arrives: [`PentagonishTopology`](../Aetherium.Test/Topology/PentagonishTopology.cs) — an intentionally irregular grid (y-even rows are 5-neighbor "pentagons", y-odd rows are 6-neighbor hexagons) run through the same [8-invariant harness](../Aetherium.Test/Topology/GridTopologyInvariants.cs) every real topology passes ([`PentagonishTopologyTests`](../Aetherium.Test/Topology/PentagonishTopologyTests.cs)). It exercises exactly the property H3 will depend on — cells whose neighbor count is not the maximum — so if any seam or caller ever reintroduces a "cells all have N edges" or "there is a global plane" assumption, CI fails long before H3 is written.

## When to pull this

Not before a game concept actually wants a planetary or lunar surface. Until then the cost of readiness is zero — the seam, the per-cell direction machinery, the `Delta`-not-`CellCenter` discipline, and the pentagon-mock guard are all already in place and green. When the day comes, `H3Topology` is a single new `IGridTopology` implementation plus the `pocketken.H3` dependency; nothing above it changes.
