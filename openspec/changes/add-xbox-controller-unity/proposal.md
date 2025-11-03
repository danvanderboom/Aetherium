## Why
Add Xbox controller (Gamepad) support to the Unity client for Windows to enable alternative input methods. This includes support for multi-option tool selection, allowing players to navigate and select from multiple usage options when tools return multiple choices (e.g., multi-use items). This enhances accessibility and provides a more traditional gamepad experience for players.

## What Changes
- Gamepad input bindings for movement (left stick), rotation (LB/RB), and level changes (LT/RT)
- Option selection mode for multi-use tools with HUD overlay display
- Async tool execution API returning ToolExecutionResultDto for option handling
- Axis-based rotation and level change input (1D axis composites)
- Option navigation (D-Pad Up/Down), confirm (A), and cancel (B) controls
- Enhanced PlayerController with option selection state management
- ToolExecutionResultDto and UsageOptionDto models for Unity client
- Comprehensive test coverage for Gamepad input and option selection

## Impact
- Affected specs: `client` (modifies Unity client input handling, adds multi-option selection)
- Affected code: 
  - `Aetherium.Unity/Assets/InputActions.inputactions` (Gamepad bindings)
  - `Aetherium.Unity/Assets/Scripts/Rendering/PlayerController.cs` (option selection)
  - `Aetherium.Unity/Assets/Scripts/Networking/GameClientFacade.cs` (async tool execution)
  - New model classes for tool execution results
- Build impact: No breaking changes; backward compatible with keyboard input

