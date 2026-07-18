## 1. Phase 1 — Single-line generation
- [ ] 1.1 Add `TransitNetworkPass` scaffolding on the generator pipeline (model on `PortalNetworkPass`)
- [ ] 1.2 Place station sites with `PoissonDiscSampling` (spacing biased to population/POIs)
- [ ] 1.3 Connect stations into a route with `MinimumSpanningTree` (ordered station list)
- [ ] 1.4 Add `CorridorProfile` and a wide-corridor carver generalizing `RiverCarverFeature`
- [ ] 1.5 Add `Station` entities (line id, schedule ref, platform tiles, board hotspot) for one surface line
- [ ] 1.6 Add per-world transit config (enabled modes, spacing, profile) threaded through world creation

## 2. Phase 2 — Multi-level & modes
- [ ] 2.1 Assign an altitude band per segment (from `add-flying-entities` bands)
- [ ] 2.2 Route with band-aware passability so lines grade-separate (viaduct at +3, subway at -2)
- [ ] 2.3 Add subway (below) and monorail (above) modes with their corridor profiles
- [ ] 2.4 Stamp ramps/portals where a line changes band (subway entrance, viaduct on-ramp)
- [ ] 2.5 Add a `GenerationValidationService` rule: connectivity, station reachability, no illegal same-band overlap

## 3. Phase 3 — Scheduled services
- [ ] 3.1 Add `ServiceGrain` / route definition (ordered station list + timetable)
- [ ] 3.2 Recurring dispatch via `EventSchedulerGrain.ScheduleRecurringEventAsync` on the configured headway
- [ ] 3.3 Service follows a Scheduled plan station-to-station on the existing tick chain
- [ ] 3.4 Station dwell: board waiting passengers, alight arriving passengers, then loop
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
