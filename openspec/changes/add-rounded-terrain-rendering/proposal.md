## Why

The demo game world renders as flat, solid-color squares — one tinted 1×1 sprite per cell (`TilemapRenderer2D` + `TileTheme`). Lakes, forests, and coastlines read as blocky "2D Minecraft" rather than a hand-painted map. The client already receives everything needed to do better — per-cell terrain via the `Visuals` dict, per-cell `LightLevel` (parsed but currently unused), and the server already computes `AmbientTint`/`GameTimeOfDay`/`Weather` on `PerceptionDto` — but the renderer discards it. This change reinterprets the same square cell data as smooth, lit, curved terrain, **entirely client-side**, without changing the wire protocol or the server's perception production.

## What Changes

- **Rounded coastlines from the cell mask.** Derive terrain-region adjacency client-side from the existing `Visuals` dict, trace region boundaries with marching squares, relax them with Chaikin subdivision, and render water (and other "region" terrains such as lava) as a smooth generated **mesh overlay** per band. Land stays on the Tilemap. Faithful to every cell centre; curved around corners.
- **SDF-style animated coastline shader.** A custom URP water material driven by a CPU-baked per-vertex distance-to-shore: animated foam bands, a wet-sand/shallows gradient, and gentle wave shimmer over `_Time`. Honors the band stack's alpha falloff and `sortingOrder`.
- **Lighting & atmosphere pass.** Fold the already-arriving per-cell `LightLevel` and a new client-side `AmbientTint` into each tile's color (extends the existing tint model — no pipeline change), plus a time-of-day post-grade hook on the already-active URP Volume. **No** switch to the URP 2D Renderer, so the 3D Deferred/SSAO pipeline and band compositing stay intact.
- **Client DTO enrichment (additive).** Thread the server's existing `AmbientTint` and `Topology` into `PerceptionLite` (additive, defaulted; mirrors how `FlightEnvelope` was added). No server or wire change — the fields already exist on `PerceptionDto`.
- **Runnable demo.** A `rounded-water-demo.json` mock frame containing an actual lake, and a one-component `RoundedTerrainDemo` bootstrap that composes Grid + band stack + camera + water overlay at runtime, so the effect is playable from an empty scene (the committed `Main.unity` is a bare template).
- Square topology only for now; the region-tracing seam is topology-parameterized so hex/H3 can follow. Not breaking.

## Impact

- Affected specs: `client` (Unity rounded-terrain mesh + water shader, lighting/atmosphere pass, demo bootstrap)
- Affected code:
  - Unity (new): `Assets/Scripts/Rendering/Water/{TerrainRegionMask,MarchingSquares,ChaikinSmoothing,ShoreDistance,WaterMeshBuilder,WaterRegionRenderer}.cs`, `Assets/Scripts/Rendering/TerrainLighting.cs`, `Assets/Art/Shaders/RoundedWater.shader` (+ `RoundedWater.mat`), `Assets/Scripts/Demo/RoundedTerrainDemo.cs`
  - Unity (modified): `Assets/Scripts/Rendering/TilemapRenderer2D.cs` (lighting-modulated tile color, water-cell skip), `TileTheme.cs` (lighting helper), `Assets/Scripts/Model/{PerceptionLite,VisualLite}.cs` (`AmbientTint`, `Topology`), `Networking/PerceptionMockProvider.cs` + fixtures
  - Tests: EditMode `TerrainRegionMaskTests`, `MarchingSquaresTests`, `ChaikinSmoothingTests`, `ShoreDistanceTests`, `TerrainLightingTests`; PlayMode `WaterRegionRenderingTests`
  - Unchanged (already emits the data): `Aetherium.Model/PerceptionDto.cs` (`AmbientTint`, `Topology`), `VisualDto.cs` (`LightLevel`)
- Design reference: `openspec/changes/add-rounded-terrain-rendering/design.md`
