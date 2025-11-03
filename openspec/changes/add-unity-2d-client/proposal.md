## Why
Add a Unity 2D client targeting PC and iOS platforms to expand beyond the console client. This enables roguelike/MUD-style tilemap rendering with Z-level support, providing a foundation for future 2D side-scroller or 3D/volumetric explorations while keeping server-authoritative architecture.

## What Changes
- New Unity 2023.3 LTS 2D project under `Aetherium.Unity/`
- Tilemap renderer mapping Perception DTOs to Unity 2D Tilemap with Z-level cycling
- Input system handling movement, rotation, and Z-level changes
- Offline Mock mode (default) replaying Perception JSON sequences from StreamingAssets
- Optional Live mode connecting to SignalR GameHub (`http://localhost:5000/gamehub`) behind `USE_SIGNALR` scripting define
- EditMode and PlayMode tests including UI automation
- Documentation in `docs/unity/` for setup, run modes, and testing

## Impact
- Affected specs: `client` (adds Unity client capability)
- Affected code: New `Aetherium.Unity/` folder; no changes to existing projects
- Build impact: Unity project must be opened in Unity Editor; does not affect existing .NET builds

