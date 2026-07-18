## 0. Phase 0 - Session -> world/map perception re-point (foundation)
- [ ] 0.1 Build grain -> session hydration: construct a session's ECS `World` + view location from a target world/map grain
- [ ] 0.2 Finish `GameHub.JoinWorld` (currently returns "not yet supported") to load the target world/map into the session and push a fresh perception
- [ ] 0.3 Close the `GameHub.UsePortal` cross-world `// TODO: Load new world/map into session` using the same re-point path
- [ ] 0.4 Update `GameSession.WorldId` and `WorldGrain.PlayerLocations` together on every re-point; add an invariant guard
- [ ] 0.5 Tests: re-point switches perception to the target map and streams a fresh frame; both sources of truth agree

## 1. Phase 1 - Footprint / multi-tile occupancy
- [ ] 1.1 Add `Footprint` component (box `Size3d` or explicit relative `Cells`; `OccupiedTiles(anchor)` enumerator, mirroring `WorldChunk.AllLocations`)
- [ ] 1.2 Index every occupied tile on `World.AddEntity`/`RemoveEntity` (extend `EntitiesByLocation` or add a parallel `FootprintIndex`), guarded by `Has<Footprint>()`
- [ ] 1.3 Make `World.TryMove`/`TryPlace` footprint-aware: validate all destination tiles for passability and collision; no two footprints overlap
- [ ] 1.4 Footprint-aware landing validity: every tile under the footprint valid landing terrain, in-bounds, and unoccupied
- [ ] 1.5 Preserve the single-tile fast path for entities without a `Footprint`
- [ ] 1.6 Tests: placement validates all tiles; move blocked if any tile impassable/occupied; single-tile entities unaffected

## 2. Phase 2 - Interior + boarding (parked vehicle)
- [ ] 2.1 Define vehicle definition data (`VehicleConfig`): exterior footprint, interior source + spawn dock, landing rules, capacity, board hotspot
- [ ] 2.2 Author a ship interior in `SpaceHackWorldBuilder` (or via `PrefabStamper`)
- [ ] 2.3 Add `IVehicleGrain`/`VehicleGrain`: `InitializeAsync` creates the interior via `AddMapAsync`; `LandAsync` places the exterior footprint on a surface
- [ ] 2.4 Implement `BoardAsync` (`MovePlayerToMapAsync` into interior + re-point each session) and `DisembarkAsync` (reverse onto valid surface tiles)
- [ ] 2.5 Wire a `board`/`use` action on the exterior board-hotspot tile
- [ ] 2.6 Tests: board a parked ship, walk the interior, disembark onto the surface (no travel yet)

## 3. Phase 3 - Timed voyage
- [ ] 3.1 `DepartAsync`: remove exterior footprint from the origin surface, compute ETA (10-30 min) via `WorldClock`, mark `InTransit`
- [ ] 3.2 Make `VehicleGrain : IRemindable` and register an Orleans reminder to self-drive the voyage
- [ ] 3.3 Each reminder wake before ETA: tick the interior map (`GameMapGrain.TickAsync`) and push a voyage-progress HUD update
- [ ] 3.4 On ETA: place the exterior footprint at the destination surface dock, unregister the reminder, transition to `Landed`
- [ ] 3.5 Recovery: re-arm the reminder from persisted `etaGameTime` on grain activation (cluster-restart edge case)
- [ ] 3.6 Tests: voyage advances over time; arrival re-docks; disembark on the destination surface

## 4. Phase 4 - In-transit events
- [ ] 4.1 On departure, schedule mid-voyage events via `EventSchedulerGrain.ScheduleEventAsync` at game-time offsets
- [ ] 4.2 On a due event, broadcast to passengers via `IEventInstanceGrain.BroadcastToAreaAsync` while the interior keeps ticking
- [ ] 4.3 Tests: a scheduled in-transit event fires and is broadcast to everyone aboard

## 5. Validation
- [ ] 5.1 `openspec validate add-boardable-vehicles --strict` passes with zero errors
