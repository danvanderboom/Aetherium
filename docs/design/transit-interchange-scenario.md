# Test Use Case: "Nexus Junction" — A Multi-Level Transit Interchange

**Status:** Worked scenario / cross-cutting acceptance test
**Exercises:** `add-flying-entities`, `add-boardable-vehicles`, `add-transit-networks`,
`add-adaptive-depth-visualization`, `add-held-key-repeat-movement`, `add-gamepad-dual-stick`

This is a single concrete scenario that stitches the whole design together. Each **beat** is annotated with
the change → requirement it validates, so the scenario doubles as an end-to-end acceptance test that the six
proposed changes **compose**. It is deliberately ambitious — most beats map to later phases of their change.

## The place

**Nexus Junction** is a sci-fi city interchange occupying one `(x,y)` district across **ten altitude bands**:

```
 +4  ┃ high-air lane        drones, air taxis routing over the city
 +3  ┃ MONORAIL VIADUCT     2-track maglev guideway, elevated stations
 +2  ┃ skybridge / rooftops pedestrian bridges between towers
 +1  ┃ elevated ramp        freeway on-ramp climbing from street
  0  ┃ STREET               roads + bus route, ground-level plaza
 -1  ┃ CONCOURSE            wide hall: shops, cafés, a bar, ticket kiosks
 -2  ┃ SUBWAY (Blue Line)   9-tile-wide, 2 tracks + platforms + shop strip
 -3  ┃ DEEP METRO (Red)     express tubes, crossing the Blue line below it
 -4  ┃ maintenance / service access tunnels, a smuggler hideout
```

The Blue Line (−2) and the Red express (−3) cross **without touching**; the freeway ramp climbs 0→+1→+2 and
passes **under** the monorail viaduct at +3. All of this coexists at the same `(x,y)` purely because
**obstruction resolves per band**.

## Beat-by-beat

### 1. The interchange generates
World-gen runs `TransitNetworkPass`: it samples station sites (`PoissonDiscSampling`), connects them
(`MinimumSpanningTree`), assigns each line a **band**, and carves **wide corridors** from a `CorridorProfile`
(9-tile subway trunk, 5-tile monorail). `TransitVenuePass` stamps shop/café/bar prefabs into the −1 concourse
flanks. Grade separation is automatic — the viaduct at +3 doesn't obstruct the street at 0.
→ `add-transit-networks` (Network Generation, Cross-Section Profile, Inhabited Corridors);
`add-flying-entities` (Altitude Bands & Layered Passability).

### 2. Trains are already running
Two `ServiceGrain`s dispatch on headways (`EventSchedulerGrain.ScheduleRecurringEventAsync`): a 4-car Blue
Line subway every 6 game-minutes, a 3-car monorail every 10. Each follows a **Scheduled** flight plan
station→station, dwelling to board/alight. The subway cars are **footprint** entities (long, multi-tile).
→ `add-transit-networks` (Scheduled Services); `add-flying-entities` (Flight Plans);
`add-boardable-vehicles` (Footprint / multi-tile occupancy).

### 3. The player arrives at street level and sees down
The player stands in the 0-band plaza. The camera renders the street at full detail and **ghosts the −1
concourse** through an open stairwell and the **+3 viaduct** faintly overhead — depth falloff by `|ΔZ|`
reusing the existing dimming ramp. A **level ribbon** shows the ten-band stack with the player at 0.
→ `add-adaptive-depth-visualization` (Multi-Z Perception Slab, Depth Falloff Rendering, Level Ribbon).

### 4. The player walks to the stairs (holds a direction)
Holding the left stick forward, the avatar walks **continuously** across the plaza at its configured move
rate — not one step per press — turning with the right stick as it goes. Pressing down the stairs descends
0 → −1.
→ `add-held-key-repeat-movement` (Held-Input Repeat, Server Action Cadence);
`add-gamepad-dual-stick` (Dual-Stick On-Foot: relative move + right-stick turn/climb).

### 5. The concourse (−1): an inhabited tunnel
The −1 band is a wide hall lined with a café, a bar, and ticket kiosks (prefab venues). The player walks up
to a vending stall and presses **X** — because a carriable item is on the tile, the context action runs
`pickup`; at the bar's terminal, **X** instead runs `use` (no item present). The depth camera now focuses
−1, ghosting the −2 platforms below and the street above.
→ `add-transit-networks` (Inhabited Corridors); `add-gamepad-dual-stick` (X = Get-or-Use context action);
`add-adaptive-depth-visualization` (auto-follow focus band).

