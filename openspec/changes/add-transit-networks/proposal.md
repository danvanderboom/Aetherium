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
