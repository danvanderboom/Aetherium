## 1. Graceful silent fallback
- [x] 1.1 `NAudioSystem.IsOutputAvailable()` (winmm is Windows-only → non-Windows unavailable)
- [x] 1.2 `CreateAudioSystem` selects `NullAudioSystem` when disabled or unavailable (up front, not ctor-only)
- [x] 1.3 `HandlePlaybackError` mutes + records `LastError` on device/playback exceptions; no `Console.WriteLine`
- [x] 1.4 Missing-asset lookups return silently and keep audio enabled (not a device error)

## 2. Asset resolution
- [x] 2.1 `NAudioSystem.AssetRoot` resolves `AssetPath` under `AppContext.BaseDirectory` when relative
- [x] 2.2 `.csproj` copies `Assets/Audio/**` to output (`CopyToOutputDirectory=PreserveNewest`)
- [x] 2.3 README states silent-by-default + how to add real CC0 assets

## 3. Honest reverb/panning
- [x] 3.1 Remove dead pan computation; document panning unsupported (single mixed output)
- [x] 3.2 `SetReverbPreset` documented unsupported; `CurrentReverbPreset` retained for introspection

## 4. Tests
- [x] 4.1 `NAudioSystemTests`: probe never throws; asset-root relative→under base dir / absolute→preserved; missing asset silent + stays enabled; reverb preset recorded
- [x] 4.2 Full solution build + suite green
