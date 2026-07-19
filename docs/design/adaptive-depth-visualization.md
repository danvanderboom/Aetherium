# Design: Adaptive-Depth Visualization

**Status:** ✅ Built & merged (Sections 1–5.4: server 3D slab + auto-slab + flight-envelope DTO + context tint; console depth composite, cross-section, altitude gauge, mode escalation; Unity band stack + follow camera + tile theme + cross-section overlay + adaptive framing + HUD altitude gauge). *Live-FPS profiling (5.5) is the only outstanding item.* · **OpenSpec change:** `add-adaptive-depth-visualization` · **Related:** [`transit-networks`](transit-networks.md), [`flying-entities`](flying-entities.md)

## Summary

Once the world has meaningful vertical structure — subway tubes at bands −1…−4, streets at 0, monorail
viaducts and skyways at +1…+6 (see [`flying-entities`](flying-entities.md) altitude bands and
[`transit-networks`](transit-networks.md)) — a single flat Z-slice can no longer communicate what's
happening. This feature makes the camera and renderer **adapt to depth**: the player's focus band is
drawn at full detail, nearby bands are progressively dimmed/ghosted so you can *see down* into a tunnel
beneath an overpass or *up* at the viaduct crossing above, and a cross-section/elevation view lets you
read a ten-level interchange at a glance. Framing adapts to how much vertical structure is around you.

## The hard constraint (drives everything)

**Perception is single-Z today.** The server computes vision, FOV, and lighting for **only the player's
current Z** and sends exactly that one slice:
- `VisionSystem.ComputeVision` builds a 2D `bool[,]` with every location at `origin.Z`
  (`Aetherium.Console/Perception/VisionSystem.cs:78`); `FovCalculator` is entirely 2D.
- `LightingSystem.FindLightSources` skips any source where `location.Z != zLevel`
  (`Aetherium.Console/Lighting/LightingSystem.cs:66`); `PerceptionService` computes at `playerLocation.Z`
  (`Aetherium.Server/PerceptionService.cs:164`).
- The renderers hardcode the current level: console `relativeZ = 0` (`ClientConsoleMapView.cs:101,470`);
  Unity filters `visual.Location.Z == zLevel` (`TilemapRenderer2D.cs:48`).

**But the DTO is already Z-capable.** Visuals are keyed `"x,y,z"`, `WorldLocationDto` carries `Z`, and
`PerceptionService` already computes `relativeZ = location.Z - playerLocation.Z`
(`Aetherium.Server/PerceptionService.cs:271`) — it just never contains anything but `relativeZ == 0`
because vision only ever includes `origin.Z`.

**Implication:** stacked/cross-section rendering is **impossible client-side alone**. The first,
load-bearing piece of this feature is a server change to emit a **Z-slab** (a range of levels), not a
single slice. The DTO schema does **not** need to change — only the production of it.

## Perception is 3D and *occluded* — not just dimmed layers

This is a **gameplay** change, not only a rendering one: what a character perceives across bands must be
computed with real vertical occlusion, because it decides what they can actually *see and act on* (the bird,
the sniper on the overpass, the platform below a grate). The engine already has the pieces to do this right
in 2D — extend them to 3D:
- **Sight blocking is `ObstructsView.Opacity`** (0 = transparent … 1 = opaque), already consumed by
  `FovCalculator` and already independent of movement blocking (`ObstructsView.cs`, `ObstructsMovement.cs`).
- A **vertical line-of-sight** from the viewer to a cell in another band is clear iff **no band between them**
  has opaque `ObstructsView` at that column (combined with the existing horizontal FOV). Terrain `BlocksLight`
  additionally governs whether the far cell is *lit* enough to see.

Worked answers to the motivating questions:
- **Bird overhead while standing under a stone bridge** → the bridge tile at the band between you and the
  bird has opaque `ObstructsView` → **you do not see the bird** (you see the bridge underside).
- **Bird overhead through a glass skylight** → the skylight has `ObstructsMovement` but `Opacity = 0` → the
  vertical ray is clear → **you see the bird**.
- **Concourse below through an open stairwell / grate** → open column (no opaque view-blocker) → visible;
  through solid pavement → not.

So the "Z-slab" the server emits is really **the set of cells in nearby bands that pass a 3D FOV test**, each
tagged with its `relativeZ` and opacity. The renderer then draws exactly what perception says is visible,
applying depth falloff for legibility (below). Off-focus bands that are *occluded* aren't merely dimmed —
they're **absent**, which is both cheaper and correct.

