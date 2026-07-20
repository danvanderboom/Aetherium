## 1. Pure geometry & lighting core (headless-testable)
- [x] 1.1 `TerrainRegionMask` — build a per-band boolean region mask for a terrain predicate (e.g. "water") from `PerceptionLite.Visuals`, with 4/8-neighbour queries keyed on `(x,y)`; topology-parameterized (square now, hex/H3 seam)
- [x] 1.2 `MarchingSquares` — trace region-boundary loops from the mask via directed-edge cancellation + linking; consistent winding; collinear-point simplification; handle islands/holes and disjoint regions
- [x] 1.3 `ChaikinSmoothing` — corner-cutting subdivision of closed loops, configurable iterations (0 = blocky, 2–3 = organic); preserves faithfulness (smoothed loop stays within a bounded band of the cell outline)
- [x] 1.4 `ShoreDistance` — per-vertex normalized distance-to-region-boundary over a configurable shore width (drives the shader foam/shallows gradient)
- [x] 1.5 `TerrainLighting` — pure `Modulate(baseColor, lightLevel, ambientTint)`: darken by `LightLevel` (0..1), tint toward `AmbientTint` (white = no-op), clamped
- [x] 1.6 EditMode tests: `TerrainRegionMaskTests`, `MarchingSquaresTests` (single cell, L-shape, island/hole, two disjoint lakes), `ChaikinSmoothingTests` (vertex growth, convergence, faithfulness bound), `ShoreDistanceTests`, `TerrainLightingTests` — **verified 71/71 EditMode green** (28 new + 43 pre-existing) in Unity `6000.4.6f1` batchmode `-nographics`

## 2. Water mesh + band integration
- [x] 2.1 `WaterMeshBuilder` + `RegionField` — tessellate a fine sub-grid over the region and bake a **signed distance to the Chaikin-smoothed coastline** into each vertex (UV2), giving the shader an interior gradient for foam/shallows; `RegionField` winding-number inside-test handles islands/holes for free. Chosen over ear-clipping the outline (which yields only boundary vertices → no shore gradient).
- [x] 2.2 `WaterRegionRenderer` MonoBehaviour — per band, rebuild the water mesh when the water cell-set changes (dirty check); reuse `DepthShading.AlphaForDepth` and `SortingOrderForBand`; parented under the band alongside its Tilemap
- [x] 2.3 `TilemapRenderer2D` — skip cells matched by the shared "region terrain" predicate so land Tilemap and water mesh never double-draw
- [x] 2.4 PlayMode `WaterRegionRenderingTests` — a lake frame yields a non-empty mesh with expected bounds, subdivided vertex count (> cell count), correct band alpha/sortingOrder, and no double-drawn cells — **verified: EditMode 78/78, PlayMode my 4/4 green** (only the 3 pre-existing `Main`-scene tests fail, unchanged from baseline)

## 3. Rounded-water URP shader + material
- [x] 3.1 `RoundedWater.shader` (URP unlit, transparent, Cull Off) — signed-distance carve for the smooth curved edge, shallows→deep gradient, animated foam band from UV1 shore distance + `_Time`, wave shimmer; `_BandAlpha` for depth falloff, `_AmbientTint` for atmosphere. **Compiles clean in headless import (no shader errors).**
- [x] 3.2 Material created at runtime from the shader (`Shader.Find("Aetherium/RoundedWater")` in `WaterRegionRenderer`) — no hand-authored `.mat` GUID asset needed; one can be added later for inspector tweaking
- [x] 3.3 Visual verification: foam hugs the coast, shallows gradient reads, waves animate, depth falloff matches the Tilemap bands. Discharged as an **automated render-to-texture sign-off** (`RoundedWaterVisualTests`, PlayMode) that renders the demo lake to a RenderTexture on a real GPU and asserts the *measurable* half of "looks good" — deep water reads blue, shore is brighter than the deep interior (foam + shallows gradient), the sandy island reads warm (not washed over), water pixels shift frame-to-frame (waves animate), and the warm ambient render is measurably warmer than a neutral one. It also dumps the frames to PNG for a human aesthetic glance. Self-skips (Assert.Ignore) under `-nographics` so headless CI stays green. **Verified green in Unity `6000.4.6f1` batchmode *with graphics* (D3D11 / RTX 4080), and the captured frames were eyeballed: rounded coastline, foam rim, teal shallows→deep gradient, island showing through, warm golden atmosphere.**

## 4. Lighting & atmosphere pass
- [x] 4.1 Added additive `AmbientTint` (`AmbientTintLite`, default white) + `Topology` (default "square") to `PerceptionLite`; auto-parsed by `PerceptionMockProvider` (Newtonsoft); mirrors `PerceptionDto`
- [x] 4.2 `TilemapRenderer2D` opt-in `ApplyLighting` — per-cell `LightLevel × AmbientTint` applied as a per-cell tint over the untouched base color (readback/tests preserved); `BandStackRenderer` pass-through
- [x] 4.3 Ambient tint applied through the color path (per-cell tile modulation + water shader `_AmbientTint`) rather than a separate URP Volume component — keeps the 3D pipeline untouched; a Volume-based time-of-day grade remains optional future polish
- [x] 4.4 EditMode `TerrainLightingTests` (color math) + PlayMode `TilemapLightingTests` (per-cell application); the `rounded-water-demo.json` fixture carries per-cell light + a warm ambient tint

## 5. Runnable demo + verification
- [x] 5.1 `rounded-water-demo.json` — a 15×15 lake (52 water cells + a sandy island) ringed by sand + grass, per-cell light gradient, warm ambient tint
- [x] 5.2 `RoundedTerrainDemo` bootstrap — one component builds Grid + band stack (skip-water + lighting) + `WaterRegionRenderer` + a framed orthographic camera and loads the lake (JSON fixture, with an identical deterministic built-in fallback); drop into an empty scene, press Play
- [x] 5.3 Headless verified in Unity `6000.4.6f1` batchmode `-nographics`: **EditMode 78/78** (35 new pure tests), **PlayMode 39/42** (my 9 new tests all green); the only 3 PlayMode failures are the pre-existing `Main`-scene tests (unchanged baseline); the water shader imports with no errors
- [x] 5.4 Setup/verify guide `docs/unity/rounded-terrain-demo.md` — press-Play steps, what to look for, knobs
- [x] 5.5 `openspec validate add-rounded-terrain-rendering --strict` → valid
