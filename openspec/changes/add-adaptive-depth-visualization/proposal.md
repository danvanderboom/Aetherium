## Why

Once the world has meaningful vertical structure — subway tubes at bands -1...-4, streets at 0, monorail viaducts and skyways at +1...+6 — a single flat Z-slice can no longer communicate what a character can see or act on. Perception is **single-Z today**: the server computes vision, FOV, and lighting for only the player's current band, so the (already Z-capable) `PerceptionDto` never carries anything but the focus slice. Stacked and cross-section rendering are therefore impossible client-side alone. This change makes perception genuinely **3D and occluded** across a band range, then makes both renderers **adapt to depth**. See `docs/design/adaptive-depth-visualization.md`.

## What Changes

- **Server perception becomes a 3D occluded slab** (the load-bearing first step). Vision/FOV/lighting compute over a configurable range of altitude bands instead of only the player's band, using per-band sight opacity (`ObstructsView.Opacity`) for a vertical line-of-sight test. Only cells that pass the 3D FOV test are emitted, each tagged with its `relativeZ`. **The `PerceptionDto` schema is unchanged** — only its production changes.
- **Console depth composite + level ribbon.** Replace the hardcoded `relativeZ = 0` loop with a per-screen-cell walk over the slab, reusing the existing `DimColor` dimming ramp keyed on `|dZ|`. Add a HUD level ribbon showing the band stack and the focus band.
- **Unity multi-band stack + camera + theming.** One `Tilemap` per band with opacity falloff and `sortingOrder`; a real orthographic follow camera with adaptive framing; wire the currently-stubbed tile color/sprite mapping.
- **Cross-section / elevation view** in both clients for reading multi-level interchanges.
- **Adaptive framing/slab + mode escalation + altitude gauge.** Focus band auto-follows the player's Z; slab depth expands in interchanges and collapses in flat terrain; camera pulls back / cross-section surfaces past a vertical-complexity threshold; an altitude gauge shows the flyer's band within its envelope.
- Off-focus bands that are occluded are **absent, not merely dimmed** (cheaper and correct). No breaking DTO or wire changes.

## Impact

- Affected specs: `perception` (3D occluded slab), `console-view` (depth-cued rendering, level ribbon, cross-section), `client` (Unity multi-band rendering + camera, altitude gauge)
- Affected code:
  - Server: `Aetherium.Console/Perception/VisionSystem.cs`, `FovCalculator.cs`, `Aetherium.Console/Lighting/LightingSystem.cs`, `Aetherium.Server/PerceptionService.cs`
  - Console client: `Aetherium.Console/Views/ClientConsoleMapView.cs`, `Aetherium.Console/Core/ClientConsoleDungeonGameNew.cs`
  - Unity client: `Aetherium.Unity/Assets/Scripts/Rendering/TilemapRenderer2D.cs`, `GameManager.cs`, `Spatial/GridHelpers.cs`, plus new camera + HUD components
  - Unchanged (already Z-capable): `Aetherium.Model/PerceptionDto.cs`, `VisualDto.cs`, `WorldLocationDto.cs`
- Design reference: `docs/design/adaptive-depth-visualization.md`
