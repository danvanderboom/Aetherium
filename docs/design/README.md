# Game Features Design: Flight, Vehicles, Transit, Depth & Controls

This folder holds the design for a connected set of gameplay features around **things that fly, things you
ride, and how you move and see in a vertical world**. They were designed together because they share
primitives — altitude bands, flight plans, footprints, action cadence — and are best built in dependency
order.

## The documents

| Doc | Feature | OpenSpec change |
|---|---|---|
| [flying-entities.md](flying-entities.md) | Flyers (birds, drones, aircraft, satellites); **altitude bands / z-order obstruction**; **flight plans**; land/takeoff; interaction | `add-flying-entities` |
| [boardable-vehicles.md](boardable-vehicles.md) | Large multi-tile vehicles (spaceships as flying buildings); **footprints**; boardable **interiors**; timed inter-planet **voyages** | `add-boardable-vehicles` |
| [transit-networks.md](transit-networks.md) | PCG **rails/roads/subways/bus routes**; stations; **scheduled/AdHoc/manual** services; wide corridors; inhabited tunnels | `add-transit-networks` |
| [adaptive-depth-visualization.md](adaptive-depth-visualization.md) | **3D occluded perception** (see up/down through bands) + depth-cued rendering + adaptive camera + altitude gauge | `add-adaptive-depth-visualization` |
| [movement-cadence-and-held-input.md](movement-cadence-and-held-input.md) | **Hold-to-repeat** movement paced to a character's **action cadence** | `add-held-key-repeat-movement` |
| [gamepad-dual-stick.md](gamepad-dual-stick.md) | **Dual-thumbstick** controls + **piloting** context | `add-gamepad-dual-stick` |
| [asset-action-scripting.md](asset-action-scripting.md) | **ECA rules → asset actions** (grow/shrink, animate, tint) via a client command channel | `add-asset-action-scripting` |
| [transit-interchange-scenario.md](transit-interchange-scenario.md) | "Nexus Junction" — a worked multi-level interchange that exercises the changes as one acceptance test | — |

## The shared primitives (why these belong together)

- **Altitude bands / layered passability** (from `flying-entities`) — obstruction resolves per Z-band, so
  flight, subways, overpasses, and interchanges all coexist. Obstruction has **three independent facets** —
  movement (`ObstructsMovement`), sight (`ObstructsView.Opacity`), light (`BlocksLight`) — which is what makes
  "see the bird overhead, but not through a bridge; yes through a skylight" correct. Used by transit
  (multi-level lines), 3D perception, and the depth camera.
- **Flight plans** (from `flying-entities`) — one follower, four sources: **Patterned** (orbit/wander),
  **AdHoc** (summon/pick destination), **Scheduled** (timetables), **Manual** (piloting). Transit services
  and vehicle voyages are just flight plans.
- **Footprints** (from `boardable-vehicles`) — multi-tile entities. A spaceship, a train car, and a bus are
  the same primitive at different sizes.
- **Action cadence** (from `movement-cadence`) — the single clock that paces held-key movement *and*
  flight-plan stepping.
- **Session→world perception re-point** (from `boardable-vehicles` Phase 0) — the currently-stubbed seam that
  boarding, portals, and instances all need.

## Recommended build order

The features form a dependency chain; a thin vertical slice de-risks the rest (see the scenario's final
section):

1. **`add-flying-entities`** — altitude bands + flight + flight plans. *Foundation; unlocks the rest.*
2. **`add-held-key-repeat-movement`** and **`add-gamepad-dual-stick`** — independent, shippable in parallel;
   cadence is shared with flight plans.
3. **`add-adaptive-depth-visualization`** — multi-Z perception slab first (server), then client rendering.
   *Needed to make vertical worlds legible.*
4. **`add-boardable-vehicles`** — footprints + interiors + boarding (Phase 0 perception re-point) + voyages.
5. **`add-transit-networks`** — PCG networks + stations + services, composing all of the above.
6. **`add-asset-action-scripting`** — ECA → asset actions; its command channel is independent (can start any
   time), but the footprint-affecting `scale` action depends on `add-boardable-vehicles` footprints.

## Grounding

Each doc's "Current state" and "Key source references" sections cite the real code the design builds on
(`World.TryMove`/`PassableTerrain`, the `MoveTool` relative-direction support, the multi-world/instance/portal
grains, the single-Z perception pipeline, the PCG pass/feature/algorithm stack, and the current
single-shot Unity input). Notable **unbuilt seams** the designs must close are called out explicitly —
chiefly the session→world perception re-point (`GameHub.cs` `JoinWorld` / `UsePortal` TODO), the absence of
multi-Z perception, and the lack of a per-entity action-cadence model.

> These are **proposals**. The matching OpenSpec changes under `openspec/changes/` carry the normative
> requirements, task checklists, and (where warranted) technical decisions. Validate with
> `openspec validate <change-id> --strict`.
