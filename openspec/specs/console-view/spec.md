## Purpose
Describes console rendering primitives and the map view behavior including heading and overlays.

## Requirements

### Requirement: Framed Console View Rendering
The view system SHALL support drawing an optional frame and clearing the content area.

#### Scenario: Draw frame
- WHEN `ConsoleView.DrawFrame()` is invoked on a framed view
- THEN it MUST render a rectangular border using the configured frame colors and size

#### Scenario: Clear content area
- WHEN `ConsoleView.Clear()` is invoked
- THEN it MUST fill the content rectangle (inside the frame if present) with spaces using the background color

### Requirement: Map Rendering and Orientation
The map view SHALL render tiles from the `World` around a `WorldLocation` with camera heading.

#### Scenario: Centered camera and rotation
- WHEN `ConsoleMapView.DrawContents()` renders
- THEN the `WorldLocation` MUST appear centered in the view
- AND the grid MUST rotate according to heading (N/E/S/W)

#### Scenario: Tile draw priority
- WHEN multiple entities share a location
- THEN characters MUST render above objects, which render above terrain

### Requirement: Grid Coloring Overlay
The map view SHALL optionally color cells using a repeating color grid overlay.

#### Scenario: Apply grid coloring
- WHEN `GridColoring` is set
- THEN the background color for each cell MUST come from the overlay
- AND the player marker on the center MUST still render distinctly


