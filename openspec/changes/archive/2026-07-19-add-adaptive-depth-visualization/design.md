## Context

Authoritative design: `docs/design/adaptive-depth-visualization.md`. As the world gains vertical structure (subway bands below, viaducts and skyways above), a flat Z-slice can no longer show what a character can see or act on.

The hard constraint that drives everything: **perception is single-Z today.** `VisionSystem.ComputeVision` builds a 2D `bool[,]` at `origin.Z`; `FovCalculator` is entirely 2D; `LightingSystem.FindLightSources` skips any source where `location.Z != zLevel`; `PerceptionService` computes at `playerLocation.Z`. The renderers then hardcode the current level (console `relativeZ = 0`; Unity filters `visual.Location.Z == zLevel`).

**But the DTO is already Z-capable.** Visuals are keyed `"x,y,z"`, `WorldLocationDto` carries `Z`, and `PerceptionService` already computes `relativeZ = location.Z - playerLocation.Z` — it just never carries anything but `relativeZ == 0` because vision only ever includes `origin.Z`. So stacked/cross-section rendering is impossible client-side alone; the first, load-bearing piece is a server change to emit a Z-slab, with no DTO schema change.

## Goals / Non-Goals

- Goals:
  - 3D **occluded** perception across a configurable band slab — a gameplay-visibility change, not just rendering.
  - Depth-cued rendering in both clients derived from one shared model (slab, falloff curve, occlusion rule).
  - Cross-section/elevation view, adaptive framing/slab, and an altitude gauge.
- Non-Goals:
  - No `PerceptionDto`/`VisualDto`/`WorldLocationDto` or wire schema change.
  - No heading-rotation fix (the live networked view does not rotate by heading; that is orthogonal).
  - Not finishing the Unity live path — build mock-frame-driven first.
  - Not stacking arbitrarily deep translucent bands in the plan view; use the cross-section view for depth.

## Decisions

- **Decision: emit a Z-slab, not a single slice.** The visible slab is `[focusZ - depthBelow, focusZ + depthAbove]`. The server produces it; the DTO is already Z-capable, so only production changes.
- **Decision: 3D occlusion via `ObstructsView.Opacity`, not movement blocking.** A vertical line-of-sight from viewer to a cell in another band is clear iff no intervening band has an opaque `ObstructsView` at that column (combined with the existing horizontal FOV). This is independent of `ObstructsMovement`: a glass skylight blocks movement but has `Opacity = 0`, so the ray is clear and the cell beyond is visible; a stone bridge is opaque, so the flyer above it is hidden and the bridge underside is what is seen. Terrain `BlocksLight` additionally governs whether the far cell is lit enough to see.
- **Decision: occluded off-focus cells are absent, not dimmed.** The slab is "the set of cells in nearby bands that pass a 3D FOV test," which is both cheaper and correct. Cells that *are* visible off-focus attenuate by `|dZ|`, reusing the existing per-tile `DimColor`/`GetInfraredColor` ramp multiplied with light level — no new color machinery.
- **Decision: bound the N-times cost.** Full fidelity (FOV + lighting) only for the focus band; off-focus bands get terrain occupancy + occlusion silhouette and at most coarse lighting, entities optional and count-capped. Cap slab depth per world; use the schematic cross-section view (one row per band, no per-tile FOV) for arbitrarily deep stacks.
- **Decision: keep the model shared so the two renderers agree.** Console composite and Unity stacking are two implementations of one model; define the slab, falloff curve, and occlusion rule server-side/shared.
- Alternatives considered: client-side-only stacking (rejected — the DTO only ever carries the focus band today, so there is nothing to stack); dimming all layers without occlusion (rejected — wrong gameplay; it would reveal the bird under an opaque bridge).

## Risks / Trade-offs

- Multi-band vision/lighting is the main cost risk → focus-full / off-focus-silhouette split and a slab cap; measure against the 60+ FPS / low-flicker constraints in `project.md`.
- Legibility vs. information (too many translucent layers become mush) → shallow default slab, lean on the cross-section view for depth.
- Two renderers diverge → keep the model shared/server-side.
- Unity live path is unfinished → mock-frame-driven development; do not block visualization on it.

## Migration Plan

Additive and backward-compatible. The DTO and wire format are unchanged, so existing clients keep working. New slab production is gated by per-world slab config; with slab depth 0 the server emits exactly today's focus-only frame and clients render identically. Roll out Phase 1 (server slab) first, then the console composite, then the Unity stack, then cross-section and adaptive behavior.

## Open Questions

- Exact depth-falloff curve constants and default slab caps per world archetype.
- Whether off-focus lighting is coarse-per-band or fully skipped for silhouettes.
- Threshold values for auto-slab expansion and mode escalation.
