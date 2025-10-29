## Why
Players should only see what their character can see. Movement obstructions (e.g., mountains) and sight obstructions (e.g., forest density) must be independent.

## What Changes
- Add FOV computation with cumulative opacity and Bresenham ray casting
- Add VisionSystem to produce `VisionFrame` from FOV results
- Bind `ConsoleMapView` to render only visible cells
- Configure terrain defaults for sight opacity (Wall/Mountain=1, Forest=0.5, Water=transparent)
- Treat open doors as transparent to vision

## Impact
- Affected specs: `perception-vision`
- Affected code: `ConsoleGame/Perception/*`, `ConsoleGame/Views/ConsoleMapView.cs`, world builders


