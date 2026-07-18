# Design: Large Boardable Vehicles (Spaceships as Flying Buildings)

**Status:** Draft design · **OpenSpec change:** `add-boardable-vehicles` · **Depends on:** [`flying-entities`](flying-entities.md)

## Summary

A *boardable vehicle* is a large flying thing — a spaceship, airship, or dropship — that behaves like
a **flying building**: it occupies many tiles on the surface, can be boarded by a group of characters,
takes off, and travels to another planet over 10–30 minutes of real time while the party has an
adventure inside it.

The core insight is that we do **not** need to simulate a giant object flying tile-by-tile with people
standing on it. Instead we decompose a vehicle into three linked pieces, each of which maps onto a
primitive the engine already has (or nearly has):

| Piece | What it is | Existing primitive it reuses |
|-------|-----------|------------------------------|
| **Exterior** | A multi-tile footprint entity sitting on a planet's *surface map* | `Entity` + `WorldLocation` (needs a new footprint component) |
| **Interior** | A separate walkable map you board into | `GameMapGrain` map-within-a-world + the instance system |
| **Voyage** | The timed journey binding an interior to a departure/arrival dock | `WorldClock` + `EventSchedulerGrain` + a new Orleans reminder |

Passengers live in the **interior map**. When the ship "flies," only the lightweight **exterior**
footprint entity moves (or is removed from planet A and re-placed on planet B). The passengers never
move tile-by-tile — their map simply travels with them. This sidesteps the hardest part (carrying
dozens of entities rigidly attached to a moving multi-tile object) while delivering the full player
experience.

## Goals / Non-Goals

**Goals**
- A vehicle can occupy a rectangular (or masked) footprint of many tiles on a surface map.
- A vehicle can **land** on valid terrain near a party and **take off** again (reuses flight rules from `flying-entities`).
- Multiple characters can **board** a landed vehicle and end up together inside a shared interior map.
- A vehicle can **travel between planets** (worlds) on a **10–30 minute** timed voyage, with scheduled
  in-transit events, and **disembark** the party onto the destination surface.
- The interior keeps ticking during transit so gameplay (combat, exploration, dialogue) happens en route.
- Everything is **per-world data**, not hardcoded — vehicle definitions, interiors, routes, and timings
  are authored/configured, consistent with the engine's data-vs-behavior split.

