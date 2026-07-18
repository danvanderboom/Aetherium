## Why
Aetherium worlds are flat at the obstruction layer: `World.TryMove` treats passability as a single per-(x,y) terrain flag and gates all vertical movement on `CanAscend`/`CanDescend` markers, so nothing can fly *over* impassable terrain or occupy the air above a street. To support birds, drones, aircraft, dropships, and orbiting satellites — entities that move on their own patterns, ignore ground obstruction, can be interacted with, and can land and take off — the engine needs per-altitude-band obstruction plus an autonomous flight-plan follower. This change is the foundation for boardable vehicles and transit networks.

## What Changes
- Make obstruction resolve **per Z-band**: `ObstructsMovement` gains a height/band-extent so a wall blocks bands `[z, z+h)`, and passability becomes a band-aware `IsPassable(cell, forEntity)` check. Grounded single-tile entities at band 0 behave exactly as today (backward compatible).
- Model obstruction as **three independent facets** — movement (`ObstructsMovement`), sight (`ObstructsView.Opacity`), and light (`BlocksLight`) — each carrying the same band extent, so a glass skylight blocks movement but not sight while a stone bridge blocks both.
- Add a **`Flight` component** (`MinBand`/`MaxBand`/`CruiseBand`/`CanLand`/`State`): while airborne, horizontal moves ignore ground-band obstruction and vertical moves within `[MinBand, MaxBand]` are free (no ascent/descent markers).
- Add a new **`flight` capability**: a `FlightPlan` (`Manual`/`AdHoc`/`Scheduled`/`Patterned`) plus a tick-driven follower that advances legs at the entity's action cadence.
- Add **movement patterns** (`orbit`, `patrol`, `wander`, `hover`) as Patterned-plan leg generators.
- Add a per-world **cruising-altitude rule** (semicircular: cruise band derived from heading) so dense opposing traffic self-separates by altitude, plus a per-world/per-class **collision policy** (`separated` default, or `collidable` which raises a collision event).
- Add **`land`/`takeoff` tools** with an `Airborne ↔ Landing/TakingOff ↔ Landed` state machine gated by `Flight.CanLand` and per-world valid landing terrain.
- Add **altitude-aware flyer interaction** affordances (hack an orbital satellite at range, summon/hail an air taxi, attack a low drone), respecting the observer's band relative to the flyer.
- Keep all configuration (band range/labels, terrain→obstruction-height table, flight params, plans, landing rules, cruise rule, collision policy) as **per-world data**, never hardcoded.

## Impact
- Affected specs:
  - `engine-core` — **MODIFIED**: Movement Constraints; **ADDED**: Altitude Bands and Layered Obstruction, Flight Capability
  - `flight` — **NEW capability**: Flight Plans, Movement Patterns, Cruising Altitude Rule, Collision Policy, Landing and Takeoff, Flyer Interaction
- Affected code:
  - `Aetherium.Server/Core/World.cs` (`TryMove`, `PassableTerrain` → band-aware `IsPassable`)
  - `Aetherium.Server/Components/ObstructsMovement.cs` (add `Height`/band extent), `ObstructsView.cs`, `CanAscend.cs`/`CanDescend.cs`
  - New `Aetherium.Server/Components/Flight.cs` and `Aetherium.Server/Flight/*` (FlightPlan, follower, pattern generators)
  - Tick chain `WorldGrain.TickAsync → GameMapGrain.TickAsync → MapRegionGrain.TickAsync` (hosts the follower)
  - New `land`/`takeoff` tools alongside `Aetherium.Server/Agents/Tools/Movement/*`
  - Spawning path `Aetherium.Server/MultiWorld/GameMapGrain.cs` (`SpawnEntityAsync`) to attach flight components from per-world data
- Design reference: `docs/design/flying-entities.md`
- Sequencing: Phases 1–4 are headless-testable; visible flyer interaction (Phase 5) depends on the multi-Z perception slab (see `adaptive-depth-visualization`).
