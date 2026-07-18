# Design: Transit Networks (Rails, Roads, Subways, Bus Routes)

**Status:** Draft design · **OpenSpec change:** `add-transit-networks` · **Depends on:** [`flying-entities`](flying-entities.md), [`boardable-vehicles`](boardable-vehicles.md)

## Summary

Make **transportation infrastructure a first-class, procedurally-generated feature** — the way roads and
rivers already are — and run **services** on it. That means:

- **Generated networks:** surface **roads** and **rail**, elevated **monorails**, underground **subway
  tunnels**, and **bus routes** on city maps — as network features with **stations/stops**.
- **Multi-level, interleaving** layouts: lines stack several bands **deep and tall** (subway at −2/−3,
  street at 0, monorail viaduct at +3), crossing like a complex freeway interchange — enabled by the
  altitude-band/z-order obstruction model from [`flying-entities`](flying-entities.md).
- **Wide corridors:** lines are **multiple tiles wide** (several tracks/lanes) with a cross-section profile,
  not one-tile lanes. Some are wide enough to be places — **inhabited subway concourses** with shops,
  restaurants, bars, and adventure rooms along the tunnels.
- **Services** that carry passengers, sourced three ways (per [`flying-entities`](flying-entities.md) flight
  plans): **Scheduled** (timetabled trains/buses/subways at stations), **AdHoc** (summon an air taxi; pick a
  destination), and **Manual** (sit in the cockpit and drive — no plan).

## Goals / Non-Goals

**Goals**
- New PCG passes/features generate transit networks (rail/road/subway/bus) with stations and multi-level,
  interleaving geometry.
- Lines carry a **cross-section profile** (width, track/lane count, flanking structures).
- **Scheduled services**: vehicles follow timetabled routes, dwell at stations, board/alight passengers —
  the "wait at the stop for the next departure" experience.
- **AdHoc services**: summon/hail a vehicle; choose a destination; it routes and carries you.
- **Manual piloting** hook for player-driven vehicles.
- Underground/elevated corridors can host **venues** (prefab interiors) as adventure content.
- Everything is **per-world data**: which modes, densities, timetables, band ranges.

**Non-Goals (this change)**
- The vehicle interior/boarding mechanics themselves (that's [`boardable-vehicles`](boardable-vehicles.md);
  this change *uses* it).
