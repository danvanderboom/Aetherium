## Why
The dungeon-instance / party / raid stack is fully built at the grain level (`InstanceAllocatorGrain`, `DungeonInstanceGrain`, `LockoutLedgerGrain`, `PartyGrain`, `RaidGrain`) but is **entirely unwired and untested** — nothing in the hub, tools, or CLI calls it, and it has zero tests. This is Phase 5 item **P3-4**. Two concrete defects make the stack unsafe even once it is reachable:

- **Instance lifecycle leaks.** `DungeonInstanceGrain.RemovePlayerAsync` marks an empty instance `Abandoned` "to be cleaned up later," but no sweeper exists, and `InstanceAllocatorGrain.ReleaseInstanceAsync` never removes the instance's map from `WorldInfo.MapIds`. Abandoned dungeon maps are therefore **ticked forever** by the world tick and accumulate without bound.
- **Lockout check→record gap.** `EnterAsync` records a lockout only on the new-allocation path, never on the reuse path (a loophole), and `RecordLockoutAsync` increments the attempt count and extends the lockout window on *every* entry — including reuse of the same instance — so a player's own re-entry double-counts against them.

## What Changes
**Slice 1 — instance lifecycle correctness (this pass):**
- Add `IWorldGrain.RemoveMapAsync(mapId)`: removes a map from `WorldInfo.MapIds` (so it stops being ticked/saved/loaded) and drops any player locations pointing at it.
- `InstanceAllocatorGrain.ReleaseInstanceAsync` now removes the instance's map via `RemoveMapAsync` — closing the "dead maps ticked forever" leak.
- Add `InstanceAllocatorGrain.SweepAbandonedInstancesAsync()` that reaps instances reported `Abandoned`/`Stopped` (or idle past a threshold) and returns the count swept; drive it from a grain timer registered on activation. To avoid a grain-reentrancy deadlock (allocator → `instance.ShutdownAsync` → allocator), add `IDungeonInstanceGrain.TeardownAsync()` (stop without the allocator callback); the sweeper calls it and then releases locally, and `ShutdownAsync` now delegates to `TeardownAsync` + release.
- Lockout fix: record the lockout on the reuse path too (no loophole), and make `RecordLockoutAsync` idempotent for the **same** instance — re-entering your own run no longer increments attempts or extends the window.
- Tests (the stack had **zero**): map removal stops ticking; release removes the map; the sweeper reaps abandoned instances; lockout records once per distinct instance and does not double-extend on reuse.

**Slice 2 — entry surface (follow-up):**
- A reachable entry point: `GameHub.EnterDungeon` and party operations, an agent tool, and an `aetherctl` command, so players/agents/operators can actually form parties and enter instances.

## Impact
- Affected specs: `instances` (ADDED: instance lifecycle & lockout correctness)
- Affected code (slice 1): `Aetherium.Server/MultiWorld/IWorldGrain.cs` + `WorldGrain.cs` (`RemoveMapAsync`), `Aetherium.Server/Instances/IInstanceAllocatorGrain.cs` + `InstanceAllocatorGrain.cs` (map cleanup on release, sweeper, timer, lockout-on-reuse), `Aetherium.Server/Instances/LockoutLedgerGrain.cs` (idempotent record); new `Aetherium.Test/Instances/*`.
- Build impact: additive grain API; no breaking changes. Existing allocation/enter behavior is unchanged except that released instances now free their map and re-entry no longer double-counts lockouts.

## Status
Slice 1 implemented on `feat/phase5-instances` (branched from `develop`). Verified: full solution build 0 errors; new `InstanceLifecycleTests` (6) green — map removal (+ unknown-map false), release frees the instance map, the sweeper reaps an abandoned instance and frees its map (and leaves an active one alone), and lockout recording is idempotent per instance. A grain-reentrancy deadlock surfaced by the sweeper test was fixed via `TeardownAsync`. **Full suite: 997 passed / 0 failed / 1 pre-existing seed-tolerant skip.** Slice 2 (hub/tool/CLI entry surface) tracked but out of scope for this pass.
