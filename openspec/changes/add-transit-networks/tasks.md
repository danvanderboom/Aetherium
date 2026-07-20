> **Status (2026-07-20):** see the reconciliation in `proposal.md`. Two slices exist:
> (a) `H3TransitNetwork` generates rail + subway lines as H3 *economy infrastructure* (band-separated,
> terrain-carving, `TradeLinks`), proving the MST-line and grade-separation ideas; and now
> (b) the **rideable-service slice** — `Station` markers on the rail line + a `TransitServiceGrain` that
> drives a train (a boardable `VehicleGrain`) station-to-station, dwelling then departing on a timed
> voyage, looping — reusing the whole `add-boardable-vehicles` voyage machinery. A player boards the
> parked train at one station and alights at the next (`TransitServiceTests`, `RailPlacesAStationMarker…`).
> Checked below: 1.5 (station markers), 3.1–3.4 (the service). Still unbuilt: the **generic,
> topology-agnostic `TransitNetworkPass`** (Poisson stations, wide `CorridorProfile`, per-world config
> threading — 1.1–1.4/1.6), monorail/ramps/validation (Phase 2), AdHoc/Manual (Phase 4), and inhabited
> corridors (Phase 5). Boarding is currently the player `board`/`disembark` path; auto-boarding NPC
> crowds and service caps (3.5) are follow-ons.

## 1. Phase 1 — Single-line generation
- [ ] 1.1 Add `TransitNetworkPass` scaffolding on the generator pipeline (model on `PortalNetworkPass`)
- [ ] 1.2 Place station sites with `PoissonDiscSampling` (spacing biased to population/POIs)
- [ ] 1.3 Connect stations into a route with `MinimumSpanningTree` (ordered station list)
- [ ] 1.4 Add `CorridorProfile` and a wide-corridor carver generalizing `RiverCarverFeature`
- [x] 1.5 Add `Station` entities for one surface line — `Station` component (line id + stop ordinal + name) on a `StationEntity`, placed by `TransitServicePlanner.PlaceStations` and wired into `H3TransitNetwork` at every rail stop. NOTE deviation: markers are non-obstructing single cells (the board hotspot is the docked train's `Boardable` exterior), not wide "platform tiles"; a schedule ref lives on the `TransitServiceGrain`, not the entity. Wide platform tiles = the `CorridorProfile` follow-on (1.4).
- [ ] 1.6 Add per-world transit config (enabled modes, spacing, profile) threaded through world creation

## 2. Phase 2 — Multi-level & modes
- [ ] 2.1 Assign an altitude band per segment (from `add-flying-entities` bands)
- [ ] 2.2 Route with band-aware passability so lines grade-separate (viaduct at +3, subway at -2)
- [ ] 2.3 Add subway (below) and monorail (above) modes with their corridor profiles
- [ ] 2.4 Stamp ramps/portals where a line changes band (subway entrance, viaduct on-ramp)
- [ ] 2.5 Add a `GenerationValidationService` rule: connectivity, station reachability, no illegal same-band overlap

## 3. Phase 3 — Scheduled services
- [x] 3.1 `ServiceGrain` / route definition — `TransitServiceConfig` (ordered `TransitStop`s + `HopMinutes`/`DwellMinutes`/`Loop` + the train `VehicleConfig`) driven by `TransitServiceGrain` (`Aetherium.Server/Transit`), which owns a train keyed off the service id.
- [x] 3.2 Recurring dispatch on the headway. NOTE deviation: fired by the grain's **own Orleans reminder** (`transit-dispatch`, 1-min, re-armed in `OnActivateAsync`), not `EventSchedulerGrain.ScheduleRecurringEventAsync` — same choice and rationale as boardable-vehicles Phase 4 (the EventScheduler path runs through the global tick driver the design avoids). `DispatchStepAsync` is also test-drivable, so the loop is deterministic despite the 1-min reminder floor.
- [x] 3.3 Service follows the plan station-to-station — `DispatchStepAsync` advances the train exactly one transition per step (arrive an in-flight train, or depart a dwelling one to the next stop), each hop reusing the train's `DepartAsync`/`ArriveAsync` voyage. Self-sufficient without the train's own reminder (it nudges `TickVoyageAsync`).
- [x] 3.4 Station dwell + loop — the train dwells `DwellMinutes` at each stop then departs to the next, looping (`Loop`) or halting at the terminus. NOTE deviation: "board waiting passengers / alight" is the player-driven `board`/`disembark` on the parked train (a boarded rider is carried in the interior to the next station and steps off there — `TransitServiceTests.BoardedPassenger_IsCarriedToTheNextStation_AndAlights`); auto-boarding NPC crowds is a follow-on.
- [ ] 3.5 Cap active services / area-of-interest simulation near players

## 4. Phase 4 — AdHoc & Manual
- [ ] 4.1 Add summon/hail affordance (kiosk/app/whistle) that generates an AdHoc plan to the caller
- [ ] 4.2 On boarding, present a destination menu via multi-option selection (reuse `add-xbox-controller-unity`)
- [ ] 4.3 Chosen destination becomes the next AdHoc leg
- [ ] 4.4 Add pilot-seat (Manual) hook: no plan, direct drive; leaving the seat returns avatar control

## 5. Phase 5 — Inhabited corridors
- [ ] 5.1 Add `TransitVenuePass` (runs after the network is generated)
- [ ] 5.2 Stamp prefab venues (shops/eateries/bars/rooms) into wide corridor flanks/concourses via `PrefabStamper`
- [ ] 5.3 Populate venues with NPCs (`SpawnNPCsFeature`); some behind doors / instanced
- [ ] 5.4 Verify wide underground/elevated corridors are walkable adventure content

## 6. Validation
- [ ] 6.1 `openspec validate add-transit-networks --strict` passes
- [ ] 6.2 Headless generation test: multi-level lines interleave without same-band collision; no stranded stations
- [ ] 6.3 Headless services test: recurring dispatch on headway; dwell boards waiting passengers
