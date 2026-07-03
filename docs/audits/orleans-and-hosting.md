# Audit: Orleans Grains & Hosting

*Audit date: 2026-07-03 · Scope: `Aetherium.Server/Program.cs` hosting/DI, all Orleans grains, persistence, the `DISABLE_ORLEANS` path, and the co-hosting DI bridge. Findings marked **Verified** or **Suspected**.*

## Summary

The grain layer is built with consistent, idiomatic Orleans patterns (constructor-injected `[PersistentState]`, `OnActivate` init, `WriteState` on mutation, deterministic composite keys, write-throttling awareness) and the core game/world/cluster grains have real integration tests. But the **hosting composition in `Program.cs` has a startup-fatal DI defect**, the **entire world-tick pipeline is undriven in production**, world state is **never actually persisted**, and one grain persists to an **unregistered storage provider**. In other words: much of the grain machinery is well-built but not actually reachable in a running, Orleans-enabled server. This audit's top finding gates all the others — if the server can't boot with Orleans on, nothing downstream runs.

| Severity | Count | Headline |
|---|---|---|
| Critical | 2 | Self-referential co-hosting DI bridge (startup-fatal with Orleans on); `metaStore` unregistered |
| High | 5 | Tick pipeline undriven; `GameMapGrain` never persists World; `ORLEANS_STORAGE≠memory` silently registers no storage; `AgentRunnerGrain` off-scheduler loop; grain-scheduler fallback deadlock |
| Medium | 8 | `[Reentrant]` RMW races; sync-over-async grain call; SignalR backplane never wired; event-instance leaks; unbounded telemetry; two state classes missing `[GenerateSerializer]`; shared global clock; volatile curriculum state |
| Low | 5 | Controllers 500 under `DISABLE_ORLEANS`; `Console.WriteLine` instead of `ILogger`; O(all-worlds) invite scan; volatile "stateless" registries; … |

## Critical

**The co-hosting DI bridge is self-referential — the server cannot boot with Orleans enabled.** *Verified, including by live boot test (2026-07-03): `dotnet run` with Orleans enabled prints through "Using in-memory grain storage (development mode)" and then hangs indefinitely (no startup banner after 90s); the identical run with `DISABLE_ORLEANS=1` boots and prints the banner within seconds.* In Orleans 7+ co-hosting, `builder.Host.UseOrleans(...)` shares the host's `IServiceCollection` — there is no separate silo container. Every "bridge" registration at `Program.cs:276-348` therefore *re-registers* an already-present service, and MS.DI's last-wins rule makes the new descriptor authoritative. Several of those descriptors resolve themselves, e.g. `Program.cs:328-331`:

```csharp
siloBuilder.Services.AddSingleton<IGrainFactory>(sp => sp.GetRequiredService<IGrainFactory>());
```

and identically for `IClusterClient` (`:334-338`), the `IHubContext<>`s, and (via the shared root provider behind `IHost`) `HubWorldLoader`/`HubWorldGenerator`/`HubTemplateResolver` and the simulation singletons. Resolving such a service re-invokes the same factory → unbounded self-recursion. `Program.cs:383` resolves `IClusterClient` during startup, before `app.RunAsync()` — which is exactly where the observed boot hang occurs. The fix is to **delete lines 276-348 entirely** — co-hosted grains already see every host singleton because the container is shared.

Why nothing caught this: all grain tests use `Orleans.TestingHost.TestCluster` with their own silo configurators (never `Program.cs`), and the only `Program.cs` boot test (`GameHubSmokeTests`) sets `DISABLE_ORLEANS=1`. This also implies the documented `start-game-test.ps1` gameplay workflow cannot currently work against an Orleans-enabled server at HEAD. (Boot-test side observation: under `dotnet run`, `Properties/launchSettings.json` overrides the listen URLs to `https://localhost:50309;http://localhost:50310` — not the documented 5000 — another doc/config drift worth resolving.)

**`metaStore` storage provider is never registered.** *Verified.* `MetaProgressionGrain.cs:19` declares `[PersistentState("metaProgression","metaStore")]`, but `Program.cs:240-242` registers only `narrativeStore`/`worldStore`/`mapStore`. Every production activation fails; call sites in `GameHub` and `NarrativeStateGrain` swallow it (silent no-op), while `MetaProgressionController` (8 sites) and `ManagementHub` (6 sites) surface 500s. (Cross-confirmed by the narrative audit.)

