## Context

The Unity renderer today is flat tinted squares: `TilemapRenderer2D` stamps one shared 1×1 white sprite per cell, tinted by `TileTheme.ColorFor(name)`. URP 17.4 is installed but configured as the **3D Deferred** pipeline (`PC_Renderer` = `UniversalRendererData`, deferred + SSAO) — **not** the URP 2D Renderer, so `Light2D` is unavailable without pipeline surgery. Depth is composited by `BandStackRenderer` (one Tilemap per Z band) via tilemap **alpha** (`DepthShading.AlphaForDepth = 1/(1+0.5|dZ|)`) and **sortingOrder** (`= band Z`). The client "Lite" DTOs drop data the server already sends: `VisualLite.LightLevel` is parsed but unused, and `PerceptionDto.AmbientTint`/`Topology` never reach the client. `Main.unity` is the bare URP template (none of the renderers are in it); `PerceptionMockProvider` (StreamingAssets JSON) is the only working data path — the live SignalR client is a stub. Unity `6000.4.6f1`; `openspec` 1.5.0; `dotnet` 10.

## Goals / Non-Goals

- Goals: smooth, curved region terrain (water) faithful to each cell; animated coastline (foam + shallows); per-cell lighting + scene ambient tint; a runnable one-component demo; all geometry/color logic headless-testable; **zero** server/wire change.
- Non-Goals: hex/H3 rounding (leave a seam only); URP 2D Renderer / `Light2D` migration; finishing the live SignalR path; hand-authored per-terrain art; any server-side perception change.

## Decisions

- **Lighting in the tint model, not `Light2D`.** The pipeline is 3D Deferred and band compositing rides on tilemap alpha/sort; switching to the 2D Renderer would rip up SSAO/deferred and the band stack. Decision: modulate each tile's color by `LightLevel × AmbientTint`. Alternatives rejected: URP 2D Renderer + `Light2D` (pipeline surgery, high risk), custom lit sprite material (heavier, needs normal maps for little gain here).
- **Client-side adjacency + generated mesh, not SpriteShape/Rule Tiles.** The whole `Visuals` dict is already local, so neighbor lookup is free; procedural, per-frame terrain fits a generated mesh far better than editor-spline SpriteShape or a hand-authored 47-tile blob set, and adds no packages. Alternatives rejected: SpriteShape (editor-spline authoring, awkward to drive per-frame), Rule Tiles (needs authored corner art; still tile-grained), fullscreen SDF RenderTexture (deferred — heavier GPU plumbing than v1 needs).
- **Shore distance baked per-vertex on the CPU; the shader stays simple.** A jump-flood SDF texture is deferred (more GPU plumbing) — per-vertex signed distance-to-coastline drives foam/shallows in a lightweight URP unlit-transparent shader. Revisit an SDF texture only if we want sub-cell precision or reflections.
- **Water mesh = sub-grid tessellation with a baked signed-distance field, not an ear-clipped outline.** Ear-clipping the smoothed boundary yields only boundary vertices, so there is no interior gradient for foam/shallows. Instead we tessellate a fine sub-grid over the region and bake `RegionField.SignedDistance` (to the Chaikin-smoothed coastline) into each vertex; the shader thresholds it for the smooth curved edge and a foam band near zero. The winding-number inside-test makes islands/holes fall out for free (water winds to +1, island interior to 0), avoiding hole-bridging entirely. Cost: some overdraw of a one-cell land collar (discarded in the shader); acceptable and dirty-checked.
- **Water is a mesh overlay that coexists with the band Tilemap.** Each band gets a `WaterRegionRenderer` that reuses the band's `AlphaForDepth` and `SortingOrderForBand`; `TilemapRenderer2D` skips cells matched by a shared "region terrain" predicate so land and water never double-draw.
- **Additive DTO enrichment.** `AmbientTint` (rgb, default white) and `Topology` (string, default `"square"`) go on `PerceptionLite`, mirroring the earlier additive `FlightEnvelope`. The server already emits both on `PerceptionDto`; this is purely a client mapper/fixture concern.

## Risks / Trade-offs

- Cannot visually verify shaders in this environment → mitigate with headless logic tests (marching squares, Chaikin, shore distance, lighting math), a one-component in-Editor bootstrap, and a verify guide; the developer does the visual sign-off.
- Per-frame mesh rebuild could cost on large water bodies → rebuild only when the water cell-set changes (dirty check), cap region size, reuse mesh buffers.
- Triangulating loops with holes (an island in a lake) → handle even-odd fill with a library-free ear-clip + hole bridging; explicitly test the island case.
- The band stack is currently exercised only by tests (`GameManager` uses the single-Tilemap path) → the demo bootstrap wires the band path explicitly, and the water renderer supports both single- and multi-band.

## Migration Plan

Additive only. Default topology `"square"`, default tint white, and `LightLevel` default `1.0` mean existing frames render identically except that water now curves and cells honor light. No spec archive/migration. Rollback = remove `WaterRegionRenderer` and revert the one-line tile-color modulation.

## Open Questions

- Which terrains count as "region" terrains besides water (lava? shallow vs deep water)? Default: water + lava, behind a configurable predicate.
- Foam palette and wave speed — tune during the visual sign-off.
