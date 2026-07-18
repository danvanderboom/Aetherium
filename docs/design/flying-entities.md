# Design: Flying Entities & Altitude Bands

**Status:** Draft design · **OpenSpec change:** `add-flying-entities` · **Foundation for:** [`boardable-vehicles`](boardable-vehicles.md), [`transit-networks`](transit-networks.md)

## Summary

Add things that fly — birds, drones, aircraft, dropships, orbiting satellites — that move on their own
patterns, ignore ground obstruction (they're *above* it), can be interacted with (hacked, boarded, shot at,
ridden), and — for those configured to — can **land** on valid terrain and **take off** again.

Two engine concepts make all of this fall out cleanly:

1. **Altitude bands** — obstruction/passability becomes *per-Z-band* (a "z-order for obstruction"). "Flying
   ignores impassable terrain" is just the special case "a ground-band obstacle doesn't obstruct an entity
   in an air band."
2. **Flight plans** — every autonomous flyer follows a `FlightPlan`, a time-ordered path. How the plan is
   *sourced* varies (patterned orbit, ad-hoc trip, scheduled route, or none/manual) but the *follower* is
   one system.

## Goals / Non-Goals

**Goals**
- A `Flight` capability lets an entity occupy air bands and traverse tiles that are impassable at ground level.
- Obstruction resolves **per altitude band**, so tunnels, streets, overpasses, and skyways coexist.
- Flyers move autonomously via **flight plans**; movement *patterns* (orbit/patrol/wander/hover) are plan
  generators.
- Players can **interact** with flyers appropriate to type: hack a satellite, hail/summon an air taxi,
  attack a drone, board a landed craft.
- Flight-capable entities can **land** and **take off**, gated by configurable **valid landing terrain**.
- All of it is **per-world data** (flight config, band ranges, landing rules) threaded through world/entity
  creation — never hardcoded.

**Non-Goals (this change)**
- Multi-tile / boardable vehicles (see [`boardable-vehicles`](boardable-vehicles.md)).
- Procedural transit networks, stations, timetables (see [`transit-networks`](transit-networks.md)).
- Player-piloted real-time flight controls (see [`gamepad-dual-stick`](gamepad-dual-stick.md) piloting mode).
- Rendering the vertical stack (see [`adaptive-depth-visualization`](adaptive-depth-visualization.md)).
- Aerodynamics/fuel/physics simulation.

## Current state (grounding)

- Movement is authoritative in `World.TryMove`, which blocks non-passable terrain via `World.PassableTerrain`
  and gates vertical moves on `CanAscend`/`CanDescend` **marker components placed on the terrain/location**
  (`engine-core` spec; `Aetherium.Server/Core/World.cs` ~`:293-338`). A player's move flows
  `MoveTool` → `GameSession.MoveView(RelativeDirection, distance)` → `TryMove`.
- The world is 3D `(X,Y,Z)`. Z-changes today require stairs-like markers, i.e. Z is "floors," not free air.
- There is an `ObstructsMovement` component with a numeric `Obstruction` (default 1)
  (`Aetherium.Server/Components/ObstructsMovement.cs`) — a natural place to add *how tall* an obstruction is.
- **There is no autonomous NPC movement / pathfinding / steering system today.** The "Agents" are
  LLM-driven; `SpawnControllerGrain`/`EventSchedulerGrain` handle spawns/events. So the flight-plan follower
  is greenfield — it rides the existing tick chain `WorldGrain.TickAsync → GameMapGrain.TickAsync →
  MapRegionGrain.TickAsync`.
- The move tool vocabulary already exists and is shared by agents and players: `move` (relative F/B/L/R +
  absolute N/E/S/W), `rotate`, `changelevel`, `pickup`, `use`, `open`, `close`, `drop`, `jump`.

## Altitude bands & layered passability

Model obstruction as a property of a **cell** `(x,y,z)` plus an obstruction **height**, rather than a flat
per-(x,y) terrain flag:

- Each Z is an **altitude band**. Suggested default banding (per-world configurable):
  `… −3 deep transit, −2 subway, −1 basement/undercroft, 0 surface, +1 low air, +2 rooftops, +3 skyway,
  +4 high air, +5 orbit …`
- A ground obstacle (wall, mountain, building) declares an **obstruction height** `h`: it blocks bands
  `[z, z+h)`. A mountain blocks surface + low air; a plaza floor blocks only its own band; a satellite in
  orbit is obstructed by nothing below it.
