# Audit: Narrative & Multi-World

*Audit date: 2026-07-03 · Scope: `Aetherium.Server/{Narrative, MultiWorld, Instances, Groups, MetaProgression, HubWorld}`, `Data/Narratives`. Findings marked **Verified** or **Suspected**.*

> **Reconciliation — `develop` @ 2026-07-03. Maturity 🔴 → 🟡.** Grain-authoritative multiplayer and durable persistence landed after the baseline, closing the two headline gameplay gaps. **FIXED:** multi-world travel now works end-to-end — `GameHub.JoinWorld` resolves the world/map grain, calls `IGameMapGrain.JoinPlayerAsync`, hydrates the session from a `WorldSnapshot` via `SnapshotWorldBuilder`, and routes mutations through a `GrainMutationGateway`; `UsePortal` resolves a portal target, transports the player, and emits `player_arrived`. Map world state is now durable (snapshot + delta-log). Co-op perception converges: a second player on the same grain-hosted map sees the first's mutations via delta fan-out. **PARTIAL:** the narrative consequence engine now runs for grain-bound sessions (the null-`WorldId` gate is satisfied by `JoinWorld`), but only `door_opened`/`door_closed`/`player_arrived` events reach it — `item_collected`/`item_used` are still never emitted, and legacy (non-`JoinWorld`) sessions still have null `WorldId`. **Also fixed (Phase 1, commit `cd6cf67`):** **`metaStore` is now registered** (`MetaProgressionGrain` no longer throws on activation), and narrative event seeds are now deterministic (`NarrativeConsequenceEngine.GetSeedForEvent` replaced `string.GetHashCode()`, randomized per process, with a stable FNV-1a hash). **STILL STANDS:** `ActiveQuestIds` is never populated (no StartQuest/Activate API), so `travel_to` objectives still cannot complete; ACL is still not enforced at join; the entire Instances/Parties/Raids stack is still unwired with zero tests; meta-progression is still keyed to the per-connection `SessionId` (though it can now at least persist, given the storage fix). **New multiplayer concerns (Verified/Suspected):** spawn allocation isn't reentrant-safe (join race); delta fan-out is fire-and-forget (`_ = FanOutAsync(...)`), so cross-delta ordering isn't guaranteed; and `UsePortal` sets `session.WorldId` before the new world is fully loaded (a documented TODO). Doc drift is unchanged: `multiworld-ecosystems.md`/`instances.md`/`narrative-systems.md` still claim "✅ Complete and tested" for quests/instances/meta that remain non-functional. Detail in the Reconciliation section at the end.

## Summary

