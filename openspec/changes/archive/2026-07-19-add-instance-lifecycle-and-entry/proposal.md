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

**Slice 2 — entry surface (this pass):**
- A reachable entry point: `GameHub.EnterDungeon(dungeonId, partyId?)` and party operations (`CreateParty`/`JoinParty`/`LeaveParty`/`GetParty`), player-profile agent tools (`enter_dungeon`/`create_party`), and `aetherctl instance enter|sweep` / `party create|add|show` — so players, agents, and operators can actually form parties and enter instances. All resolve the allocator/party grains from the world/session. New standalone `EnterDungeonResultDto`/`PartyInfoDto`/`PartyMemberDto`.

## Impact
- Affected specs: `instances` (ADDED: instance lifecycle & lockout correctness; instance/party entry surface)
- Affected code (slice 1): `Aetherium.Server/MultiWorld/IWorldGrain.cs` + `WorldGrain.cs` (`RemoveMapAsync`), `Aetherium.Server/Instances/IInstanceAllocatorGrain.cs` + `InstanceAllocatorGrain.cs` (map cleanup on release, sweeper, timer, lockout-on-reuse), `Aetherium.Server/Instances/IDungeonInstanceGrain.cs` + `DungeonInstanceGrain.cs` (`TeardownAsync`), `LockoutLedgerGrain.cs` (idempotent record).
- Affected code (slice 2): `Aetherium.Server/GameHub.cs` (enter/party methods), new `Aetherium.Model/InstanceDtos.cs`, new `Aetherium.Server/Agents/Tools/Instances/{EnterDungeonTool,CreatePartyTool}.cs`, `AgentToolProfile.cs` (`instance` category), `Aetherctl/Commands/InstanceCommands.cs` + `OrleansClientFactory.cs` + `Program.cs`; new `Aetherium.Test/Instances/*` + `AgentToolProfileTests.cs`.
- Build impact: additive grain/hub/tool/CLI API; no breaking changes. Existing allocation/enter behavior is unchanged except that released instances now free their map and re-entry no longer double-counts lockouts.

## Status
Both slices implemented (slice 1 on `feat/phase5-instances`, slice 2 on `feat/phase5-instance-entry`, both branched from `develop`). Slice 1: `RemoveMapAsync`, release/sweeper free abandoned maps, lockout idempotence, and a `TeardownAsync` fix for a sweep reentrancy deadlock. Slice 2: `GameHub.EnterDungeon` + party ops, `enter_dungeon`/`create_party` tools, and `aetherctl instance|party` commands, with an end-to-end grain test (party enter → reuse; solo enter → leave → sweep; leader reassignment) and tool-reachability tests. Verified: full solution build 0 errors; **full suite green**. Deferred: session teleport into the instance map on enter (client can `JoinWorld` the returned map for now); raid entry surface.
