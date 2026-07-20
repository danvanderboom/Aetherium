## Why
Transportation infrastructure in Aetherium is not yet a first-class, procedurally-generated feature the way rivers and roads are, and nothing runs services on it. This change makes transit networks (rail, road, subway, monorail, bus) generated content with stations, multi-level interleaving geometry, wide inhabitable corridors, and passenger-carrying services (scheduled, ad-hoc, manual) — giving cities living transportation the player can ride and explore.

## What Changes
- Add a new `transit` capability.
- `TransitNetworkPass`: generate one or more lines per enabled mode (rail/road/subway/monorail/bus), placing stations with Poisson-disc spacing and connecting them with a minimum spanning tree.
- Altitude-band assignment per segment so lines grade-separate and interleave (subway below, street at grade, monorail above) — reuses altitude bands from `add-flying-entities`.
- `CorridorProfile` cross-section carver: corridors are multiple tiles wide (tracks/lanes plus flanking platforms/walls/shop strips), generalizing `RiverCarverFeature`.
- `Station` entities with line id, schedule reference, platform tiles, and a board hotspot; lightweight bus stops along city streets.
- Scheduled services: `ServiceGrain` plus recurring dispatch, station dwell, board/alight, and headway looping.
- AdHoc services: summon/hail a vehicle that routes to the player and offers a destination menu via multi-option selection (reuses `add-xbox-controller-unity`).
- Manual piloting: a pilot-seat hook that drives a vehicle directly with no plan.
- `TransitVenuePass`: stamp prefab venues (shops/eateries/rooms) into wide underground/elevated corridors as walkable content.
- All behavior is per-world data (enabled modes, densities, band ranges, corridor profiles, timetables).

## Impact
- Affected specs: `transit` (NEW capability).
- Depends on: `add-flying-entities` (altitude bands / layered passability, flight plans) and `add-boardable-vehicles` (multi-tile footprints, interiors, boarding); reuses the `add-xbox-controller-unity` multi-option selection UI.
- Affected code (future implementation):
  - `Aetherium.Server/WorldGen/Passes/TransitNetworkPass.cs`, `TransitVenuePass.cs` (new)
  - `Aetherium.Server/Transit/CorridorProfile.cs`, `ServiceGrain` (new)
  - `Aetherium.Server/Components/Station.cs` (new)
  - Reuses `WorldGen/Algorithms/Graphs/MinimumSpanningTree.cs`, `Sampling/PoissonDiscSampling.cs`, `Features/RiverCarverFeature.cs`, `Prefabs/PrefabStamper.cs`, `Events/EventSchedulerGrain.cs`, `Simulation/WorldClock.cs`
- Design reference: `docs/design/transit-networks.md`.

## Current implementation status (reconciliation, 2026-07-20)

The H3 living-systems work landed a **narrow slice of this vision, H3-only**, ahead of this
change: `Aetherium.Server/WorldGen/Generators/Outdoor/H3TransitNetwork.cs` (opt-in `transit=1`
on `H3TerrainGenerator`) generates a **rail MST backbone between the major cities on band 0** and
**subway tunnels a couple of bands underground** (`subwayBand`, default −2) from each capital to its
nearest cities. Both **carve real terrain** along the geodesic and register **high-capacity
`TradeLinks`** so `EconomySystem` routes bulk freight over them; the subway is a real place the
perception slab surfaces. This proves, on H3, the two hardest ideas in Phases 1–2:

- **MST line generation** (task 1.3) — demonstrated, but over *settlements*, not Poisson-disc *stations*.
- **Altitude-band grade separation** (tasks 2.1–2.2) — demonstrated: rail at band 0, subway below,
  genuinely non-colliding across bands.

Everything that makes transit a thing a **player rides** is still unbuilt, and is the real remaining
scope of this change:

- **Generic, topology-agnostic pass.** `H3TransitNetwork` is bespoke to H3 geodesics; the proposed
  `TransitNetworkPass` (square/hex/H3, Poisson-disc stations, `PortalNetworkPass` model) does not exist.
- **`Station` entities + board hotspots** (task 1.5) — none; the current lines have no stops.
- **Wide `CorridorProfile` carver** (task 1.4) — the current carve is a single-cell geodesic stamp,
  not a multi-tile cross-section; nothing to inhabit yet.
- **Monorail (above), ramps/portals at band changes, validation rule** (tasks 2.3–2.5) — none.
- **Scheduled / AdHoc / Manual services** (Phases 3–4) — none. This is where `add-boardable-vehicles`
  (now fully built: `VehicleGrain`, timed reminder-driven voyages, boarding, interiors) becomes the
  engine a `ServiceGrain` drives station-to-station.
- **Inhabited corridors / `TransitVenuePass`** (Phase 5) — none.

Net: the generation *concepts* are proven on H3 as **economy infrastructure**; the **player-facing,
rideable** feature (stations → boardable scheduled services on the existing lines, reusing the boardable-
vehicles voyage machinery) is the natural next slice and the bulk of this change. Task checkboxes below
are left unchecked because they specify the *generic* pass + rideable services, which `H3TransitNetwork`
does not provide.