### 6. Down to the platform (−2) and board the Blue Line
On the −2 platform the player **waits at the stop**; the level ribbon and an arrivals sign show the next Blue
Line in 40s. The 4-car subway pulls in and **dwells**. The player steps to the car's board hotspot and
presses **A** to board — perception re-points to the **car/train interior**, and the player is now inside a
moving service with other passengers.
→ `add-transit-networks` (Scheduled Services, station dwell/board);
`add-boardable-vehicles` (Boarding = session perception re-point, Phase 0 seam; Interior map).

### 7. Ride, cross under the express, disembark
The subway runs its Scheduled plan along the −2 corridor, **passing beneath** the Red express at −3 (both
visible as ghost layers if the player peeks via the cross-section view). At the destination station the
player disembarks (**B**/exit) back onto a −2 platform in a different district.
→ `add-flying-entities` (Flight Plans / band traversal);
`add-adaptive-depth-visualization` (Cross-Section View for the −2/−3 crossing);
`add-boardable-vehicles` (Disembark).

### 8. Summon an air taxi to the surface-and-up
Back up top, the player uses a hail kiosk (**summon**). An idle air taxi at +4 generates an **AdHoc** flight
plan, descends, and **lands** on a marked pad (valid-terrain landing). The player boards and is shown a
**destination menu** (the multi-option selection UI). They pick "Orbital Dock."
→ `add-flying-entities` (AdHoc plans, Land/Takeoff on valid terrain, Summon interaction);
`add-transit-networks` (AdHoc services + destination menu);
`add-boardable-vehicles` (boarding).

### 9. Optionally: take the controls
Instead of riding, the player takes the pilot seat. The taxi's plan clears to **Manual**; the **same
dual-stick controls** now fly the vehicle — left stick thrust/strafe, right stick yaw + **climb/descend**
through the bands, up past the +3 viaduct into the +4 lane. Leaving the seat restores avatar control.
→ `add-gamepad-dual-stick` (Piloting Context); `add-flying-entities` (Manual flight plan, band flight).

### 10. Hack the satellite overhead (bonus interaction)
While airborne the player targets a comms **satellite** orbiting at +5 and runs a `hack` interaction — no
adjacency, resolved by an uplink/line-of-sight check — to retask it and reveal the map.
→ `add-flying-entities` (Player Interaction with flyers, altitude-aware affordances).

## Coverage matrix

| Beat | flying-entities | boardable-vehicles | transit-networks | adaptive-depth | held-key | gamepad |
|---|:--:|:--:|:--:|:--:|:--:|:--:|
| 1 Generate interchange | ● (bands) | | ● | | | |
| 2 Trains running | ● (plans) | ● (footprint) | ● | | | |
| 3 See down/up | | | | ● | | |
| 4 Walk (hold) + turn | | | | ● | ● | ● |
| 5 Concourse + X | | | ● | ● | | ● |
| 6 Wait + board subway | | ● | ● | | | ● (A) |
| 7 Ride under express | ● | ● | | ● | | |
| 8 Summon air taxi | ● | ● | ● | | | |
| 9 Pilot manually | ● | | | | ● | ● |
| 10 Hack satellite | ● | | | | | |

Every proposed change is exercised, and several beats require **two or three to work together** — which is
the point: the scenario fails if the seams between them are wrong (e.g. band passability vs. footprint
collision in beat 2; boarding re-point vs. scheduled dwell in beat 6; cadence vs. dual-stick in beat 4).

## What this scenario tells us to prioritize
The load-bearing prerequisites surface immediately:
- **Altitude bands / layered passability** (`add-flying-entities` Phase 1) — nothing multi-level works without it.
- **Session→world perception re-point** (`add-boardable-vehicles` Phase 0) — boarding *anything* depends on it.
- **Multi-Z perception slab** (`add-adaptive-depth-visualization` Phase 1) — the stack is unreadable without it.

A thin vertical slice — bands + one wide subway line + one scheduled train + boarding + a shallow depth
camera — would play beats 1–6 end to end and de-risk the whole set before the fancier pieces (piloting,
satellites, inter-planet voyages) are built.
