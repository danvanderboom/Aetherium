## Purpose
Defines the demo console app entry point, default world, and interactive controls for navigation.

## Requirements

### Requirement: Program Entry and World Selection
The demo SHALL start a console game using a selected world builder.

#### Scenario: Torus world by default
- WHEN the program starts
- THEN it MUST construct `ConsoleDungeonGame(new TorusWorldBuilder())` and run it

### Requirement: Controls
The demo SHALL support camera movement, rotation, altitude changes, and overlays.

#### Scenario: Movement with arrows
- WHEN arrow keys are pressed
- THEN the camera MUST move relative to heading (Forward/Backward/Left/Right)
- AND with CapsLock held, step size MUST increase

#### Scenario: Rotation
- WHEN `Z` is pressed, heading MUST rotate left
- WHEN `X` is pressed, heading MUST rotate right

#### Scenario: Altitude
- WHEN `U` is pressed, Z MUST increase by 1 (or 10 with CapsLock)
- WHEN `D` is pressed, Z MUST decrease by 1 (or 10 with CapsLock)

#### Scenario: Grid coloring toggle
- WHEN `M` is pressed
- THEN the view MUST toggle a grid coloring overlay or clear it

#### Scenario: Follow maze
- WHEN `Space` is pressed
- THEN follow-maze mode MUST toggle, causing the camera to track maze generation