**Non-Goals (this change)**
- Real-time free-flight piloting of the ship by a player (voyages are scheduled routes, not a flight sim).
- Rigid multi-tile physics / passengers standing on an exterior deck that itself moves across the surface
  (a later "open-deck" extension — see [Future extensions](#future-extensions)).
- Ship-to-ship combat or destructible hull sections.
- Persisting a voyage across a full server restart (best-effort via Orleans reminders; see risks).

## How it maps onto the current architecture

The exploration below references real code. The short version: **most of the plumbing exists, but three
seams are unfinished and this feature is what forces us to finish them.**

### What we can reuse

- **Worlds contain many maps.** `IWorldGrain.AddMapAsync` mints a map id `"{worldId}:map:{Guid}"` and
  spins up a `GameMapGrain` (`Aetherium.Server/MultiWorld/WorldGrain.cs:154`). A ship interior is just
  another map inside a world. A destination *planet* is a separate `WorldGrain`.
- **The instance system is the template for an interior.** `InstanceAllocatorGrain.AllocateInstanceAsync`
  (`Aetherium.Server/Instances/InstanceAllocatorGrain.cs:121`) already: creates a fresh map via
  `AddMapAsync`, stores the map id in config, initializes a per-instance grain, moves players onto the
  map (`DungeonInstanceGrain.AddPlayersAsync` → `IWorldGrain.MovePlayerToMapAsync`,
  `Aetherium.Server/Instances/DungeonInstanceGrain.cs:124`), ticks independently
  (`DungeonInstanceGrain.TickAsync:168`), and tears down (`ShutdownAsync:192`). A **`VehicleGrain` is an
  instance grain whose map is a ship interior and whose lifecycle is a voyage.**
- **Group membership already exists.** `IPartyGrain.GetMemberIdsAsync` (see `docs/instances.md`) gives us
  the boarding manifest for a party.
- **Cross-map/cross-world movement flow exists.** `GameHub.UsePortal`
  (`Aetherium.Server/GameHub.cs:388`) resolves a target and calls `MovePlayerToMapAsync` (same world) or
  `RemovePlayerAsync`/`AddPlayerAsync` (cross world). Boarding/disembarking is the same shape.
- **Real↔game time conversion exists.** `WorldClock` (`Aetherium.Server/Simulation/WorldClock.cs`) with
  `RealTimeToGameTime`/`GameTimeToRealTime`; `SimulationOptions` default `DayLengthMinutes = 24`. A
  10–30 real-minute voyage is a well-defined game-time duration.
- **A scheduled-event system exists.** `EventSchedulerGrain.ScheduleEventAsync(...scheduledGameTime...)`
  (`Aetherium.Server/Events/EventSchedulerGrain.cs:67`) plus `IEventInstanceGrain` with
  `GetPlayersInAreaAsync` / `BroadcastToAreaAsync` — ideal for mid-voyage events ("asteroid field",
  "boarding party", "engine fault") broadcast to everyone aboard.
- **An authored-interior home already exists, empty.** `SpaceHackWorldBuilder.Build()` currently returns
  `new World()` (`Aetherium.Server/WorldBuilders/SpaceHackWorldBuilder.cs:18`). This is the natural place
  to author a ship-interior layout (or use a prefab via `PrefabStamper`).

### The three unfinished seams this feature must close

1. **Session → world/map perception re-point is not implemented.** Perception is streamed from the
   *session's own* ECS `World` (`Aetherium.Server/GameSession.cs`), and `GameHub.UsePortal` cross-world
   branch has the literal `// TODO: Load new world/map into session` (`Aetherium.Server/GameHub.cs:483`);
   `GameHub.JoinWorld` returns *"not yet supported"* (`Aetherium.Server/GameHub.cs:665`). Boarding a ship
   **is** "switch this connection's perception to the interior map's world." The intended hook exists —
   `GameSessionManager.CreateSession(connectionId, worldId, World, startLocation)` — but the grain→session
   `World` hydration is not built. **This is the critical-path prerequisite.**

2. **No multi-tile entity occupancy.** Position is a single point: `WorldLocation` holds one `(X,Y,Z)`
   (`Aetherium.Server/Components/WorldLocation.cs`), and `World.EntitiesByLocation` is keyed by a single
   tile (`Aetherium.Server/Core/World.cs:20`). We must add a **footprint** (see below). Note a reusable
   pattern already exists for *terrain*: `WorldChunk(location, Size3d)` with an `AllLocations` enumerator
   (`Aetherium.Server/Core/WorldChunk.cs`) is used to stamp terrain over rectangles — the footprint
   occupancy index can mirror it.

3. **Nothing auto-drives world ticks.** `WorldTickService` is a `BackgroundService` whose loop only
   sleeps (`Aetherium.Server/Simulation/WorldTickService.cs:34`, `// TODO: Add a world registry`). A
   voyage that must advance for 10–30 minutes needs a real driver. Rather than fix global ticking as a
   prerequisite, the **`VehicleGrain` self-drives via an Orleans reminder** (there are no `IRemindable`
   grains today — this is new but standard Orleans) and ticks its own interior map on each wake, the same
   way `DungeonInstanceGrain.TickAsync` already ticks one map.

## New primitives

### 1. Footprint / multi-tile occupancy

Add a `Footprint` component describing the tiles an entity occupies relative to its anchor `WorldLocation`:

```csharp
// Aetherium.Server/Components/Footprint.cs (new)
public class Footprint : Component
{
    // Simple case: a rectangular box anchored at the entity's WorldLocation.
    public Size3d Size { get; set; } = new(1, 1, 1);
    // Advanced: explicit relative offsets for non-rectangular hulls (overrides Size when non-empty).
    public IReadOnlyList<WorldOffset> Cells { get; set; } = Array.Empty<WorldOffset>();
    public IEnumerable<WorldLocation> OccupiedTiles(WorldLocation anchor) { /* box or Cells */ }
}
```

`World` gains footprint-aware registration and movement:
- `AddEntity` / `RemoveEntity` index **every** occupied tile in `EntitiesByLocation` (or a parallel
  `FootprintIndex`), not just the anchor.
- `TryMove` / `TryPlace` for a footprint entity validate **all** destination tiles for
  passability/collision (reusing `PassableTerrain` per tile, plus flight rules from `flying-entities` so a
  ship ignores impassable ground while airborne). Collision becomes "no two footprints overlap."
- Landing validity = **every** tile under the footprint is valid landing terrain (see `flying-entities`
  landing constraints), and the whole footprint fits in-bounds and unoccupied.

This is deliberately minimal (box footprint first; masked `Cells` later) per the repo's "simplicity first"
guidance.

### 2. Vehicle definition (data)

Per-world/authored data, not hardcoded:

```jsonc
// Data/Vehicles/dropship-kestrel.json  (illustrative)
{
  "vehicleId": "dropship-kestrel",
  "displayName": "Kestrel Dropship",
  "footprint": { "width": 5, "length": 7, "depth": 1 },
  "exteriorTileType": "hull",
  "interior": { "builder": "SpaceHackWorldBuilder", "spawnDock": { "x": 2, "y": 6, "z": 0 } },
  "landing": { "requiredTerrain": ["Plains", "Road", "Landingpad"], "clearanceTiles": 0 },
  "capacity": 12,
  "boardHotspot": { "x": 2, "y": 6 }        // the exterior tile characters interact with to board
}
```

### 3. `VehicleGrain` (an instance-style grain)

Binds the three pieces and owns the voyage lifecycle:

```csharp
// Aetherium.Server/Vehicles/IVehicleGrain.cs (new), IGrainWithStringKey (vehicleInstanceId)
Task InitializeAsync(VehicleConfig cfg);                 // creates interior map via AddMapAsync
Task<LandingResult> LandAsync(WorldId world, WorldLocation anchor);   // footprint placement on surface
Task<bool> BoardAsync(IReadOnlyList<PlayerId> players);  // MovePlayerToMapAsync into interior + re-point sessions
Task<bool> DisembarkAsync(IReadOnlyList<PlayerId> players, WorldLocation surfaceTile);
Task<VoyageHandle> DepartAsync(RouteId route);           // takeoff + start timed voyage (reminder)
Task<VehicleInfo> GetInfoAsync();
```

State: `interiorMapId`, `manifest (PlayerId set)`, `dock { worldId, anchor } | inTransit`, `voyage
{ route, departedGameTime, etaGameTime, scheduledEvents }`.

### 4. Voyage driver (Orleans reminder)

- `DepartAsync` computes ETA from the route's real-time duration via `WorldClock`, removes the exterior
  footprint from planet A's surface map, marks the vehicle `InTransit`, and registers an Orleans reminder
  (e.g. every 15–30 s) plus schedules mid-voyage events through `EventSchedulerGrain` at game-time offsets.
- Each reminder wake: tick the interior map (`GameMapGrain.TickAsync`), fire any due events (broadcast to
  passengers via `IEventInstanceGrain.BroadcastToAreaAsync`), and push a lightweight "voyage progress"
  HUD update.
- On ETA: place the exterior footprint on planet B's surface map at the arrival dock, unregister the
  reminder, transition to `Landed`, and allow disembarking.

## End-to-end flows

### Boarding a landed ship
1. Party stands near the ship; a character interacts with the ship's `boardHotspot` tile (a `use`/`board`
   action on the exterior entity).
2. `VehicleGrain.BoardAsync(partyMembers)` → for each player `MovePlayerToMapAsync(playerId, interiorMapId)`
   **and re-point the SignalR session** to the interior world/map (the seam we implement), spawning them at
   `interior.spawnDock`.
3. Server pushes a fresh perception frame; players now see the interior. The exterior remains on the
   surface for any players who did not board.

### Takeoff and voyage
1. A pilot/console interaction inside the interior calls `DepartAsync(route)`.
2. Exterior footprint is removed from planet A (takeoff), voyage reminder starts, HUD shows "In transit →
   Planet B (ETA 18:42)".
3. Mid-voyage events fire (encounter grains broadcast to everyone aboard); the interior map ticks so
   combat/exploration proceed normally.
4. At ETA, exterior footprint is placed at planet B's arrival dock; state → `Landed`.

### Disembarking
1. Interaction at an interior airlock calls `DisembarkAsync(players, surfaceTile)`.
2. Each player `MovePlayerToMapAsync` onto planet B's surface map, session re-pointed, spawned adjacent to
   the ship footprint; meta-progression records the new-world discovery (as `UsePortal` already does,
   `Aetherium.Server/GameHub.cs:486`).

## Phasing

Because seam #1 (session perception re-point) is a real prerequisite, phase the work so value lands early:

- **Phase 0 — Session re-point (foundation).** Implement grain→session world/map hydration and finish
  `JoinWorld`/the `UsePortal` cross-world TODO. Independently valuable (unblocks portals & instances too).
- **Phase 1 — Footprint occupancy.** `Footprint` component + multi-tile indexing + footprint-aware
  `TryMove`/placement/landing. Ship can land/park on a surface as a big static object.
- **Phase 2 — Interior + boarding.** `VehicleGrain` creates an interior (authored in `SpaceHackWorldBuilder`),
  `BoardAsync`/`DisembarkAsync` move a party in/out. No travel yet (board a parked ship, walk around, leave).
- **Phase 3 — Voyage.** Reminder-driven timed journey between two worlds with `DepartAsync`, ETA arrival,
  and disembark on the destination.
- **Phase 4 — In-transit events.** Wire `EventSchedulerGrain` encounters into the voyage timeline.

## Risks & trade-offs

- **Reminder durability.** Orleans reminders survive grain deactivation but a full cluster restart
  mid-voyage needs recovery logic (re-arm from persisted `etaGameTime` on activation). Acceptable for MVP;
  document the edge case.
- **Perception re-point is load-bearing and currently stubbed.** If Phase 0 slips, nothing else ships.
  Keep it a standalone deliverable with its own tests.
- **Footprint touches hot paths** (`AddEntity`/`RemoveEntity`/`TryMove`, `GameMapGrain.SpawnEntityAsync`
  `Aetherium.Server/MultiWorld/GameMapGrain.cs:378`). Guard behind `Has<Footprint>()` so single-tile
  entities keep their current fast path (see the `Entity.Get<T>()` throws-on-missing gotcha — always
  `Has<T>()` first).
- **Two sources of truth for "current world"** (`GameSession.WorldId` and `WorldGrain.PlayerLocations`).
  Boarding must update both; add an invariant check/test.

## Future extensions
- **Open-deck vehicles** where passengers stand on an exterior deck that itself moves across the surface
  (requires the rigid multi-tile carry mechanic we deliberately deferred).
- **Player-piloted free flight** (real-time steering instead of scheduled routes).
- **Persistent fleets** and vehicle ownership/economy tie-in via the cluster economy
  (`docs/multiworld-ecosystems.md`).

## Key source references
- `Aetherium.Server/Core/World.cs` — `EntitiesByLocation`, `TryMove`, `AddEntity`/`RemoveEntity`
- `Aetherium.Server/Components/WorldLocation.cs`, `Aetherium.Server/Core/WorldChunk.cs`, `Size3d.cs`
- `Aetherium.Server/MultiWorld/WorldGrain.cs` (`AddMapAsync`, `MovePlayerToMapAsync`), `GameMapGrain.cs`
- `Aetherium.Server/Instances/InstanceAllocatorGrain.cs`, `DungeonInstanceGrain.cs` (interior template)
- `Aetherium.Server/GameHub.cs` (`UsePortal`, `JoinWorld` seam), `GameSession.cs`, `GameSessionManager.cs`
- `Aetherium.Server/Simulation/WorldClock.cs`, `WorldTickService.cs` (tick-driver gap)
- `Aetherium.Server/Events/EventSchedulerGrain.cs`, `IEventInstanceGrain.cs` (in-transit events)
- `Aetherium.Server/WorldBuilders/SpaceHackWorldBuilder.cs` (authored interior home)
