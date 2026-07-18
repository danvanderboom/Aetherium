## 1. 3D Occluded Perception Slab (server) — load-bearing first
- [x] 1.1 Add per-world slab configuration (`depthBelow`/`depthAbove`, depth cap) threaded through world/perception creation as data (`World.SlabDepthBelow`/`SlabDepthAbove`/`SlabDepthCap`, default 0 = single-Z)
- [x] 1.2 Extend `FovCalculator` from 2D to a vertical FOV: for a target cell in another band, combine the existing horizontal FOV with a vertical line-of-sight test that is clear iff no intervening band has opaque `ObstructsView.Opacity` at that column (`BandVerticalOpacity` + `VerticalVisibleBands`)
- [x] 1.3 Update `VisionSystem.ComputeVision` to iterate the configured Z-range instead of only `origin.Z`, producing visibility per `(x, y, z)`
- [x] 1.4 Update `LightingSystem` to provide at least coarse lighting per band in the slab (`AddCoarseSlabLighting`: off-focus cells inherit focus-column light attenuated by band distance); focus-band lighting unchanged
- [x] 1.5 Update `PerceptionService` to emit every band's cells that pass the 3D FOV test, each tagged with the existing `relativeZ`; keep the `PerceptionDto`/`VisualDto`/`WorldLocationDto` schema unchanged (no schema change; the existing relative-key loop already handles multi-Z)
- [x] 1.6 Cost control: focus-band full fidelity / off-focus silhouette split (off-focus emits only non-empty cells), slab depth cap — the optional per-`(focusZ, viewport)` slab cache is deferred as a pure optimization (correctness first)
- [x] 1.7 Tests: bird-under-bridge (occluded), bird-through-skylight (visible), level-below-through-grate (visible), solid-pavement (not visible); `relativeZ` tagging; DTO schema unchanged (`PerceptionSlabTests`, 7)

## 2. Console depth composite + level ribbon
- [x] 2.1 Replace the hardcoded `relativeZ = 0` loop in `ClientConsoleMapView` with a per-screen-cell walk over the slab from `focusZ` outward (both `DrawContents` and `CaptureRenderedFrame` via `BuildColumnIndex` + `SelectDisplayVisual`)
- [x] 2.2 Composite rule: topmost drawable glyph within the slab wins (focus band prioritised so the player's own level stays legible), off-focus cells dimmed by `|dZ|` via `DepthFactor` × light level through the existing `DimColor`/`GetInfraredColor` ramp; entity-only off-focus cells render as silhouettes. (Console is one glyph/cell, so translucent "blending beneath" reduces to depth-dimming the chosen glyph.)
- [x] 2.3 Keep the player screen-centered using the existing viewport math (no change)
- [x] 2.4 Add the HUD level ribbon showing the band stack and a marker for `focusZ` (`BuildLevelRibbon` + `DrawLevelRibbon`)
- [x] 2.5 `focusZ` auto-follows the player's Z — inherent to the relative-coordinate model (player always at `dZ 0`) since the server re-centres perception on the player's band each frame and the ribbon re-derives from fresh perception
- [x] 2.6 Verified via the deterministic ASCII-capture tests (`ConsoleDepthViewTests`, `DepthIntegrationTests`) that drive the same `CaptureRenderedFrame`/`AsciiMapData` path `monitor-lite.ps1` consumes; a live `monitor-lite.ps1` run additionally needs a running server (`ws://localhost:5001/monitor`)

## 3. Unity stack + camera + theming
- [ ] 3.1 Wire the stubbed tile color/sprite mapping in `TilemapRenderer2D` so bands and terrains are distinguishable
- [ ] 3.2 Render one `Tilemap` per band in the slab, stacked by `sortingOrder`, with `TilemapRenderer` alpha set from `|dZ|` (focus opaque, deeper bands more transparent)
- [ ] 3.3 Add a real orthographic follow camera that tracks the player
- [ ] 3.4 Map DTO to `PerceptionLite` for multi-Z frames; build/verify against the JSON mock provider first so the unfinished live path does not block visualization
- [ ] 3.5 EditMode/PlayMode tests for multi-band rendering and alpha falloff

## 4. Cross-section / elevation view (both clients)
- [ ] 4.1 Console: a toggle-key side-on schematic that draws the column around the player as stacked bands (one row per band, no per-tile FOV)
- [ ] 4.2 Unity: an equivalent cross-section overlay
- [ ] 4.3 Verify both against a multi-level interchange column

## 5. Adaptive framing/slab + mode escalation
- [ ] 5.1 Auto-slab: expand `depthBelow`/`depthAbove` when the local column has many occupied bands, collapse in flat terrain
- [ ] 5.2 Mode escalation past a vertical-complexity threshold: surface the cross-section view (console) or pull the camera back / adapt `orthographicSize` toward an isometric framing (Unity) to the local vertical extent
- [ ] 5.3 Altitude gauge: console glyph ladder / Unity HUD meter of `N` discrete steps over the flyer's `[MinBand, MaxBand]`, current band highlighted
- [ ] 5.4 Context tint: reuse vision modes (`Torch`/`Sunlight`/`Ambient`, `Normal`/`Infrared`) so underground bands default to torch/enclosed cueing and skyways to sunlight
- [ ] 5.5 Measure against the 60+ FPS / low-flicker constraints in `project.md`