- **Passability check per band:** an entity moving into `(x,y,z)` is blocked iff some entity/terrain
  *obstructs that band* at `(x,y)`. Generalizes today's binary `PassableTerrain` into
  `IsPassable(cell, forEntity)` that consults band-aware obstruction. Ground-band terrain keeps its current
  rule (backward compatible: an entity with no `Flight` at band 0 behaves exactly as today).
- **`ObstructsMovement` gains `Height`** (how many bands up it blocks) and optionally a band range. Terrain
  passability tables map terrain → obstruction height (a wall is tall; open plains are height 0).

This is the "z-order for depth/altitude of obstruction resolution" — and it's what lets a subway tube at −2,
a street at 0, and a monorail viaduct at +3 all occupy the same `(x,y)` without colliding.

### Obstruction has three independent facets (movement, sight, light)

Crucially, blocking movement is **not** the same as blocking sight or light — and the engine *already*
models these separately; they just need band/height semantics:
- **Movement** — `ObstructsMovement.Obstruction` (`Aetherium.Server/Components/ObstructsMovement.cs`).
- **Sight** — `ObstructsView.Opacity` (0 = see-through … 1 = opaque)
  (`Aetherium.Server/Components/ObstructsView.cs`), consumed by `FovCalculator`.
- **Light** — terrain `BlocksLight` setting + lighting opacity (`SunlightCalculator.cs:191`).

So a **stone bridge** at band +1 has both `ObstructsMovement` *and* `ObstructsView` → it blocks the tile you
stand under *and* hides the bird flying above it. A **glass skylight** has `ObstructsMovement` (you can't
walk through it) but `ObstructsView.Opacity = 0` → you see the bird straight through it. Each facet carries
the same **height/band** extent so occlusion is evaluated per band. This is what makes vertical perception
correct (see [`adaptive-depth-visualization`](adaptive-depth-visualization.md) — 3D occluded perception),
and it directly answers "can I see the flyer overhead?": **only if no band between us has opaque
`ObstructsView` at that column.**

## Flight capability

A `Flight` component grants band freedom:

```csharp
// Aetherium.Server/Components/Flight.cs (new)
public class Flight : Component
{
    public int MinBand { get; set; } = 1;     // lowest air band it can occupy while airborne
    public int MaxBand { get; set; } = 5;     // ceiling (e.g. orbit for a satellite)
    public int CruiseBand { get; set; } = 2;  // preferred altitude
    public bool CanLand { get; set; } = false;
    public FlightState State { get; set; } = FlightState.Airborne; // Airborne | Landed | TakingOff | Landing
}
```

Rules in `TryMove`/flight follower (guarded by `Has<Flight>()` so single-tile grounded entities keep the fast path):
- While `Airborne`, horizontal moves **ignore ground-band obstruction** and are blocked only by obstacles
  that reach the flyer's band (a mountain peak into low air).
- Vertical moves within `[MinBand, MaxBand]` are **free** — they do **not** require `CanAscend`/`CanDescend`
  markers (those still gate *grounded* stair movement).
- A flyer never "falls"; altitude is explicit state.

## Flight plans (the movement engine)

Every autonomous flyer carries a `FlightPlan`. The follower advances it each tick at the entity's movement
cadence (shared with [`movement-cadence-and-held-input`](movement-cadence-and-held-input.md)).

```csharp
// Aetherium.Server/Flight/FlightPlan.cs (new)
public enum FlightPlanSource { Manual, AdHoc, Scheduled, Patterned }

public class FlightPlan : Component
{
    public FlightPlanSource Source { get; set; }
    public IReadOnlyList<Waypoint> Legs { get; set; }   // (worldId?, cell, band, arriveByGameTime?)
    public LoopMode Loop { get; set; }                  // Once | Loop | PingPong
    public string? PatternId { get; set; }              // for Patterned: "orbit","patrol","wander","hover"
    public int Cursor { get; set; }                     // current leg
}
```

| Source | Who sets it | Behavior |
|--------|-------------|----------|
| **Patterned** | Spawn config / generator | Procedural loop from a `PatternId`: satellite **orbit**, bird **wander**, drone **patrol**, gunship **hover**. |
| **AdHoc** | On demand | Generated when a destination is chosen — player picks from options, or an air taxi is summoned. |
| **Scheduled** | Timetable | Fixed route + arrival times; used by transit (see `transit-networks`). |
| **Manual** | (none) | No plan; a controller drives movement directly (piloting). |

