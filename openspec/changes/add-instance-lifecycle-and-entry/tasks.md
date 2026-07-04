## Slice 1 — instance lifecycle correctness (this pass)

### 1. Map removal
- [x] 1.1 Add `IWorldGrain.RemoveMapAsync(string mapId)` returning bool
- [x] 1.2 `WorldGrain.RemoveMapAsync`: remove from `Info.MapIds`, drop matching `PlayerLocations`, persist

### 2. Release + sweep
- [x] 2.1 `InstanceAllocatorGrain.ReleaseInstanceAsync` removes the allocation's map via `RemoveMapAsync`
- [x] 2.2 Add `SweepAbandonedInstancesAsync()` (interface + impl): reap instances reported `Abandoned`/`Stopped` or idle past a threshold; return count
- [x] 2.3 Register a grain timer on activation to run the sweeper periodically
- [x] 2.3a Add `IDungeonInstanceGrain.TeardownAsync()` (stop without calling back the allocator); the sweeper uses it + a local `ReleaseInstanceAsync` to avoid a grain-reentrancy deadlock (allocator→instance→allocator). `ShutdownAsync` now delegates to `TeardownAsync` + release.

### 3. Lockout correctness
- [x] 3.1 `EnterAsync` records the lockout on the reuse path too
- [x] 3.2 `RecordLockoutAsync` is idempotent for the same instance (`ApplyReEntry`: no attempt increment / no window extension on re-entry of the same InstanceId; a distinct instance still counts)

### 4. Tests
- [x] 4.1 `RemoveMapAsync` drops the map from `GetMapIdsAsync` (+ returns false for unknown map)
- [x] 4.2 `ReleaseInstanceAsync` removes the instance map
- [x] 4.3 Sweeper reaps an abandoned instance (and frees its map); leaves an active instance alone
- [x] 4.4 Lockout records once per distinct instance; reuse does not double-extend
- [x] 4.5 Full solution build + suite green

## Slice 2 — entry surface (follow-up, not this pass)
- [ ] 5.1 `GameHub.EnterDungeon` + party ops (create/join/leave/get) resolving grains from the session
- [ ] 5.2 Agent tool + `aetherctl instance|party` command
- [ ] 5.3 End-to-end test: form party → enter dungeon → leave → sweep
