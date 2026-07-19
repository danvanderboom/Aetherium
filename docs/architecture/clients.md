# Client Architecture

*Last updated: 2026-07-19. Covers the shared `Aetherium.Client` library, `Aetherium.Console`, the `com.aetherium.unity` Unity package + Aphelion sample (and the legacy `Aetherium.Unity`), `Aetherium.Dashboard`, and the planned Unreal client. See [overview.md](overview.md) for the protocol they share.*

All clients are thin renderers of server-computed perception. They connect to `/gamehub` over SignalR, send actions (preferably via `ExecuteTool`), and re-render whenever a `PerceptionDto` arrives. No game rules live client-side.

## Shared client library (`Aetherium.Client`)

A reusable .NET library that factors the non-visual client concerns out of any one renderer: SignalR connection lifecycle, perception/interoception subscription, action dispatch, and **session resume** across reconnects (the session re-attaches to its world rather than starting fresh). It references only `Aetherium.Model`, so it carries no rendering assumptions. The Unity package consumes it as a bundled `Aetherium.Client.dll`; future clients (including Unreal) are expected to build on it too. Covered by an in-proc integration suite in `Aetherium.Client.Tests`.

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

## Unity client

The current Unity client is a **reusable package + sample** pair, both under version control:

- **`com.aetherium.unity`** (`clients/unity/com.aetherium.unity/`) — a reusable Unity package that renders server perception: `GridMapView` (grid/tilemap), the depth **band stack** and cross-section overlay for vertical worlds, a `FollowCamera`, tile themes, an `EntityViewRegistry` binding entity kinds to models, and a `MainThreadDispatcher`. It talks to the server through the bundled `Aetherium.Client.dll` (real SignalR) rather than DTO shims, and supports **session resume**.
- **`samples/unity/Aphelion`** (`samples/unity/Aphelion/`) — the **Aphelion** sample game: a co-op sci-fi station/planet crawler wiring the package to a live server, with a bootstrap scene, URP setup, relative arrow-key controls, suit-lamp lighting, per-creature Quaternius models, and last-seen "ghost" rendering. Design: [Unity sample suite](../design/unity-sample/README.md); broader vision in the [arcade-client design](../design/arcade-client/README.md).

Depth-aware rendering (band stack, cross-section, adaptive framing, HUD altitude gauge) mirrors the server's 3D perception slab — see [adaptive-depth-visualization](../design/adaptive-depth-visualization.md).

### Legacy `Aetherium.Unity` scaffold

The original Unity client — a Unity 2D project (not part of `Aetherium.sln`) targeting PC and iOS — is superseded by the package + sample above but remains in-tree. Docs: [docs/unity/README.md](../unity/README.md), [docs/unity/testing.md](../unity/testing.md).

#### Mock-first architecture

Two interchangeable perception providers sit behind a `GameClientFacade` MonoBehaviour:

- **`PerceptionMockProvider`** — replays JSON perception frames from `Assets/StreamingAssets/PerceptionFrames/`, enabling fully offline development and deterministic tests.
- **`PerceptionSignalRClient`** — live server connection, compiled only when the `USE_SIGNALR` define is set (the SignalR client package must be imported manually; not in the Unity Package Manager).

`GameManager` consumes provider events and drives `TilemapRenderer2D` (perception → Unity tilemap) and `PlayerController` (sprite, HUD, input).

#### DTO shims

Unity deliberately does **not** reference `Aetherium.Model`; it defines serialization-friendly "Lite" mirrors (`PerceptionLite`, `VisualLite`, `WorldLocationLite`, …) under `Assets/Scripts/Model/`. This avoids pulling server assemblies into Unity and works around `JsonUtility`'s lack of dictionary support via custom parsing. The cost is a schema-drift risk against the real DTOs — a known trade-off (same pattern the Unreal guide prescribes).

#### Input

New Input System (`InputActions.inputactions`) with full Xbox controller support: left stick → cardinal movement, LB/RB rotate, LT/RT z-level, A confirm, B cancel, D-pad option navigation; multi-option selection mode suspends movement until confirm/cancel. Covered by PlayMode tests (`GamepadInputTests`, `OptionSelectionTests`, `InputAutomationTests`, `TilemapAndInputTests`) plus EditMode JSON-parsing tests.

## Dashboard (`Aetherium.Dashboard`)

Blazor Server app for operators and agent-training workflows. Data paths: an Orleans cluster client (`OrleansClientConnectionService`, with retry/backoff), a YARP reverse proxy forwarding `/agentDashboardHub` to the game server, and typed HTTP clients (`PcgApiClient`, `ManagementApiClient`) for REST endpoints.

Pages: Index (status), AgentMonitor (live telemetry), AdaptationMonitor, BehaviorAnalysis, PerformanceAnalytics, BenchmarkComparison, CurriculumProgress (training), PCG (interactive generation), Worlds (world management), ReplayViewer.

Status note (2026-07-19): the Dashboard **now compiles** (`dotnet build Aetherium.Dashboard` → 0 errors, a handful of warnings). The blockers flagged in the 2026-07-03 audit — Orleans client calls predating the hosted-client model, and a `Pages/BehaviorAnalysis.razor` type-shadowing collision — have since been fixed (see the `finish-dashboard-decoupling` and `complete-dashboard-stub-pages` OpenSpec changes). It still has no dedicated test coverage. Historical context: [docs/audits/unity-and-dashboard.md](../audits/2026-07-03-initial-subsystem-audit/unity-and-dashboard.md).

## Planned Unreal client

No Unreal code exists yet. [docs/clients/unreal-client-guide.md](../clients/unreal-client-guide.md) is the migration guide: reuse the `Aetherium.Client` library (connection, perception subscription, session resume) and `Aetherium.Model` DTOs, implement `IGameRenderer` (`UnrealGameRenderer`), `IAudioSystem`, and input mapping. The server needs no changes to support it.
