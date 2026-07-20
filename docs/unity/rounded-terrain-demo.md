# Rounded terrain demo (Unity)

Smooth, lit, animated water for the demo world — square terrain reinterpreted as
curved coastlines with foam/shallows and a per-frame atmosphere tint, entirely
client-side (no server or wire change). Implemented under
`Assets/Scripts/Rendering/Water/` + `Rendering/TerrainLighting.cs` +
`Art/Shaders/RoundedWater.shader`, driven by the OpenSpec change
`add-rounded-terrain-rendering`.

## See it in ~30 seconds

1. Open the Unity project (`Aetherium.Unity`, Unity `6000.4.6f1`).
2. Create a new empty scene (or use any scene).
3. Create an empty GameObject and add the **`RoundedTerrainDemo`** component
   (Add Component → search "Rounded Terrain Demo").
4. Press **Play**.

The bootstrap builds a Grid + band-stack land renderer + water-mesh renderer + an
orthographic camera, loads
`StreamingAssets/PerceptionFrames/rounded-water-demo.json` (a lake with a sandy
island), and renders it. If the JSON is missing it falls back to an identical
built-in lake, so it always shows something.

## What to look for (the visual sign-off)

- **Curved coastline** — the lake edge is smooth and rounded, not a staircase of
  squares, while still hugging the underlying cells (marching squares → Chaikin →
  a signed-distance field the shader thresholds).
- **Foam band** at the shoreline that **animates** (drifts/shimmers over time).
- **Shallows → deep gradient** — lighter near the shore, deeper blue toward the
  middle.
- **The sandy island** reads correctly: the land tile draws over the water surface
  (no water bleeding across it).
- **Warm atmosphere** — the whole scene is tinted toward golden-hour; land near the
  "north" (top) is a touch brighter (per-cell light level).
- **Waves** — a faint moving ripple on the open water.

## Knobs to tweak

On the water material / `RoundedWater.shader` (select a `Water_*` object at runtime,
or edit the material defaults):

| Property | Effect |
|---|---|
| `_ShallowColor` / `_DeepColor` | shore vs. deep water color |
| `_FoamColor`, `_FoamWidth`, `_FoamSpeed` | foam look + animation |
| `_ShoreWidth` | how far the shallows gradient reaches inward |
| `_WaveScale`, `_WaveSpeed`, `_WaveStrength` | ripple size / speed / amount |
| `_EdgeSoftness` | anti-aliasing of the carved coastline |
| `_WaterAlpha` | base transparency |

On `WaterRegionRenderer` (mesh detail): `smoothIterations` (0 = blocky, 2–3 =
organic), `subdivisions` (SDF sampling density → curve smoothness), `shoreWidth`.

On `RoundedTerrainDemo`: `frameFileName` (swap in another perception frame),
`cameraPadding`, `backgroundColor`.

## How it fits the existing renderer

- **Land** stays on the Tilemap band stack (`BandStackRenderer`); its
  `SkipRegionTerrain` is on so water cells aren't drawn as tiles.
- **Water** is a separate mesh per band (`WaterRegionRenderer`) that reuses the exact
  same depth alpha (`DepthShading.AlphaForDepth`) and `sortingOrder` as the tilemap,
  so it composites correctly through the Z-band stack and sits just behind that band's
  land tiles.
- **Lighting** multiplies each land cell's color by its `LightLevel` × the frame's
  `AmbientTint` (`TerrainLighting`); water gets the ambient tint via the shader's
  `_AmbientTint`. No switch to the URP 2D Renderer — the 3D pipeline is untouched.

## Verification

All non-visual logic is covered by headless tests (Unity batchmode `-nographics`):
- EditMode: `MarchingSquaresTests`, `ChaikinSmoothingTests`, `ShoreDistanceTests`,
  `TerrainRegionMaskTests`, `RegionFieldTests`, `WaterMeshBuilderTests`,
  `TerrainLightingTests`.
- PlayMode: `WaterRegionRenderingTests`, `TilemapLightingTests`,
  `RoundedTerrainDemoTests`.

The *appearance* is covered too, by an automated render sign-off:
`RoundedWaterVisualTests` (PlayMode) renders the demo lake to a RenderTexture on a
real GPU and asserts the measurable half of "looks good" — deep water reads blue,
shore is brighter than the deep interior (foam + shallows), the sandy island reads
warm (not washed over), pixels shift frame-to-frame (waves animate), and the warm
ambient render is warmer than a neutral one. It also writes the frames to PNG
(`ROUNDED_WATER_CAPTURE_DIR`, default `Application.temporaryCachePath/rounded-water-verify`)
for a human glance. It needs a graphics device, so run it *without* `-nographics`:

```
Unity.exe -runTests -batchmode -projectPath Aetherium.Unity -testPlatform PlayMode \
  -testFilter Aetherium.Unity.Tests.RoundedWaterVisualTests -testResults results.xml
```

The only thing left to human taste is whether the water is *pretty* — the captured
PNGs above are there for that glance.
