## Context
See `docs/design/boardable-vehicles.md` for the authoritative design. A boardable vehicle is a large flying thing that behaves like a **flying building**. Rather than simulate a giant multi-tile object flying tile-by-tile with people standing on it, we decompose a vehicle into three linked pieces, each of which maps onto a primitive the engine already has (or nearly has):

| Piece | What it is | Existing primitive it reuses |
|-------|-----------|------------------------------|
| **Exterior** | A multi-tile footprint entity sitting on a planet's surface map | `Entity` + `WorldLocation` + a new `Footprint` component |
| **Interior** | A separate walkable map you board into | `IWorldGrain.AddMapAsync` map-within-a-world + the instance system |
| **Voyage** | The timed journey binding the interior to departure/arrival docks | `WorldClock` + `EventSchedulerGrain` + a new Orleans reminder |

Passengers live in the **interior map**. When the ship "flies", only the lightweight **exterior** footprint moves (removed from planet A, re-placed on planet B); passengers never move tile-by-tile because their map simply travels with them. This sidesteps the hardest problem (rigidly carrying dozens of entities on a moving multi-tile object) while still delivering the full player experience.

## Goals / Non-Goals
- **Goals:** footprint occupancy on a surface map; land / take off (reusing flight rules); board/disembark a party into a shared interior; a 10-30 real-minute timed voyage between worlds with scheduled in-transit events; interior keeps ticking en route; everything is per-world authored data.
- **Non-Goals:** real-time free-flight piloting; rigid "open-deck" carry of passengers on a moving exterior; ship-to-ship combat / destructible hulls; guaranteed voyage persistence across a full cluster restart (best-effort via reminders).

## Decisions
- **Three-piece decomposition** (exterior / interior / voyage) instead of rigid multi-tile physics.
- **`VehicleGrain` is an instance-style grain** modeled on `DungeonInstanceGrain`: its map is the ship interior, its lifecycle is the voyage. It reuses the instance flow (`AddMapAsync` -> store map id -> `MovePlayerToMapAsync` -> tick -> teardown).
- **Interior is a map within a world** created via `IWorldGrain.AddMapAsync`, ticked independently like an instance so gameplay proceeds whether parked or in transit.
- **Voyage self-drives via an Orleans reminder** (`VehicleGrain : IRemindable`, new but standard Orleans) that ticks its own interior each wake, rather than taking a dependency on fixing the global tick driver.

## The three unfinished seams (this feature forces closing them)
1. **Session -> world/map perception re-point.** Perception streams from the *session's own* ECS `World` (`GameSession.cs`). The cross-world branch of `GameHub.UsePortal` has the literal `// TODO: Load new world/map into session` (`GameHub.cs:483`), and `GameHub.JoinWorld` returns "not yet supported" (`GameHub.cs:665`). Boarding **is** "switch this connection's perception to the interior map's world." The intended hook exists (`GameSessionManager.CreateSession(connectionId, worldId, World, startLocation)`) but the grain -> session `World` hydration is unbuilt. **Critical-path prerequisite** (Phase 0), and independently valuable (unblocks portals and instances too).
2. **Multi-tile footprint occupancy.** Position is a single point: `WorldLocation` holds one `(X,Y,Z)` and `World.EntitiesByLocation` is keyed by a single tile. Add a `Footprint`; index every occupied tile; make move/place/collision/landing footprint-aware. Mirror the terrain pattern `WorldChunk(location, Size3d).AllLocations`. Guard behind `Has<Footprint>()` so single-tile entities keep the fast path.
3. **Tick driver.** `WorldTickService`'s loop only sleeps (`WorldTickService.cs:34`, `// TODO: Add a world registry`). Instead of fixing global ticking as a prerequisite, `VehicleGrain` ticks its own interior map on each reminder wake, the same way `DungeonInstanceGrain.TickAsync` already ticks one map.

## Risks / Trade-offs
- **Reminder durability** -> reminders survive grain deactivation but not a full cluster restart mid-voyage; re-arm from persisted `etaGameTime` on activation. Acceptable for MVP.
- **Perception re-point is load-bearing and currently stubbed** -> keep Phase 0 a standalone deliverable with its own tests; if it slips, nothing else ships.
- **Footprint touches hot paths** (`AddEntity`/`RemoveEntity`/`TryMove`, `GameMapGrain.SpawnEntityAsync`) -> guard with `Has<Footprint>()`; remember `Entity.Get<T>()` throws on a missing component, so always `Has<T>()` first.
- **Two sources of truth for "current world"** (`GameSession.WorldId` and `WorldGrain.PlayerLocations`) -> boarding must update both; add an invariant check/test.

## Migration Plan
Additive only. Footprint behavior is gated by `Has<Footprint>()`, so existing single-tile entities are unchanged and no data migration is required. Phase strictly 0 -> 4 so value lands early: Phase 0 (re-point) unblocks portals/instances independently; Phase 1 gives a parked ship; Phase 2 adds board/disembark; Phase 3 adds travel; Phase 4 adds encounters.

## Open Questions
- Reminder interval (15-30 s) vs. voyage-progress HUD smoothness.
- Whether each vehicle definition authors its own interior or draws from a shared prefab library via `PrefabStamper`.