This subsystem has the widest gap between documentation and reality in the whole codebase. Individual grains are well-built (named persistent stores, `[GenerateSerializer]`, a self-driving cluster economy, robust hub-world loading), and the tests validate each grain island in isolation. But **almost every bridge between grains is missing, unwired, or dead-ended**, and the docs describe the whole thing as complete and tested. In real gameplay: narrative consequences never fire, no player can travel between worlds, meta-progression records nothing (its storage provider isn't even registered), and the entire instances/parties/raids stack is unreachable code with zero tests.

| Severity | Count | Headline |
|---|---|---|
| Critical | 2 | `metaStore` provider unregistered (grain throws on every activation); narrative + portal gameplay dead-wired via null `session.WorldId` |
| High | 5 | `ActiveQuestIds` never populated (travel_to can't complete); map world-state never persisted; ACL never enforced; instance lifecycle leaks; Instances/Groups entirely unwired |
| Medium | 7 | Lockout check→record gap; meta keyed to wrong identity; narrative scope/seed dropped; `[Reentrant]` RMW race; unbounded state growth; non-deterministic seeds; no production world ticking |
| Low | 6 | OrleansWorldHost bugs; in-memory adaptive state; portal fallback teleports anywhere; dead ContentCatalogGrain; azure-storage silent no-op; … |

## Critical

**`metaStore` storage provider is never registered in the server silo.** *Verified.* `Program.cs:237-243` registers only `narrativeStore`/`worldStore`/`mapStore`, but `MetaProgressionGrain.cs:19` demands `[PersistentState("metaProgression","metaStore")]`. Every production activation throws (only the test fixture registers it). All 9 MetaProgressionController endpoints, GameHub discovery recording, and NarrativeStateGrain quest recording fail at runtime — the latter two swallowed by catch blocks, so meta-progression **silently records nothing**.

**Narrative + portal gameplay is dead-wired.** *Verified.* `session.WorldId` is only ever set inside a successful cross-world `UsePortal` (`GameHub.cs:482`), but real sessions are built via the legacy builder overload (`:104`) that leaves it null. `ProcessNarrativeEventAsync` early-returns on null `WorldId` (`:45`); `UsePortal` requires non-null `WorldId` for both resolution and transport (`:431,455`) — a circular dependency. So in real gameplay the consequence engine **never executes** and portals **never work**.

## High (verified)

- **`ActiveQuestIds` is never populated** — only removed/read; there is no StartQuest/ActivateQuest API. `HandlePlayerArrivedEventAsync` iterates an always-empty set, so `travel_to` objectives (the centerpiece of cross-world quests) can never complete.
- **Map world state never persisted** — `MapState.SerializedWorld` always null (TODO), reactivation restores a placeholder `new World()`, `GetWorldAsync` returns null. Any silo restart loses all generated terrain/entities/portals. (Cross-confirmed by the worldgen audit.)
- **ACL exists but is never enforced at join** — `WorldGrain.AddPlayerAsync` and `GameHub.JoinWorld` never consult `IWorldAclGrain.CanAccessAsync`; directory listing has a TODO for ACL filtering. Private worlds are decorative.
- **Instance lifecycle leaks** — `RemovePlayerAsync` marks `Abandoned` "cleaned up later" but no sweeper exists; `ReleaseInstanceAsync` never removes the instance map from `WorldInfo.MapIds`, so dead dungeon maps are ticked forever and accumulate unboundedly.
- **Instances/Groups subsystems entirely unwired** — no controller, hub, tool, or client calls `InstanceAllocatorGrain.EnterAsync`, `PartyGrain`, or `RaidGrain`; zero tests. The whole dungeon-instance/party/raid/lockout stack is unreachable.

## Medium (verified)

- **Lockout check→record gap** — `EnterAsync` records a lockout only on the new-allocation path, not the reuse path; the ledger is keyed by DungeonId while allocators are per-world, so two world allocators can interleave check/record; `RecordLockoutAsync` double-counts attempts and extends the lockout on every entry.
- **Meta-progression keyed to wrong identities** — GameHub keys the grain by the per-connection `SessionId`; NarrativeStateGrain keys it by `WorldId ?? StateId`. Even if `metaStore` existed, progression would never accrue to a stable player.
- **`WorldConfig.NarrativeStateScope`/`NarrativeSeed` dropped** — defined but never copied into `WorldInfo`/Metadata by `WorldGrain.InitializeAsync`; GameHub reads a metadata key that's never written → per-world scope and deterministic narrative seed are unusable.
- **`[Reentrant]` WorldGrain with read-modify-write across awaits** — `AddPlayerAsync`'s capacity check and `PlayerCount++` straddle an await, so interleaved calls can exceed MaxPlayers.
- **Unbounded state growth** — `GeneratedQuests` (GUID-suffixed quests minted per event), `CompletedQuestIds`, `Relationships`, `CompletedObjectives`, `WorldInviteGrain` invites, and `LockoutLedgerGrain.Lockouts` are all uncapped (only the event log is capped at 1000).
- **Non-deterministic "deterministic" seeds** — `GetSeedForEvent` uses `string.GetHashCode()` (randomized per process), violating the narrative spec's determinism requirement.
- **World simulation never ticks in production** — `WorldTickService` is an empty delay loop (TODO); only ClusterGrain's economy timer actually fires. (Cross-confirmed by the simulation audit.)

## Low (verified)

`OrleansWorldHost` bugs (sync-over-async `.Wait()`; `InviteAsync` self-invites; `AcceptInviteAsync` linear-scans up to 1000 worlds); `AdaptiveNarrativeGrain` state is in-memory only; portal fallback teleports to "first market in the cluster" (worsened by never-populated world metadata); dead `ContentCatalogGrain`; `ORLEANS_STORAGE=azure` silently registers no storage (commented-out branch).

## Verified leads (from the brief)

1. **Confirmed (worse)** — `NarrativeConsequenceEngine` is invoked only from GameHub interaction handlers, best-effort; the new `ExecuteTool` path dropped the `item_collected`/`item_used` events; nothing publishes to the `WorldEvent` Orleans stream (subscribe-only); and the null-`WorldId` gate means it never runs in real gameplay.
2. **Partial/Confirmed** — `LoreGenerator` is used only in the CLI-only `EnvironmentalStoryPass` (absent from the server's runtime pass list, so runtime worlds get no lore); `NarrativeGraphGenerator` and `RelationshipMatrix` have zero callers (dead code).
3. **Confirmed** — grain-to-grain portal/cluster/economy internals are genuinely wired, but no player can travel between worlds (traced: local diagnostic world with no portals → `JoinWorld` hard-fails → `UsePortal` needs a WorldId only `UsePortal` can set → no client caller). World `Metadata`/tags are never populated, so all tag matching degrades to "first world" fallback.
4. **Confirmed** — `Data/Narratives/*.json` load only via `aetherctl narrative load` (graceful on malformed JSON), not at startup — inert until an operator runs the CLI. (Contrast: hub JSON auto-loads at startup.)

## Strengths

- Consistent Orleans patterns: named stores, `[GenerateSerializer]`/`[Id]` on all state models; the event log is bounded (1000).
- The worldgen→map→cluster portal registration chain is genuinely wired; `PortalNetworkPass` supports both authored and procedural placement.
- ClusterGrain economy is a complete self-driving loop (transports depart/arrive, restock routes, supply/demand pricing with clamps).
- Hub-world loading is robust and actually wired (per-file try/catch, tag normalization, `HUB_PATH` override, startup load, request-override merging).
- `aetherctl narrative load` validates and reports JSON errors cleanly; the JSON files match the schema.
- The lockout ledger's dual party/player keying and time+attempt policy is a sound design.

## Spec & doc alignment

**narrative spec**: procedural quest generation, NPC relationship networks, and consequence propagation are met on paper but unmet in the running system (generators uncalled, relationships dead, consequence events never emitted, `travel_to` can't complete); deterministic generation is violated by `GetHashCode` seeding; environmental storytelling exists only in the CLI pipeline. **add-multiworld-ecosystems/tasks.md** is all-checked but tasks 1.9/1.10/1.14 are non-functional or untested. **Doc drift** across `docs/multiworld-ecosystems.md`, `docs/instances.md`, `docs/narrative-systems.md`: all present travel, cross-world quests, meta-unlocks, instance cleanup, and event types as working/"✅ Complete and tested," none of which function in gameplay. These three docs should be reframed as **design/aspiration** with a clear "implemented vs planned" split.

## Test coverage

Grain-level CRUD is covered (NarrativeGrain 15, WorldGrain 13, GameManagementGrain 15, ClusterGrain 8, MetaProgressionGrain 9 — the last registers its own `metaStore`, masking the production gap; PortalNetworkPass 7; MultiWorld region tests). **Gaps (zero tests):** NarrativeStateGrain (quest completion, travel_to, relationships), NarrativeConsequenceEngine, Lore/Graph generators, RelationshipMatrix, CrossWorldConstraintResolver, AdaptiveNarrativeGrain, WorldAcl/Invite/Directory grains, OrleansWorldHost, Party/Raid/InstanceAllocator/DungeonInstance/LockoutLedger grains, HubWorldLoader, and every GameHub cross-world method. **Nothing exercises any cross-grain end-to-end flow** — precisely where every broken seam lives.
