## Context
Aetherium's PCG stack (`WorldGenerationOrchestrator` + `GeneratorPipeline` running ordered `IWorldGenerationPass` steps) already lays networks (`PortalNetworkPass`) and carves wide linear features (`RiverCarverFeature`). This change extends that stack to generate transit networks and runs passenger services on them. Full design: `docs/design/transit-networks.md`.

## Goals / Non-Goals
- Goals: PCG passes generate multi-level, interleaving transit networks (rail/road/subway/monorail/bus) with stations and wide corridors; scheduled/ad-hoc/manual services carry passengers; wide corridors host prefab venues; everything is per-world data.
- Non-Goals: vehicle interior/boarding mechanics (owned by `add-boardable-vehicles`); the depth camera (`add-adaptive-depth-visualization`); realistic traffic/signaling/congestion simulation.

## Decisions
- **Two-part decomposition: PCG network generation + runtime services.**
  - *Generation (PCG):* `TransitNetworkPass` builds one or more lines per enabled mode — `PoissonDiscSampling` for station spacing, `MinimumSpanningTree` for connectivity, a per-segment altitude band for grade separation, and a `CorridorProfile` carver (generalizing `RiverCarverFeature`) for wide, multi-tile corridors. `TransitVenuePass` runs afterward, stamping prefab venues into wide corridors.
  - *Services (runtime):* a service is a footprint vehicle following a route/flight plan. `ServiceGrain` + recurring dispatch (`EventSchedulerGrain`) drives Scheduled services; a summon/hail affordance generates AdHoc plans with a destination menu; a pilot-seat hook gives Manual control. Services ride the existing `WorldGrain -> GameMapGrain -> MapRegionGrain` tick chain.
- **Grade separation falls out of altitude bands, not new collision code:** a viaduct at band +3 simply does not obstruct the street at band 0; a subway tube at -2 shares the same (x,y) column freely.
- **Per-world data:** enabled modes, densities, band ranges, corridor profiles, and timetables are configuration threaded through world creation (data-vs-behavior split), never hardcoded.

## Dependencies
- **`add-flying-entities` — altitude bands.** Per-band passability (z-order obstruction) is what lets subway (-2), street (0), and monorail (+3) segments share an (x,y) column without colliding. Services also reuse the `FlightPlan` follower and its plan sources (Scheduled / AdHoc / Manual) rather than inventing a new mover.
- **`add-boardable-vehicles` — footprints.** A train car or large vehicle is a multi-tile `Footprint` entity; boarding, interiors, and passenger transport for large services reuse this wholesale. Single-tile pods can ship first if footprints land later.
- **`add-xbox-controller-unity` — multi-option selection.** The AdHoc destination menu reuses the existing multi-option selection UI/flow.

## Risks / Trade-offs
- Multi-level routing can self-intersect or strand stations -> add a `GenerationValidationService` rule (connectivity, station reachability, no illegal same-band overlap).
- Legibility depends on the depth camera (`add-adaptive-depth-visualization`) -> sequence alongside Phase 2.
- Simulation cost of many vehicles ticking across bands -> cap active services, use headway-based spawning, and only fully simulate services near players (area-of-interest).
- Footprint dependency -> single-tile pods can ship before `add-boardable-vehicles` footprints land, then upgrade to multi-car services.

## Migration Plan
Additive only: a new `transit` capability plus new PCG passes and a services grain. Grounded (non-transit) behavior is unchanged; transit is inert unless a world's config enables one or more modes. Delivery is phased per `tasks.md`: single line -> multi-level & modes -> scheduled -> ad-hoc/manual -> inhabited corridors.

## Open Questions
- Do large services require `add-boardable-vehicles` footprints at Phase 3, or ship single-tile pods first and upgrade later?
- Should bus routes bind to existing `GridCityGenerator` / `OrganicCityGenerator` street output, or generate independently?