## High (verified)

- **The world-tick pipeline is entirely undriven in production.** `WorldTickService` is registered but never `AddHostedService`'d (`Program.cs:150-164`), and even if started its `ExecuteAsync` is a delay-only loop with a TODO. Nothing else drives `IWorldGrain.TickAsync` — no timer, reminder, or endpoint (only tests call it). So weather, seasons, spawn modifiers, `BuilderModifier`, and both event schedulers are dead at runtime despite substantial implementation. (`BuilderModifier` is doubly dead: constructed with a null World at `Program.cs:139`, it early-returns.)
- **`GameMapGrain` never persists or restores its World.** `SerializedWorld=null` (TODO), reactivation yields a placeholder `new World()`, `GetWorldAsync` returns null. Any silo recycle replaces the generated map with an empty one → spawns fail, `JoinWorld` errors "no active map," ticks no-op. World "persistence" is metadata-only. (Cross-confirmed by the worldgen and narrative audits.)
- **`ORLEANS_STORAGE≠memory` silently registers zero grain storage.** `Program.cs:235-243` has no `else` (the azure branch is commented out), so a non-`memory` value adds no storage and startup proceeds without error — every `[PersistentState]` activation then fails at runtime.
- **`AgentRunnerGrain` runs an unmanaged off-scheduler loop.** `RunAsync` spawns `Task.Run` calling `StepAsync()` from a thread-pool thread, racing Orleans-scheduled turns on grain state; survives deactivation poorly. Should use a grain timer. (Cross-confirmed by the agents audit.)
- **Grain-scheduler fallback path can deadlock.** *Suspected.* When the `IEventScheduler` service is absent, `MapRegionGrain.TickAsync` calls back into its non-reentrant parent `GameMapGrain.GetWorldAsync()` while that grain is blocked awaiting the regions' ticks — a call-cycle that times out (~30s) each tick. Masked today because the service is always registered.

## Medium (verified)

