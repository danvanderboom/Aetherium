# Client Architecture

*Last updated: 2026-07-03. Covers `Aetherium.Console`, `Aetherium.Unity`, `Aetherium.Dashboard`, and the planned Unreal client. See [overview.md](overview.md) for the protocol they share.*

All clients are thin renderers of server-computed perception. They connect to `/gamehub` over SignalR, send actions (preferably via `ExecuteTool`), and re-render whenever a `PerceptionDto` arrives. No game rules live client-side.

## Console client (`Aetherium.Console`)

The primary, most complete client: a Windows terminal UI built on a hybrid of Spectre.Console (widgets, chrome) and direct console writes (the map, for performance and flicker control).

### Structure

| Area | Purpose |
|---|---|
| `Client/GameClient.cs` | SignalR connection lifecycle, perception subscription. Platform-agnostic — reusable by future clients |
| `Core/ClientConsoleDungeonGameNew.cs` | Live game loop (rendering + audio integration). `ClientConsoleDungeonGame` (legacy client loop) and `ConsoleDungeonGame` (pre-client-server single-process game) remain in-tree but are superseded |
| `Rendering/` | `IGameRenderer` abstraction + `SpectreConsoleRenderer`; `GameViewState` snapshot; theme system (Zen, Cyberpunk, Halloween, Winter, Classic); `ClientConsoleMapView` draws the map |
| `Rendering/Widgets/` | Self-contained widgets (`CompassWidget` — currently 4-way, `InventoryWidget`) that auto-show/hide from perception data |
| `Audio/` | `IAudioSystem` with `NAudioSystem` (Windows), `MauiAudioSystem` (macOS/iOS), `NullAudioSystem` fallback when assets are missing. Music playlist + SFX triggers (footsteps, doors, pickup/drop, teleport). Reverb/occlusion are stubs |
| `Monitoring/` | `MapFrameMonitor` — embedded WebSocket server (`ws://localhost:5001/monitor`) broadcasting raw perception JSON + rendered ASCII map per frame; optional `MapFrameLogger` file logging; `/health` endpoint |
| `SelfTest/` | UI automation harness (`--ui-selftest`, driven by `scripts/run-client-ui-tests.ps1`) |

### Design notes

- The renderer abstraction means game-loop code never touches Spectre directly; a new presentation layer only implements `IGameRenderer` + `IAudioSystem`.
- Do not clear the console after the map draws (`AnsiConsole.Clear()` erases the direct-rendered map) — a standing constraint documented in [openspec/project.md](../../openspec/project.md).
- Input is keyboard-only by design (arrows/WASD movement, Q/E–Z/X rotation, M/N audio controls). Controller support lives in the Unity client.
- Theme switching applies live to widgets but requires restart for map colors (direct console writes bypass Spectre).
- Further reading: `Rendering/README.md`, `Monitoring/README.md`, `Assets/Audio/README.md` in the project; user docs under [docs/console/user/](../console/user/).

## Unity client (`Aetherium.Unity`)

A Unity 2023.3 LTS 2D project (not part of `Aetherium.sln`) targeting PC and iOS. Docs: [docs/unity/README.md](../unity/README.md), [docs/unity/testing.md](../unity/testing.md).

### Mock-first architecture

Two interchangeable perception providers sit behind a `GameClientFacade` MonoBehaviour:

- **`PerceptionMockProvider`** — replays JSON perception frames from `Assets/StreamingAssets/PerceptionFrames/`, enabling fully offline development and deterministic tests.
- **`PerceptionSignalRClient`** — live server connection, compiled only when the `USE_SIGNALR` define is set (the SignalR client package must be imported manually; not in the Unity Package Manager).

`GameManager` consumes provider events and drives `TilemapRenderer2D` (perception → Unity tilemap) and `PlayerController` (sprite, HUD, input).

### DTO shims

Unity deliberately does **not** reference `Aetherium.Model`; it defines serialization-friendly "Lite" mirrors (`PerceptionLite`, `VisualLite`, `WorldLocationLite`, …) under `Assets/Scripts/Model/`. This avoids pulling server assemblies into Unity and works around `JsonUtility`'s lack of dictionary support via custom parsing. The cost is a schema-drift risk against the real DTOs — a known trade-off (same pattern the Unreal guide prescribes).

### Input

New Input System (`InputActions.inputactions`) with full Xbox controller support: left stick → cardinal movement, LB/RB rotate, LT/RT z-level, A confirm, B cancel, D-pad option navigation; multi-option selection mode suspends movement until confirm/cancel. Covered by PlayMode tests (`GamepadInputTests`, `OptionSelectionTests`, `InputAutomationTests`, `TilemapAndInputTests`) plus EditMode JSON-parsing tests.

## Dashboard (`Aetherium.Dashboard`)

Blazor Server app for operators and agent-training workflows. Data paths: an Orleans cluster client (`OrleansClientConnectionService`, with retry/backoff), a YARP reverse proxy forwarding `/agentDashboardHub` to the game server, and typed HTTP clients (`PcgApiClient`, `ManagementApiClient`) for REST endpoints.

Pages: Index (status), AgentMonitor (live telemetry), AdaptationMonitor, BehaviorAnalysis, PerformanceAnalytics, BenchmarkComparison, CurriculumProgress (training), PCG (interactive generation), Worlds (world management), ReplayViewer.

Status note (2026-07-03): the Dashboard **does not currently compile** (and hasn't since it was first committed ~8 months ago) — its Orleans client calls (`IClusterClient.Connect/Close`) predate Orleans 7's hosted-client model, and `Pages/BehaviorAnalysis.razor` has a type-shadowing collision (the unqualified name `BehaviorAnalysis` binds to the Razor component class instead of the server model). See [docs/audits/unity-and-dashboard.md](../audits/unity-and-dashboard.md). It has no test coverage.

## Planned Unreal client

No Unreal code exists yet. [docs/clients/unreal-client-guide.md](../clients/unreal-client-guide.md) is the migration guide: reuse `Aetherium.Model` DTOs and `GameClient` connection logic, implement `IGameRenderer` (`UnrealGameRenderer`), `IAudioSystem`, and input mapping. The server needs no changes to support it.
