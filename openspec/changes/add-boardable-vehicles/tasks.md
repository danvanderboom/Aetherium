## 0. Phase 0 - Session -> world/map perception re-point (foundation)
- [x] 0.1 Build grain -> session hydration: reusable `GameSessionManager.RepointSessionAsync` (swaps the session World from a target-map snapshot builder, rebinds world/map, swaps gateway, pushes a fresh frame) — silo-side so both `GameHub` and grains can drive it
- [x] 0.2 `GameHub.JoinWorld` routed through the shared `RepointCallerToMapAsync` path (it already hydrated; now it also registers the world-grain player location)
- [x] 0.3 Close the `GameHub.UsePortal` cross-world `// TODO: Load new world/map into session` — both same-world and cross-world branches now re-point via the shared path
- [x] 0.4 `IWorldGrain.RegisterPlayerLocationAsync`/`UnregisterPlayerAsync` keep `WorldGrain.PlayerLocations` in agreement with the map-grain re-point; the shared helper updates both sides on every re-point
- [x] 0.5 Tests: `SessionRepointTests` — the location invariant (register/re-point/unregister, no count inflation) + `RepointSessionAsync` switches the map, pushes a fresh frame, and perception follows the new map; existing `Travel_Rebinds_Session…` isolation test still green

## 1. Phase 1 - Footprint / multi-tile occupancy
- [x] 1.1 `Footprint` component (box `Size3d` or explicit relative `Cells`; `OccupiedTiles(anchor)` enumerator, mirroring `WorldChunk.AllLocations`)
- [x] 1.2 Index every occupied tile on `World.AddEntity`/`TryRemoveEntity`/`MoveEntity` (idempotent multi-tile indexing over `EntitiesByLocation`), guarded by `Has<Footprint>()`
- [x] 1.3 `World.TryPlace`/`TryMoveFootprint` + `CanPlaceFootprint`: validate every destination tile for passability and collision; no two footprints overlap
- [x] 1.4 Footprint-aware landing validity: `CanPlaceFootprint` requires every tile under the footprint passable, in-bounds, and unoccupied — the landing check `VehicleGrain.LandAsync` will call
- [x] 1.5 Single-tile fast path preserved for entities without a `Footprint` (every footprint branch guarded by `Has<Footprint>()`)
- [x] 1.6 Tests: `FootprintOccupancyTests` — placement indexes all tiles / blocked by impassable, off-map, or overlap; move re-indexes & releases previous / blocked by a character; removal un-indexes all; single-tile fast path unchanged. Full suite (2508) green.

## 2. Phase 2 - Interior + boarding (parked vehicle)
- [x] 2.1 `VehicleConfig` (`Aetherium.Model/Vehicles`): exterior footprint (W/L/D), interior generator+size, landing terrain, capacity. Pure data, passed to `InitializeAsync` (not hardcoded).
- [x] 2.2 Interior authored as the "Main" map of the vehicle's own world, built by `VehicleConfig.InteriorGenerator` (a registered `IMapGenerator`). NOTE deviation: `AddMapAsync` takes a generator-type string and there is no `WorldBuilder`→generator bridge, so the empty `SpaceHackWorldBuilder` isn't used directly; a bespoke ship-interior generator/prefab is a follow-on.
- [x] 2.3 `IVehicleGrain`/`VehicleGrain` (modeled on `DungeonInstanceGrain`, auto-discovered, "worldStore"): `InitializeAsync` creates the interior world+map; `LandAsync` places the exterior footprint via `IGameMapGrain.PlaceVehicleExteriorAsync` (`VehicleExterior` entity + `Footprint` + `Boardable` + solid `ObstructsMovement`, landing-terrain-gated).
- [x] 2.4 `BoardAsync`/`DisembarkAsync`: cross-world re-point (interior is on the vehicle's own world) via `IGameMapGrain.JoinPlayerAsync` + `IWorldGrain.RegisterPlayerLocationAsync`/`UnregisterPlayerAsync` + `GameSessionManager.RepointSessionToMapAsync` (Phase 0). Capacity-bounded; surplus rejected.
- [x] 2.5 `board`/`disembark` interaction tools (auto-discovered, Player-profile-allowed). `board` targets the exterior entity; the hub validates adjacency via `IGameMapGrain.GetBoardableInfoAsync` (pure read → no grain re-entrancy) then calls the vehicle grain directly. `disembark` resolves the vehicle from the interior world id.
- [x] 2.6 Tests: `VehicleBoardingTests` — init creates a joinable interior; board moves a player into the interior & off the surface; disembark returns them; capacity rejects surplus; board requires a landed vehicle; boarding re-points a live session (fresh interior frame). Full suite 2513 passing.

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
