> Status (2026-07-03): the Unity 6 project now imports and compiles (P2-10), mock mode is functional, and Edit/PlayMode tests compile. Under-reporting drift corrected below — most boxes were unchecked despite the work existing. Remaining gaps: live SignalR mode is a stub, tile-type→sprite mapping is unimplemented (tiles render spriteless), and Main.unity is an empty template scene.

## 1. Implementation
- [x] 1.1 Create OpenSpec change add-unity-2d-client with proposal, tasks, deltas
- [x] 1.2 Create Aetherium.Unity 2D project and baseline folders (checked 2026-07-03: project exists with Scripts/{Model,Networking,Rendering,Spatial}, Scenes, Tests)
- [x] 1.3 Install packages and create Core/EditMode/PlayMode asmdefs (checked 2026-07-03: Packages/manifest.json with InputSystem/TestFramework/Newtonsoft.Json; three asmdefs in their own folders)
- [x] 1.4 Add Unity-friendly Perception DTO shims under Scripts/Model (checked 2026-07-03: PerceptionLite, VisualLite, WorldLocationLite, etc.)
- [x] 1.5 Implement PerceptionMockProvider reading JSON frames (checked 2026-07-03: uses JsonConvert so dictionaries populate)
- [x] 1.6 Add GameClientFacade with ExecuteTool and mode switching (checked 2026-07-03: includes async ExecuteToolAsync)
- [x] 1.7 Implement TilemapRenderer2D with Z-layer support (checked 2026-07-03: renderer exists, but tile-type→sprite mapping is unimplemented — CreateDefaultTile produces spriteless tiles, so tiles render invisible)
- [x] 1.8 Configure Input System actions and handlers (checked 2026-07-03: InputActions.inputactions + PlayerController, keyboard and gamepad schemes)
- [ ] 1.9 Build Main.unity with Grid, player marker, HUD (unchecked 2026-07-03: Main.unity exists but is an empty template scene — Main Camera, Directional Light, Global Volume only; no Grid, player marker, or HUD objects)
- [x] 1.10 Write EditMode test for perception JSON parsing (checked 2026-07-03: Tests/EditMode/PerceptionParsingTests.cs, compiles)
- [x] 1.11 Write PlayMode tests for render and movement (checked 2026-07-03: Tests/PlayMode/TilemapAndInputTests.cs et al. compile; runtime pass not verified — scene lacks the objects in 1.9)
- [ ] 1.12 Add SignalR client behind USE_SIGNALR define (unchecked 2026-07-03: PerceptionSignalRClient exists behind USE_SIGNALR but is a stub — HubConnection commented out, ExecuteToolAsync always returns failure)
- [x] 1.13 Write docs/unity setup, run modes, testing guides (checked 2026-07-03: docs/unity/README.md + testing.md)
- [x] 1.14 Update .gitignore, commit with conventional commits and push (checked 2026-07-03: .gitignore has Unity meta/Packages rules; project committed)
