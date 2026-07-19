## Why
The console client's audio was audible in spec but broken in practice (Phase 5 item **P3-9**, `docs/audits/2026-07-03-initial-subsystem-audit/console-client.md`):

- **The null-fallback path was dead.** `CreateAudioSystem` wrapped `new NAudioSystem(config)` in a try/catch and only returned `NullAudioSystem` if the *constructor* threw — but the constructor never touches a device. NAudio opens the output device lazily on first play, so on audio-less or non-Windows hosts every play call threw, was caught, and wrote `[Audio] Error …` straight into the Spectre TUI — corrupting the frame.
- **Assets were unreachable even if present.** No audio files ship (only `.gitkeep`/README), the `.csproj` copied nothing to output, and `AudioConfig.AssetPath` was cwd-relative — so lookups from `bin/` could never resolve.
- **Reverb and panning overclaimed.** `SetReverbPreset` was a pure stub; `PlayPositionalEffect` computed a pan value and silently discarded it. The audio spec's "Acoustic Simulation" requirement implied both worked.

(The previously-flagged dead `M`/`Shift+M` input block was already fixed on `develop`; nothing to do there.)

## What Changes
- **Graceful, silent fallback.** New `NAudioSystem.IsOutputAvailable()` (the winmm backend is Windows-only, so non-Windows → unavailable). `CreateAudioSystem` now selects `NullAudioSystem` up front when audio is disabled or unavailable. `NAudioSystem` gains a private `HandlePlaybackError` that, on any device/playback exception, mutes audio for the session and records `LastError` — **never** writing to the console. Missing-asset lookups return silently (no console spam), and are *not* treated as device errors (audio stays enabled).
- **Correct asset resolution.** `NAudioSystem.AssetRoot` resolves `AssetPath` under `AppContext.BaseDirectory` when relative; the `.csproj` copies `Assets/Audio/**` to output. So dropping real CC0 files into `music/`/`effects/` (names per the README) just works — no code change.
- **Honest reverb/panning.** Removed the dead pan computation; documented reverb DSP and stereo panning as **unsupported** (spatial cues are distance-attenuation + occlusion-volume only). `CurrentReverbPreset` is retained for introspection. README states audio is silent-by-default and why.
- **Tests** (audio had almost none): `NAudioSystemTests` — fallback probe never throws, asset-root resolves under base dir (relative) / is preserved (absolute), missing assets are silent and keep audio enabled, reverb preset is recorded despite unsupported DSP.

## Impact
- Affected specs: `audio` (ADDED: Audio Runtime Availability & Fallback, Audio Asset Resolution; MODIFIED: Acoustic Simulation to state reverb/panning unsupported).
- Affected code: `Aetherium.Console/Audio/NAudioSystem.cs`, `Aetherium.Console/Core/ClientConsoleDungeonGameNew.cs`, `Aetherium.Console/Aetherium.Console.csproj`, `Aetherium.Console/Assets/Audio/README.md`; new `Aetherium.Test/Audio/NAudioSystemTests.cs`.
- Build impact: additive; no breaking changes. Behavior change: on non-Windows/audio-less hosts the client is now cleanly silent instead of corrupting the TUI with error text.

## Status
Implemented on `feat/phase5-audio` (branched from `develop`). Full solution build 0 errors; new `NAudioSystemTests` (5) green; full Audio filter 37 passed. Reverb DSP / stereo panning / frequency-domain occlusion remain deliberately unsupported (scoped down, stated honestly) — real DSP is out of scope for this polish pass.