- **`[Reentrant]` WorldGrain with check-then-act across awaits** — `AddPlayerAsync` checks capacity then awaits then increments (interleaved joins exceed MaxPlayers); double `RemovePlayerAsync` decrements twice; `WriteStateAsync` can race a mutating turn ("collection modified"). Reentrancy appears needed only because `GameMapGrain.InitializeAsync` calls back into the initializing WorldGrain — a real cycle worth restructuring.
- **Blocking sync-over-async grain call** — `OrleansWorldHost.cs:67` `.Wait()` on `SetAclAsync` blocks the activation turn thread; inconsistent with surrounding `await`s.
- **SignalR Orleans backplane never actually configured** — the package is imported and a comment claims auto-config, but neither `AddOrleans()` on the SignalR builder nor `AddSignalRBackplane()` on the silo is called; `signalRBuilder` is unused. Inert (moot at single-silo, but the code claims otherwise).
- **Event-instance lifecycle unreachable and leaks** — `UpdateAsync`/`CompleteAsync` have no production callers; `ActiveEventInstances` grows unpruned; `BroadcastToAreaAsync` doesn't broadcast; `SpawnControllerGrain.DespawnEntitiesAsync` doesn't despawn (TODO) → event monsters persist forever.
- **`AgentTelemetryGrain` grows without bound** — `_snapshots` appended per step, never trimmed; all telemetry volatile. (Cross-confirmed by the agents audit.)
- **`WorldGrainState` and `ClusterState` lack `[GenerateSerializer]`** unlike all 17 other persisted state types — one storage-serializer change (e.g. the planned Azure path) from breaking; the inconsistency is unintentional.
- **Shared singleton `WorldClock` across all worlds** — `WorldGrain.TickAsync` advances a process-global clock, so with >1 world each tick multiply-advances global time; also silently fabricates a default clock in production if resolution fails. (Relates to the simulation audit's two-clocks finding.)
- **`CurriculumProgressionGrain` state is volatile + mutable static** — no `[PersistentState]`; curriculum definitions live in a private static populated only by test reflection, so `StartCurriculumAsync` silently no-ops in production.

## Low (verified)

Grain-backed controllers hard-500 under `DISABLE_ORLEANS=1` (hubs degrade gracefully; controllers don't); pervasive `Console.WriteLine` instead of `ILogger`, several empty `catch {}` blocks; `OrleansWorldHost.AcceptInviteAsync` fans out over all worlds per invite; `GameManagementGrain._worldRegistry` is volatile in an otherwise-stateless grain, giving inconsistent post-deactivation recovery.

## Verified leads (from the brief)

1. **Confirmed** — `EventScheduler` (host service) and `EventSchedulerGrain` duplicate the same API; the service is used (via `MapRegionGrain`), the richer persisted grain is unreachable fallback — and doubly dead because the fallback needs `GetWorldAsync` (which returns null) and because region ticks never run.
2. **Confirmed** — `WorldTickService` never started; the whole tick pipeline is undriven (see High).
3. **Confirmed** — `BuilderModifier(builderAI, null)` early-returns; dead code inside a dead pipeline.
4. **Confirmed (generalizes)** — the self-referential `IGrainFactory` registration is one instance of the whole `276-348` bridge being self-referential (Critical finding).
5. **Confirmed** — `ORLEANS_STORAGE=azure` silently yields no storage (High).
6. **Confirmed** — `metaStore` is the one unregistered store; the other 18 `[PersistentState]` usages map to registered stores. (Test drift: `ClusterGrainTests` registers an unused `clusterStore`; `GameManagementGrainTests` an unused `management`.)
7. **Confirmed** — `AgentGrain.Join/Leave/UpdatePrompt` are still stubs; the real loop is in `AgentRunnerGrain`, making `AgentGrain` vestigial.

## Strengths

- Consistent grain idiom (injected `[PersistentState]`, `OnActivate` init, `WriteState` on mutation); deterministic composite keys enabling address-by-convention.
- Nearly all state/DTO types carry `[GenerateSerializer]`/`[Id]` (the two exceptions above are the anomaly).
- `GameManagementGrain` implements the spec's error contract faithfully (exact reason strings, `ConcurrentDictionary` indexes).
- Graceful Orleans-off degradation in the SignalR layer (optional `IClusterClient`, null-returning grain accessors, try/catch around register/unregister).
- `ClusterGrain` manages its economy-timer lifecycle correctly; several grains `DeactivateOnIdle` on completion; write-throttling is deliberate (region persists ≤ every 5 min; WorldGrain/DungeonInstance skip per-tick writes; narrative event log capped at 1000).
- Solid TestingHost integration discipline for the core grains with realistic silo configurators.
- The instance/lockout actor decomposition (allocator → ledger → instance, with party/player reuse and stale-mapping cleanup) is clean.

## Spec alignment (`game-management-grain/spec.md`)

- **Terminate session (`:167-173`) — deviates**: only removes from the session manager; does not disconnect the SignalR connection as required.
- **Batch operations (`:191-204`) — missing**: no `SetAllSessionsVisionModeAsync` or any batch method exists.
- **Invalid-parameter validation (`:223-227`) — partial**: control methods return `OperationResult.Error`, but `Register/UnregisterSessionAsync` throw `ArgumentNullException` rather than failing gracefully.
- **AgentCLI integration (`:235-265`) — deviates in mechanism**: no AgentCLI project; `aetherctl` reaches management via the SignalR `ManagementHub`, not a direct `IGameManagementGrain` handle.
- **Orleans-disabled mode (`:229-233`) — partial**: hub registration is a safe no-op (compliant); grain-backed controllers 500 with no "Orleans disabled" messaging; and with Orleans *enabled* the DI defect means the grain is unreachable anyway.
- **Aligned**: vision/FOV/lighting/vision-mode requirements (exact reason strings, 1–360 validation, `Enum.IsDefined`), GLOBAL singleton key usage, stateless session index with reconnect repopulation. (Spec's Purpose section is still "TBD".)

## Test coverage

13 of 27 grains have direct tests (World/GameMap/MapRegion/Cluster/MetaProgression/Curriculum/AgentTelemetry/Narrative/EventInstance/SpawnController/PromptRegistry/GameManagement/AgentRunner-broadcast) — but even the tested ones have big gaps (GameManagementGrain's vision/lighting/time-scale/tool-execution/ACL methods are untested). **14 grains have zero tests**, including the entire Instances/Groups domain (5 grains), NarrativeStateGrain's quest state machine, and every directory/ACL/invite grain. **Structural gaps that hid the top findings:** no test boots `Program.cs` with Orleans enabled (would have caught the DI defect), none exercises the `ORLEANS_STORAGE` branch, and none drives the tick pipeline from a hosted service.
