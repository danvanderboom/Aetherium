# Audit: Console Client

*Audit date: 2026-07-03 · Scope: `Aetherium.Console` (~100 files, 11.3k lines): game loops, `GameClient`, rendering, audio, monitoring, self-test. Findings marked **Verified** or **Suspected**.*

> **Reconciliation — `develop` @ 2026-07-03.** Only two console files changed since baseline (`GameClient.cs`, `Geometry/MazeGenerator.cs`). **FIXED:** the High reconnect soft-lock — `GameClient.cs` now re-raises `Connected` on the auto-`Reconnected` path, so input resumes after a reconnect instead of being ignored forever. **Also fixed (Phase 1, commit `cd6cf67`):** the dead input block — the compass-mode/next-track logic, previously stranded after a `break;` following the Mode-4 case, moved into the `ConsoleKey.M` case; Shift+M and M now work as the help panel advertises. **STANDS:** unsynchronized torn-frame rendering, the ~6k lines of dead legacy client code (`MazeGenerator` was refactored — injected `Random`, `HashSet` membership — but the legacy game loops that use it remain uninstantiated and undeleted), and all the audio/monitoring findings. **New (minor):** during a disconnect the loop sets a `"Reconnecting…"` status but never re-renders, so it stays invisible until the next perception/command.

## Summary

The console client has a clean presentation abstraction (`IGameRenderer`/`GameViewState`, null-object audio, a coherent theme system) and a genuinely good self-test harness. But it carries three verified **High** runtime bugs — advertised features wired to unreachable code, torn-frame rendering from unsynchronized threads, and a soft-lock after reconnect — and **more than half the project (~5,800–6,300 lines across ~45 files) is dead legacy code** from the pre-client-server era. Several user-facing features silently don't work (status messages never render, audio has no assets and its null-fallback path is dead, map colors ignore the theme).

## Findings

**[High · Verified] Advertised music/compass controls are wired to unreachable code.** In `ClientConsoleDungeonGameNew.cs:329-341`, the Shift+M "next track" and M "compass mode toggle" logic sits *after* a `break;` in the `D4/NumPad4` case — its own `case` label was lost in an edit (compiles with CS0162). `case ConsoleKey.M` (`:294-296`) is an empty no-op. The help panel still advertises "[M] compass mode / Shift+M next track" (`SpectreConsoleRenderer.cs:223`). Both features are unreachable from the keyboard.

**[High · Verified] Unsynchronized cross-thread rendering → torn frames.** `OnPerceptionUpdated` runs on SignalR thread-pool threads (`GameClient.cs:33-36`) and calls `RenderCurrentState()`, while the input loop renders concurrently after each key — with no lock, over hundreds of non-atomic `SetCursorPosition`+`Write` pairs (`ClientConsoleMapView.cs:92-184`). Any perception update landing mid-render corrupts the frame.