## Model: focus band + depth falloff

Define the visible **slab** as `[focusZ − depthBelow, focusZ + depthAbove]`. Rendering rules:

1. **Focus band (`ΔZ = 0`)** — full detail, full FOV, full lighting (exactly today's output).
2. **Off-focus bands (`ΔZ ≠ 0`)** — attenuated by `|ΔZ|`:
   - **Dim/desaturate** using the *existing* per-tile scalar dimming ramp. The console already maps a
     0–1 scalar to progressively darker colors in `DimColor` (`ClientConsoleMapView.cs:394`) and a heat
     ramp in `GetInfraredColor` (`:342`). Depth reuses this ramp keyed on `|ΔZ|`, multiplied with light
     level. No new color machinery.
   - **Occlusion / see-through** — a band is only revealed where the bands optically above it (toward the
     camera) are *open* at that column. Looking down from a street tile onto an open stairwell reveals the
     concourse below; looking down onto solid pavement shows pavement. This is a per-column "topmost
     opaque within slab wins, then show N translucent layers beneath" composite.
   - **Ghosting** — distant bands render as sparse silhouettes (structure only, no entities/light) to
     stay legible and cheap.
3. **Beyond the slab** — culled.

"Flying above terrain" and "subway below the street" become the *same* rendering rule as a bird's-eye
overpass: whichever band you focus, the others fall off by depth.

## Console client

- **Composite instead of single-slice.** Replace the hardcoded `relativeZ = 0` loop
  (`ClientConsoleMapView.cs:92-184`) with a per-screen-cell walk over the slab from `focusZ` outward:
  pick the first opaque glyph, then blend up to `depthBelow` translucent glyphs beneath it, each dimmed by
  `|ΔZ|` via the existing ramp. Player stays screen-centered (unchanged viewport math, `:77-78`).
- **Level ribbon** — a compact vertical gauge in the HUD showing the band stack and where `focusZ` sits
  (e.g. `▲ +3 viaduct / +0 street ◀ / −1 concourse / −2 platform ▼`), so depth is legible without the
  map alone carrying it.
- **Cross-section (elevation) view** — an optional side-on schematic (toggle key) that draws the column
  around the player as stacked bands: invaluable for reading a multi-level interchange. It's schematic
  (one row per band), so it's cheap and doesn't need per-tile FOV.
- **Heading caveat** — the live networked view does not rotate by heading today (only the legacy
  `ConsoleMapView.cs:105-159` does). Depth work is orthogonal to that and shouldn't try to fix rotation.

## Unity client

Unity is greenfield here — there is **no camera and no committed scene**, one `Tilemap` on a single plane
(`GridToWorld` drops Z to 0, `GridHelpers.cs:15-26`), no sorting/opacity use, and tile coloring is still a
TODO (`TilemapRenderer2D.cs:95-135`). So we build rather than retrofit:

- **One `Tilemap` per band in the slab**, stacked by `sortingOrder`, each with a `TilemapRenderer` color
  alpha set from `|ΔZ|` (opacity falloff). Focus band opaque; deeper bands more transparent. This is the
  Unity-native equivalent of the console composite.
- **A real orthographic camera** with follow + **adaptive framing**: it tracks the player and adjusts
  `orthographicSize` to the local vertical extent — zoomed in on a flat street, pulled back (or switched
  to the cross-section overlay) inside a tall interchange.
- **Tile theming** — wire the currently-stubbed color/sprite mapping so bands and terrains are
  distinguishable (a prerequisite that also just improves the base client).
- **Note:** the Unity *live* path is a stub (`PerceptionSignalRClient.cs:91-99` doesn't map DTO→
  `PerceptionLite`); build/verify against the JSON mock provider first, feeding multi-Z sample frames.

## Adaptive behavior

"Adaptive to depth" concretely means:
- **Auto-follow band** — `focusZ` follows the player's Z as they take stairs/lifts/vehicles.
- **Auto-slab** — expand `depthBelow/depthAbove` when the local column has many occupied bands (an
  interchange) and collapse it in flat terrain, trading cost for clarity only where needed.
- **Mode escalation** — past a vertical-complexity threshold, surface the cross-section view (console) or
  pull the camera back / tilt toward an isometric framing (Unity).
- **Context tint** — reuse vision modes (`Torch/Sunlight/Ambient`, `Normal/Infrared`,
  `Aetherium.Model/SharedEnums.cs:32-43`) so, e.g., an underground band defaults to torch/enclosed cueing
  while a skyway uses sunlight.

### Altitude gauge

When the player is flying/piloting (or changing Z on foot), show an **altitude gauge**: a vertical ladder of
`N` discrete steps spanning the flyer's `[MinBand, MaxBand]` (see [`flying-entities`](flying-entities.md)),
with the current band highlighted and, optionally, occupied/obstructed bands marked. It makes controlled
climb-out, level-off, and descent-to-land legible ("two steps to the landing pad"), and pairs with the
level ribbon (which shows the *world* stack around you) — the gauge shows *your* altitude within the flyer's
envelope. Console renders it as a compact glyph ladder; Unity as a HUD meter.

## Performance

Full FOV + lighting per band is N× the base cost, so:
- **Full fidelity only for the focus band.** Off-focus bands get **terrain occupancy + occlusion silhouette**
  (cheap) and, at most, coarse lighting; entities optional and count-capped.
- **Cap slab depth** (configurable, per-world), with the cross-section view — which is schematic, not
  per-tile — as the tool for arbitrarily deep stacks.
- The server can compute the slab once and cache per (focusZ, viewport); only the focus band recomputes on
  light/vision changes.

## Phasing

- **Phase 1 — 3D occluded perception slab (server).** Extend `FovCalculator`/`VisionSystem`/`LightingSystem`/
  `PerceptionService` from 2D-at-one-Z to a **vertical FOV** over a configurable Z-range, using
  `ObstructsView.Opacity` per band for occlusion (bird-under-bridge vs. skylight). Emit only cells that pass
  the 3D FOV test, each tagged with `relativeZ`. DTO schema unchanged. *This unblocks everything else —
  gameplay visibility across bands, not just rendering.*
- **Phase 2 — Console depth composite + level ribbon.** Reuse `DimColor` keyed on `|ΔZ|`; multi-band
  per-cell composite; HUD ribbon.
- **Phase 3 — Unity stack + camera + theming.** Per-band tilemaps with opacity + sorting; orthographic
  follow camera; tile color/sprite mapping.
- **Phase 4 — Cross-section / elevation view** (both clients) for interchanges.
- **Phase 5 — Adaptive framing/slab + mode escalation.**

## Risks & trade-offs

- **Server cost.** Multi-band vision/lighting is the main risk; the focus-full / off-focus-silhouette split
  and a slab cap keep it bounded. Measure against the 60+ FPS / low-flicker constraints in `project.md`.
- **Legibility vs. information.** Too many translucent layers becomes mush; default to a shallow slab and
  lean on the cross-section view for depth, rather than stacking ten translucent bands in the plan view.
- **Two renderers diverge.** Console composite and Unity stacking are different implementations of one
  model; keep the *model* (slab, falloff curve, occlusion rule) defined server-side/shared so they agree.
- **Unity live path is unfinished** — don't block visualization on it; mock-frame-driven development first.

## Key source references
- Single-Z perception (the constraint): `Aetherium.Server/PerceptionService.cs:104-108,164-181,264-286`;
  `Aetherium.Console/Perception/VisionSystem.cs:78`; `FovCalculator.cs`; `Aetherium.Console/Lighting/LightingSystem.cs:57-80`
- Z-capable DTO: `Aetherium.Model/PerceptionDto.cs`, `VisualDto.cs`, `WorldLocationDto.cs`; Unity subset `VisualLite.cs`
- Console renderer + dimming hook: `Aetherium.Console/Views/ClientConsoleMapView.cs:92-184,294-417`
- Console Z-change round-trip + modes: `Aetherium.Console/Core/ClientConsoleDungeonGameNew.cs:265-329`
- Unity renderer / Z-cycle / grid: `Aetherium.Unity/Assets/Scripts/Rendering/TilemapRenderer2D.cs:37-93`,
  `GameManager.cs:99-137`, `Spatial/GridHelpers.cs:15-26`
- Unity has no camera/scene: `Aetherium.Unity/Assets/Scenes/README.md`
- Wide-feature generation (not render-aware): `Aetherium.Console/WorldBuilders/Features/RiverFeatureBuilder.cs:37-64`