- The depth camera (that's [`adaptive-depth-visualization`](adaptive-depth-visualization.md); this change
  *needs* it to be legible, but doesn't build it).
- Realistic traffic simulation / signaling / congestion (future).

## Current state (grounding)

The PCG stack is a strong fit — we extend it rather than invent:
- **Pipeline & passes:** `WorldGenerationOrchestrator` + `GeneratorPipeline` run ordered
  `IWorldGenerationPass` steps (`Aetherium.Server/WorldGen/Passes/…`). `PortalNetworkPass` already lays a
  *network of link points* across a map — the template for a `TransitNetworkPass`.
- **Features:** `IGenerationFeature` implementations stamp content; **`RiverCarverFeature`
  (`Aetherium.Server/WorldGen/Features/RiverCarverFeature.cs`) is the model for a wide linear feature** —
  it carves a variable-width path via `World.SetTerrain` over `Size3d` spans. A transit corridor is the same
  shape with a richer cross-section and a band.
- **Algorithms ready to reuse:** `MinimumSpanningTree`
  (`WorldGen/Algorithms/Graphs/MinimumSpanningTree.cs`) to connect stations into a line/network;
  `PoissonDiscSampling` (`…/Sampling/`) to place stations with spacing; `PerlinNoise`, `FloodFill`.
- **City generators:** `GridCityGenerator`, `OrganicCityGenerator`
  (`WorldGen/Generators/Cities/…`) already produce street layouts — the anchor for **bus routes** and
  surface roads.
- **Prefabs for venues:** `PrefabStamper` + `PrefabLibrary` (`WorldGen/Prefabs/…`) stamp fixed layouts —
  the mechanism for shops/bars/adventure rooms along a concourse.
- **Timetables:** `EventSchedulerGrain.ScheduleRecurringEventAsync(intervalHours)` + `WorldClock`
  (real↔game time) drive departures; the tick chain runs services.

Two prerequisites come from sibling changes: **altitude bands** (multi-level lines) from `flying-entities`,
and **footprints** (a train car occupies many tiles; it's a small boardable vehicle) from `boardable-vehicles`.

## Network generation

### Lines, stations, and the graph
- A **`TransitNetworkPass`** builds one or more **lines** per enabled mode. Per line:
  1. Choose **anchors** (POIs/districts) and **station** sites via `PoissonDiscSampling` (spacing) biased to
     population/POIs.
  2. Connect them into a route with `MinimumSpanningTree` (or a path for a simple line; a small graph for a
     network), yielding an ordered station list.
  3. Assign a **band** per segment (subway −2, street 0, monorail +3) and route around/over/under obstacles
     using band-aware passability — **grade separation falls out of the z-order model**: a viaduct simply
     occupies +3 and doesn't obstruct the street at 0.
  4. **Carve the corridor** with the cross-section profile (below), stamping track/lane terrain, platforms at
     stations, ramps/portals where a line changes band (a subway entrance, a viaduct on-ramp).
- **Stations/stops** are entities with a `Station` component (line id, schedule ref, platform tiles, a
  **board hotspot** like a vehicle's) plus, for subways/rail, a platform sub-area; bus **stops** are
  lightweight surface markers along city streets.

### Cross-section profile (wide corridors)
```csharp
// Aetherium.Server/Transit/CorridorProfile.cs (new)
public class CorridorProfile
{
    public int Width;             // total tiles across
    public int Tracks;            // number of running ways (rails/lanes)
    public int Band;              // altitude band of the running surface
    public string RunningTerrain; // "Rail","Road","MaglevGuideway"
    public IReadOnlyList<FlankProfile> Flanks; // platforms, walls, service ways, shopfront strip
}
```
The carver lays `Width` tiles perpendicular to the line direction each step (generalizing `RiverCarverFeature`),
placing running terrain in the center and flank structures (platform, wall, shop strip) on the sides. A
"quite wide" subway trunk = many tracks + broad flanking concourse.

### Inhabited tunnels / concourses (adventure spaces)
- A **`TransitVenuePass`** (runs after the network) stamps **prefab venues** into the flank strips and
  concourse levels of wide underground/elevated corridors: shops, restaurants, bars, maintenance rooms,
  hideouts. These are ordinary walkable map areas (populated via prefabs + `SpawnNPCsFeature`), some behind
  doors, some instanced ([`boardable-vehicles`] interior/instance pattern) for adventures.
- Result: the −1/−2 bands of a big interchange aren't just track — they're a neighborhood you can explore
  while waiting for a train.

## Services (who runs on the network)

A **service** is a vehicle (a footprint entity, small = one car, large = boardable per
[`boardable-vehicles`](boardable-vehicles.md)) following a [flight/route plan](flying-entities.md):

- **Scheduled** — a `ServiceGrain`/route definition holds an ordered station list + timetable. A recurring
  scheduler (`EventSchedulerGrain.ScheduleRecurringEventAsync`) spawns/dispatches the vehicle; it follows a
  **Scheduled** plan station-to-station, **dwelling** at each to board/alight, and loops. Passengers **wait
  at a station** and board on arrival — bus/train/subway UX. (Subways and monorails are the same logic at
  different bands with different corridor profiles.)
- **AdHoc** — a player uses a **summon/hail** affordance (kiosk, app, whistle). The nearest idle vehicle
  generates an **AdHoc** plan to the caller, arrives, and — after boarding — presents a **destination menu**
  (reusing the multi-option selection UI already built in `add-xbox-controller-unity`); the choice becomes
  the next AdHoc leg. Air taxis, robo-cabs, chartered dropships.
- **Manual** — the player takes a pilot seat; the vehicle has **no plan** and is driven directly with the
  [dual-stick controls](gamepad-dual-stick.md) (thrust/strafe/yaw/climb). Leaving the seat returns control
  to the avatar.

Boarding/interiors/passenger transport for large services reuse [`boardable-vehicles`](boardable-vehicles.md)
wholesale; small services (a single-tile pod) can just attach the rider and move them with the vehicle.

## Data model (per-world)
```jsonc
"transit": {
  "bands": { "subway": -2, "deepMetro": -3, "street": 0, "monorail": 3 },
  "modes": {
    "subway":   { "enabled": true, "profile": { "width": 9, "tracks": 2, "band": -2, "runningTerrain": "Rail",
                                                "flanks": ["platform","shopStrip","wall"] },
                  "stationSpacing": 24, "venues": ["shop","cafe","bar"] },
    "monorail": { "enabled": true, "profile": { "width": 5, "tracks": 2, "band": 3, "runningTerrain": "MaglevGuideway" },
                  "stationSpacing": 40 },
    "bus":      { "enabled": true, "onStreets": true, "stopSpacing": 12 }
  },
  "services": [
    { "id": "blue-line", "mode": "subway", "headwayGameMinutes": 6, "cars": 4 },
    { "id": "sky-1",     "mode": "monorail", "headwayGameMinutes": 10, "cars": 3 }
  ]
}
```

## Phasing
- **Phase 1 — Single-line generation.** `TransitNetworkPass` + `CorridorProfile` carver + `Station` entities
  for one surface line. Wide corridor, stations, board hotspots.
- **Phase 2 — Multi-level & modes.** Band assignment + grade separation; subway (below) and monorail (above);
  ramps/portals between bands. Interleaving interchanges.
- **Phase 3 — Scheduled services.** `ServiceGrain` + recurring dispatch + station dwell + board/alight;
  "wait for the next train" loop.
- **Phase 4 — AdHoc & Manual.** Summon/hail + destination menu (AdHoc); pilot-seat (Manual) hook.
- **Phase 5 — Inhabited corridors.** `TransitVenuePass` stamps shops/bars/adventure rooms into wide tunnels.

## Risks & trade-offs
- **Generation complexity/validation.** Multi-level routing can self-intersect or strand stations; add a
  `GenerationValidationService` rule (connectivity, station reachability, no illegal same-band overlap).
- **Legibility depends on the depth camera.** A ten-level interchange is unreadable without
  [`adaptive-depth-visualization`](adaptive-depth-visualization.md); sequence that alongside Phase 2.
- **Simulation cost.** Many vehicles ticking across bands; cap active services, use headway-based spawning,
  and only fully simulate services near players (area-of-interest, like `EventInstanceGrain`).
- **Footprint dependency.** Wide/long vehicles need `boardable-vehicles` footprints; single-tile pods can
  ship first if that lands later.

## Key source references
- Pipeline/passes: `Aetherium.Server/WorldGen/WorldGenerationOrchestrator.cs`, `GeneratorPipeline.cs`,
  `Passes/PortalNetworkPass.cs`, `IWorldGenerationPass.cs`, `IGenerationFeature.cs`
- Wide linear feature model: `Aetherium.Server/WorldGen/Features/RiverCarverFeature.cs`; `World.SetTerrain(…, Size3d)`
- Algorithms: `WorldGen/Algorithms/Graphs/MinimumSpanningTree.cs`, `…/Sampling/PoissonDiscSampling.cs`
- City layouts: `WorldGen/Generators/Cities/GridCityGenerator.cs`, `OrganicCityGenerator.cs`
- Venues: `WorldGen/Prefabs/PrefabStamper.cs`, `PrefabLibrary.cs`; `WorldGen/Features/Population/SpawnNPCsFeature.cs`
- Timetables/services: `Aetherium.Server/Events/EventSchedulerGrain.cs`, `Simulation/WorldClock.cs`; tick chain
- Reused: `boardable-vehicles` (footprints, interiors, boarding), `flying-entities` (bands, flight plans)
