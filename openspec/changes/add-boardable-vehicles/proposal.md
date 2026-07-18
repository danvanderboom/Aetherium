## Why
Large boardable vehicles (spaceships, airships, dropships) are a headline feature: a party lands a "flying building", boards a shared interior, and travels between planets over 10-30 real minutes while playing out an adventure inside. The authoritative design (`docs/design/boardable-vehicles.md`) shows most plumbing already exists, but three engine seams are unfinished and this feature is what forces us to close them: session -> world/map perception re-point, multi-tile footprint occupancy, and a tick driver for the interior.

## What Changes
- Add **multi-tile footprint occupancy** to `engine-core`: a `Footprint` component (box `Size3d` or explicit relative cells), footprint-aware world indexing, movement, placement, and collision. Single-tile entities keep their fast path (guarded by `Has<Footprint>()`).
- Introduce a **NEW `vehicles` capability**: vehicle definitions (authored data), interior maps created via `IWorldGrain.AddMapAsync` that tick independently, boarding/disembarking a party, and reminder-driven timed voyages between worlds with scheduled in-transit events.
- Close the **session -> world/map perception re-point** seam so the server can switch a connection's perception to another world/map, finishing `GameHub.JoinWorld` ("not yet supported") and the `GameHub.UsePortal` cross-world `// TODO: Load new world/map into session`.
- Own the voyage lifecycle in a `VehicleGrain` (an instance-style grain) that self-drives via an Orleans reminder and ticks its own interior map, avoiding a dependency on fixing the global tick driver.

## Impact
- Affected specs: `engine-core` (ADDED: Multi-Tile Footprint Occupancy), `vehicles` (NEW capability)
- Affected code:
  - `Aetherium.Server/Core/World.cs`, `Aetherium.Server/Components/Footprint.cs` (new), `WorldChunk.cs`/`Size3d.cs` (reuse)
  - `Aetherium.Server/MultiWorld/WorldGrain.cs`, `GameMapGrain.cs` (`AddMapAsync`, `MovePlayerToMapAsync`, `SpawnEntityAsync`)
  - `Aetherium.Server/GameHub.cs` (`JoinWorld`, `UsePortal`), `GameSession.cs`, `GameSessionManager.cs` (perception re-point)
  - `Aetherium.Server/Vehicles/*` (new `VehicleGrain`/`IVehicleGrain`), `Aetherium.Server/WorldBuilders/SpaceHackWorldBuilder.cs` (authored interior)
  - `Aetherium.Server/Simulation/WorldClock.cs`, `Aetherium.Server/Events/EventSchedulerGrain.cs`, `IEventInstanceGrain.cs` (voyage timing + in-transit events)
- No breaking changes: footprint behavior is additive and guarded by `Has<Footprint>()`; single-tile entities are unaffected.