The **follower** is a per-map system on the tick chain: for each entity with a `FlightPlan` (non-Manual),
step toward the current leg's cell/band by one cadence-move, honoring band passability; on reaching a leg,
advance the cursor / loop / fire arrival behavior (hover, land, unload, submit next AdHoc plan). No global
pathfinder is required for MVP — legs are waypoints; straight-line/greedy stepping suffices, with optional
A* over the band-graph later.

### Movement patterns (Patterned plans)
Pattern generators produce legs from a few parameters, kept as **data**:
- **orbit** — closed ring at a fixed band around a center (satellites, patrol drones).
- **patrol** — back-and-forth or circuit between anchor points.
- **wander** — bounded random walk within a region and band range (birds, insects).
- **hover** — hold a cell/band with small jitter (gunship, surveillance drone).

## Cruising-altitude rule & collision policy

When a game wants **lots** of flyers moving at once (a Coruscant-style sky), we avoid expensive per-pair
avoidance by borrowing the real-world **semicircular rule**: cruise band is a function of **heading**, so
opposing traffic is naturally separated by altitude.

```csharp
// Per-world data, applied by the flight-plan follower when choosing a leg's cruise band.
"cruiseRule": {
  "type": "semicircular",
  "eastbound":  { "headings": "0..179",   "bands": [2, 4] },   // e.g. even bands
  "westbound":  { "headings": "180..359", "bands": [3, 5] },   // e.g. odd bands
  "landingExempt": true                                          // takeoff/landing/hover ignore the rule
}
```

- Autonomous flyers pick their cruise band from the rule for their current heading; when they turn past a
  threshold they climb/descend to the band appropriate to the new heading. Layered lanes emerge for free.
- **Collision policy is data**, per world / per flyer class:
  - `separated` (default for dense skies) — the cruise rule keeps classes apart; same-band same-tile is
    still resolved as occupancy (no two footprints overlap).
  - `collidable` — flyers *can* collide (dogfights, hazards); collisions raise an event
    (hook for [`asset-action-scripting`](asset-action-scripting.md) reactions / damage).
- **Player / manual flight is exempt.** A piloted flyer chooses its own altitude via the
  **altitude gauge** — a discrete ladder of `N` steps across `[MinBand, MaxBand]` shown in the HUD (detailed
  in [`gamepad-dual-stick`](gamepad-dual-stick.md) piloting and [`adaptive-depth-visualization`](adaptive-depth-visualization.md)).
  This is exactly what makes controlled climb-out and descent-to-land legible.

## Landing & takeoff

**Landing is descending to the top of the next obstruction below you.** A flyer descends its column until it
meets the highest surface below its band and comes to rest **co-located with that band**, rendered on top of
whatever it rests on. That surface is the topmost of:

- the **ground floor** — a passable terrain tile at its own band (a plane lands on the desert at band 0,
  the same band as the map tile);
- a **terrain peak** — a tall terrain feature rests at its top band (a bird lands on a mountain's peak, band
  `tileBand + height − 1`, not inside its base);
- a **structure top** — a monorail deck at band 4, a skyscraper roof at band 15.

**Which surfaces are landable depends on the flying thing.** A bird lands on forest, mountain, or water; a
floatplane on water or flat land; a wheeled plane only on dry flat ground. This is per-flyer data
(`Flight.LandableTerrain`), falling back to a per-world default when unset. Structure tops are landable by any
flyer that can land at all.

```jsonc
// per-world default (fallback)
"landing": { "requiredTerrain": ["Plains","Road","Landingpad"], "forbiddenOccupied": true }
// per-flyer override
"flight": { "canLand": true, "landableTerrain": ["Forest","Mountain","Water"] }  // a bird
```

- **Land:** find the surface below; if it is terrain, require it be in the flyer's `LandableTerrain`
  (else the world default); a structure top is always landable. The resting cell must be unoccupied, and an
  open column with nothing below refuses. Transitions `Airborne → Landing → Landed`, lowering the entity to
  the surface band. A landed flyer obeys **normal** ground passability and can be approached/boarded.