**[High · Verified] Client soft-locks after a full disconnect/reconnect cycle.** `GameClient.cs:43-55`: the `Closed` handler (fires after `WithAutomaticReconnect()`'s 4 attempts are exhausted) manually restarts the connection but never re-raises `Connected` — only the auto `Reconnected` path does (`:57`). `HandleCommand` early-returns while `!connected` (`:206-207`), so after a successful manual restart input is ignored forever. On disconnect the UI never re-renders (rendering only happens on perception/post-command), so the "Disconnected" panel never shows — the game silently freezes. The retry delay `new Random().Next(0,5)*1000` can also be 0 ms.

**[Medium · Verified]**
- **`GameViewState.StatusMessage` is never rendered** (`SpectreConsoleRenderer` never references it) — every "Picked up item!", failure reason, and theme/mode change is invisible.
- **Spectre markup injection in inventory** — `InventoryWidget.cs:44-47` builds `"{Label} [{KeyId}]"` fed raw into `new Markup(...)` (`SpectreConsoleRenderer.cs:196-203`); a KeyId like `[gold-key]` throws at render time (dropped frame), and `[red]`-style text is parsed as color tags.
- **Committed `.ui-test` dirs permanently enable per-frame diagnostics** — `ClientConsoleMapView.cs:81-90` treats an existing `.ui-test` directory as test mode; `Aetherium.Console/.ui-test/` and root `.ui-test/` (16 files) are git-tracked, so normal gameplay does `Directory.Exists` + `File.WriteAllText` on every frame.
- **NAudio loop-restart handlers race with disposal** (`NAudioSystem.cs:86-94, 296-304`) — `PlaybackStopped` on the device thread can restart a just-disposed player; an exception on a NAudio callback thread can kill the process.
- **NullAudioSystem fallback never triggers** — the ctor guard (`ClientConsoleDungeonGameNew.cs:79-99`) catches only constructor exceptions, but `NAudioSystem` creates players lazily, so on audio-less/non-Windows machines every play call throws, is caught, and prints errors into the TUI, corrupting the display.
- **No audio assets ship and none copy to output** — only `.gitkeep` files exist; the `.csproj` has no `Content/CopyToOutputDirectory` for `Assets`, and `AudioConfig.AssetPath` is cwd-relative — audio is silent out of the box and unreachable from `bin/`.
- **Monitoring client list leaks + slow-consumer hazards** — `MapFrameMonitor.cs:25` uses a `ConcurrentBag<WebSocket>` (can't remove dead sockets); fire-and-forget broadcasts with `CancellationToken.None` (`:277`) let a stuck consumer accumulate unbounded pending tasks; JSON re-serialized per client per frame; `CaptureRenderedFrame` runs every perception even with zero monitors.

**[Low · Verified]** MapFrameLogger rotation hardcoded to 100 frames/file with no retention; port-5001 conflict survivable but mis-reported as "started"; no console-resize handling (a window < ~124 cols throws from `SetCursorPosition`, swallowed on the perception thread); 50 ms input polling latency + minimal modifier support; `ConsoleView.Clear` ignores its computed frame offset; whole-region redraw each frame; CompassWidget is 4-way only (can't crash, but 45° headings show a wrong cardinal and the disambiguating degree readout is unreachable); InventoryWidget has no item cap (>~8 items overflow the help panel); Escape exit fire-and-forgets disconnect and never stops the monitor.

## Verified leads (from the audit brief)

1. **Monster.cs `NotImplementedException` — partial/benign.** Actual location is `Entities/Monster.cs:78` (a `_ => throw` default arm covering all 6 `WorldDirection` values), **not ~145** — that figure came from a historical stack trace in `Goals.txt`. It is doubly unreachable: `Monster.Heartbeat` is only called by the legacy `ConsoleDungeonGame` (never instantiated), whose `AddMonsters()` is itself commented out. Not a live risk.
2. **Legacy game loops are dead code — confirmed.** `Program.cs:77` instantiates only `ClientConsoleDungeonGameNew`; `ConsoleDungeonGame` (386 lines) and `ClientConsoleDungeonGame` (594 lines) have zero live references, and they drag an **entire duplicate client-side engine** (World, Views/ConsoleMapView, Perception, Lighting, WorldBuilders, Entities, Components, Geometry) used by nothing else — **~5,800–6,300 lines / ~45 files, over half the project**. Shared pieces that must stay: `ConsoleView` (base of the live map view), `Enums.cs` (`RelativeDirection`), parts of `Extensions.cs`.
3. **Audio reverb is a pure stub** (`NAudioSystem.cs:346`); occlusion is volume-only (no frequency filtering); panning is computed then discarded (`:221-225`).
4. **Theme hot-reload — stronger than documented:** map tiles are drawn from *server-supplied* `TileTypeDto.Settings` colors via direct console writes (`ClientConsoleMapView.cs:294-336`); the theme is never consulted for map colors at all — not "needs restart," but *no code path exists*.
5. **9 TODOs, 0 FIXME/HACK** — including `World.cs:258` which marks a real (dead-code) bug: `TryMove(Character, RelativeDirection)` moves by `FromDelta(0,0,0)`, a no-op self-move.
6. **Monitoring server — no auth, but loopback-bound.** `MapFrameMonitor.cs:83` binds `http://localhost:5001/` (not 0.0.0.0); no auth/origin check on the WebSocket upgrade or `/health`/`/config`. Low risk given loopback, but the port/prefix is config-driven, so any future change to a wider bind exposes full perception (location, inventory, map) with zero gate.

## Strengths

- Clean `IGameRenderer` abstraction + `GameViewState` snapshot; hybrid rendering deliberately avoids full-screen clears to limit flicker.
- Well-shaped `IAudioSystem` with a null-object implementation and spatial hooks; `AudioDirector` implements danger-level hysteresis, biome ambient transitions, and terrain-aware footsteps exactly as the audio spec requires.
- `NAudioSystem` effect players are self-cleaning under lock; `Dispose` tears down defensively.
- The self-test harness is genuinely good: connection retry with backoff, rich artifacts (before/after text + color heatmaps + diff stats via a `ConsoleSnapshotter` P/Invoke), heuristic assertions, and an orchestrating script that manages server lifecycle.
- `MapFrameLogger` is properly serialized with a `SemaphoreSlim`; monitoring exposes sensible `/health` and `/config` with per-send error isolation.
- `ClientConsoleMapView` degrades gracefully (fallback player tile, missing-setting defaults, thoughtful color mapping within ConsoleColor's 16-color limit); the theme system is case-insensitive with a safe default and an ASCII Classic fallback.

## Spec alignment

- **demo-game spec — stale/violated.** It mandates `ConsoleDungeonGame(new TorusWorldBuilder())` at startup and describes the legacy control scheme (Z/X rotate, CapsLock ×10, Space follow-maze, M grid overlay); the live loop connects via SignalR with a different control set and M as a no-op. Never updated for the client-server migration.
- **console-view spec — partial.** It describes the dead legacy `ConsoleMapView` (heading rotation, character>object>terrain priority); the live `ClientConsoleMapView` delegates rotation to the server and renders no character/monster layer, so "characters render above objects" has no client counterpart. The spec doesn't cover the live view.
- **audio spec — partial.** Listener updates, distance attenuation, occlusion-as-volume, ambient loops, adaptive music with hysteresis, and terrain footsteps are implemented; reverb is a stub (not even an approximation), frequency-filter occlusion and panning are unimplemented, and with zero shipped assets every scenario is inaudible in practice.

## Test coverage

- **`CLIENT_TESTS_README.md` is aspirational:** it states client tests need a separate `Aetherium.Client.Test` project that does not exist; its templates reference drifted APIs.
- **`ClientUITests.cs` (11 facts) re-implement display logic inline** against Model DTOs — they never invoke the real renderer/widgets, so none of the High findings above would trip them.
- **Only `AudioDirectorTests.cs`** exercises real client code (`AudioDirector` vs a mock `IAudioSystem`).
- **SelfTest** covers exactly one E2E scenario (move-Backward, snapshot-diff heuristics), Windows-only, wired into `scripts/run-client-ui-tests.ps1` but **not CI** (`.github/workflows/` contains only `deploy-server.yml`, which runs no `dotnet test`).
- **Gaps:** zero tests for `GameClient` reconnection (where the soft-lock lives), `SpectreConsoleRenderer` (markup injection, StatusMessage omission), map rendering, `MapFrameMonitor` (leak, concurrent sends), NAudio disposal races, theme cycling, or the input dispatch table (a single "M toggles compass mode" test would catch the dead-block bug).
