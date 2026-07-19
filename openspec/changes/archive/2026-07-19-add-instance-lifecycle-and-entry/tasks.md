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

## Slice 2 — entry surface (this pass)
- [x] 5.1 `GameHub.EnterDungeon(dungeonId, partyId?)` + party ops (`CreateParty`/`JoinParty`/`LeaveParty`/`GetParty`) resolving the allocator/party grains from the session; new `EnterDungeonResultDto`/`PartyInfoDto`/`PartyMemberDto`.
- [x] 5.2 Player-profile agent tools `enter_dungeon` + `create_party` (category `instance`; add `instance` to the Player profile); `aetherctl instance enter|sweep` and `party create|add|show` commands + factory grain accessors.
- [x] 5.3 End-to-end test: form party → enter dungeon (allocate) → re-enter (reuse same instance); solo enter → leave → sweep frees the map; leader-leaving reassigns leadership. Plus tool-reachability (Player allows, Explorer denies).
- [x] 5.4 Full solution build + suite green.

## Deferred (future)
- `EnterDungeon` returns the instance + map id but does not yet rebind the caller's session to the instance map (full teleport like `JoinWorld` does). The client can `JoinWorld` the returned map; a one-call teleport is a follow-up.
- Raid stack (`RaidGrain`) has no entry surface yet; parties are the covered group type.