- **Take off (launch):** ascending **above whatever you were resting on**. `Landed → TakingOff → Airborne`,
  rising to the first clear band above the surface — the `CruiseBand` when it lies above the surface, else
  just clear of it (so a bird resting on a monorail at band 4 with a cruise band of 2 climbs *up* to band 5,
  not down into the deck), clamped to `[MinBand, MaxBand]`. Refused if the flyer cannot ascend clear within
  its ceiling. Once airborne, ground obstruction no longer applies.
- Landing/takeoff are **tools** (`land`, `takeoff`) so agents, scripts, and players trigger them uniformly;
  autonomous flyers invoke them from flight-plan arrival behavior.

## Player interaction

Interaction is per **type**, via the existing tool/affordance system (`interaction`/`perception` specs) plus
new affordances surfaced when a flyer is in range:
- **Satellite (orbit):** a `hack` interaction — succeeds against an out-of-reach entity via a
  line-of-sight/uplink check rather than adjacency; grants intel / control (e.g. retask its orbit, read its
  feed). Good showcase of "interact with something you can't physically touch."
- **Air taxi / transport:** `summon`/`hail` → the craft generates an AdHoc plan to the caller, lands, and
  offers boarding (boarding itself is `boardable-vehicles`).
- **Bird / drone:** `attack`/`shoot` (ranged), `scare`, or `tag`/`observe`.
- **Landed craft:** `board`, `enter`, `inspect`.

Affordances respect altitude: a grounded player can shoot a low-air drone but only *hack* an orbital
satellite; perception must include flyers in nearby bands (ties to the multi-Z perception slab in
`adaptive-depth-visualization`).

## Data model (per-world, not hardcoded)

Consistent with the engine's data-vs-behavior split:
- **World config** carries the band range/labels and terrain→obstruction-height table.
- **Entity/spawn config** carries `Flight` params, the `FlightPlan` (or a pattern id + params), and landing
  rules. Flyers are spawned like any entity (`GameMapGrain.SpawnEntityAsync`) with these components attached.

## Phasing
- **Phase 1 — Altitude bands & layered passability.** Per-band `IsPassable`, `ObstructsMovement.Height`,
  terrain→height table. Grounded behavior unchanged. *Foundation.*
- **Phase 2 — Flight capability.** `Flight` component; airborne moves ignore ground obstruction; free
  vertical within band range.
- **Phase 3 — Flight-plan follower.** Tick-driven follower + Patterned generators (orbit/wander/patrol/hover).
- **Phase 4 — Land/takeoff.** `land`/`takeoff` tools + valid-terrain gating + state machine.
- **Phase 5 — Interaction.** Type-specific affordances (hack, summon, attack), altitude-aware.

## Risks & trade-offs
- **`TryMove`/passability is a hot path.** Keep the per-band check behind `Has<Flight>()` and a cheap
  band-obstruction lookup; single-tile grounded entities must not regress (mind the `Entity.Get<T>()`
  throws-on-missing gotcha — `Has<T>()` first).
- **Perception must expose other bands** for interaction/visibility, which today it does not (single-Z).
  Flying interaction depends on the multi-Z perception slab from `adaptive-depth-visualization` — sequence
  accordingly, or ship Phase 1–4 headless-testable and gate visible interaction on the perception work.
- **No existing pathfinder.** MVP uses waypoint stepping; complex avoidance is future work.
- **Band count creep.** Deep+tall stacks multiply per-band cost; make the range per-world and bounded.

## Key source references
- `Aetherium.Server/Core/World.cs` (`TryMove`, `PassableTerrain`), `engine-core` spec (Movement Constraints,
  Terrain Passability Rules)
- `Aetherium.Server/Components/ObstructsMovement.cs`, `CanAscend.cs`, `CanDescend.cs`
- `Aetherium.Server/Agents/Tools/Movement/MoveTool.cs` (relative+absolute dirs), `RotateTool.cs`, `ChangeLevelTool.cs`
- `Aetherium.Model/SharedEnums.cs` (`RelativeDirection`), `GameSession.MoveView`
- Tick chain: `WorldGrain.TickAsync` → `GameMapGrain.TickAsync` → `MapRegionGrain.TickAsync`
- Spawning: `Aetherium.Server/MultiWorld/GameMapGrain.cs` (`SpawnEntityAsync`)
- Interaction/perception: `interaction`, `perception`, `perception-vision` specs
