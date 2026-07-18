## 1. Altitude Bands & Layered Passability
- [x] 1.1 Add a `Height`/band-extent to `ObstructsMovement` and give `ObstructsView` the same band extent
- [x] 1.2 Generalize `World.PassableTerrain` into a band-aware `IsPassable(cell, forEntity)` that consults per-band obstruction
- [x] 1.3 Add per-world band range/labels and a terrain→obstruction-height table to world config
- [x] 1.4 Keep grounded band-0 entities on the existing fast path (guard behind `Has<Flight>()`)
- [x] 1.5 Headless tests: layered movement/sight/light (bridge, glass skylight, higher-band clearance)

## 2. Flight Capability
- [x] 2.1 Add the `Flight` component (`MinBand`/`MaxBand`/`CruiseBand`/`CanLand`/`State`) — landed early in Phase 1 as data to support the `Has<Flight>()` guard; airborne wiring below remains
- [x] 2.2 In `TryMove`, let airborne horizontal moves ignore ground-band obstruction
- [x] 2.3 Allow free vertical movement within `[MinBand, MaxBand]` without `CanAscend`/`CanDescend`
- [x] 2.4 Attach `Flight` from per-world/spawn data in `SpawnEntityAsync`
- [x] 2.5 Headless tests: airborne traversal over impassable ground; band-range clamping

## 3. Flight-Plan Follower
- [x] 3.1 Add the `FlightPlan` component (`Source`, `Legs`, `Loop`, `PatternId`, `Cursor`)
- [x] 3.2 Implement the tick-driven follower on the map tick chain, stepping one move per tick (cadence-pacing integrates with `add-held-key-repeat-movement`)
- [x] 3.3 Implement Patterned generators: `orbit`, `patrol`, `wander`, `hover`
- [x] 3.4 Implement leg advance / loop modes (`Once`/`Loop`/`PingPong`) and arrival behavior
- [x] 3.5 Apply the per-world semicircular cruise rule and collision policy (`separated`/`collidable` + event)
- [x] 3.6 Headless tests: each pattern; cruise-band separation; collision event

## 4. Landing & Takeoff
- [x] 4.1 Add `land`/`takeoff` tools invoking the `Airborne ↔ Landing/TakingOff ↔ Landed` state machine
- [x] 4.2 Gate landing on `Flight.CanLand` + per-world valid landing terrain + unoccupied cell
- [x] 4.3 Return the flyer to `CruiseBand` on takeoff; wire flight-plan arrival behavior to invoke `land`
- [x] 4.4 Headless tests: valid/invalid/occupied landing; takeoff-to-cruise

## 5. Flyer Interaction
- [x] 5.1 Surface altitude-aware affordances per flyer type (`FlyerProfile` + `FlyerInteractionSystem.Affordances` + `flyer-affordances` tool)
- [x] 5.2 Implement `hack` (range/uplink, no adjacency) for orbital satellites (`TryHack` + `hack` tool + `Hacked` marker)
- [x] 5.3 Implement `summon`/`hail` → AdHoc plan + land for air taxis; `attack`/`shoot` for low drones (`TrySummon`/`TryAttack` + `summon`/`attack-flyer` tools)
- [x] 5.4 Include nearby-band flyers in perception — delivered by the 3D occluded perception slab (`add-adaptive-depth-visualization` Section 1): a flyer in a nearby band, horizontally visible and not occluded, is now emitted in the `PerceptionDto` tagged with its `relativeZ` when the world's slab depth is set. (`FlyerInteractionSystem.FlyersInRange` remains the range-based targeting primitive.)
- [x] 5.5 Tests: hack-at-range; summon flow; altitude-gated affordances (`FlyerInteractionTests`, 7)
