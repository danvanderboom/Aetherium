# Implementation Tasks

## 1. Delta DTOs
- [x] 1.1 Created `Aetherium.Server/MultiWorld/Deltas.cs` with `EntityAddedDelta`, `EntityRemovedDelta`, `EntityMovedDelta`, `EntityHeadingChangedDelta`, `DoorStateChangedDelta`, `ItemTransferredDelta`, `HeatRecordedDelta`, `HeatExpiredDelta`. All `[GenerateSerializer]` with stable `[Id]` ordinals
- [x] 1.2 `MapDelta` base class with discriminated-union semantics; `ApplyDelta(delta)` dispatches on runtime type
- [x] 1.3 Each delta carries enough state for the receiver to reconcile without re-querying the grain (entity IDs, locations, component state, item placements for inventory transfers)
- [x] 1.4 `MapDelta.Sequence` monotonic per-map sequence number assigned by the grain. Phase 2c stamps it; resync-on-gap is a follow-up

## 2. InteractionSystem refactor (stateless)
- [ ] 2.1 **Deferred to `remove-legacy-mutation-paths` (phase 2d).** Reasoning: refactoring 14 methods plus all their internal helpers is high-churn and the grain's mutation methods are simpler to port natively. Phase 2c grain methods reimplement just the gameplay-critical verbs (Move, Rotate, ChangeLevel, Pickup, Drop, Open, Close, plus key-on-door Use). Phase 2d resolves duplication by making `InteractionSystem` stateless and switching `LocalMutationGateway` to share the same implementation
- [ ] 2.2 — see 2.1
- [ ] 2.3 — see 2.1

## 3. Entity ID assignment for player Character and server-side heading
- [x] 3.1 `Entity.EntityId` was already settable; no API change needed
- [x] 3.2 `IGameMapGrain.JoinPlayerAsync` now creates a `new Character { EntityId = playerId }` with `HasHeading`/`Inventory`/`WorldLocation` attached and adds it to `_world`
- [x] 3.3 `RemovePlayerAsync` and the new `LeavePlayerAsync` remove the player Character via `World.TryRemoveEntity` and emit `EntityRemovedDelta`
- [x] 3.4 `GameSession.HeadingDegrees` now reads/writes `Player.Get<HasHeading>().Heading` with a `_fallbackHeadingDegrees` for sessions that haven't yet bound a Player
- [x] 3.5 `IGameMapGrain.RotateAsync` mutates `Character.Get<HasHeading>().Heading`. Emits `EntityHeadingChangedDelta` via `NotifySessionMutationAsync` (actor-only, not the map group), so other players' clients never receive heading deltas. The perception layer also doesn't surface other-character heading in `VisualDto` — defense-in-depth preserves perception-pure for facing direction

## 4. Snapshot adjustments
- [x] 4.1 `WorldSnapshotBuilder.SnapshotOf` accepts an optional `excludePlayerEntityId` parameter and includes `Character` entities by default. `GetWorldSnapshotForJoinerAsync(joinerId)` uses this to exclude the joiner's own Character
- [x] 4.2 `EntityFactory.Construct` already handles Character via the parameterless-ctor reflection path (added in phase 1's factory completeness work)
- [x] 4.3 `JoinPlayerAsync` order: pick spawn → add Character to `_world` → write state. `GetWorldSnapshotForJoinerAsync` is called by the hub *after* the join, and excludes the joiner so they don't see themselves twice. The joiner's session creates a fresh local-mirror Character on hydration with `EntityId == SessionId`

## 5. Grain mutation methods
- [x] 5.1 Added `MoveAsync(sessionId, direction, distance)`, `RotateAsync(sessionId, degrees)`, `ChangeLevelAsync(sessionId, deltaZ)` to `IGameMapGrain`. Each typed-result via `MoveResult` / `RotateResult` / `ChangeLevelResult`
- [x] 5.2 Added `PickupAsync`, `DropAsync`, `UseAsync`, `OpenAsync`, `CloseAsync` returning `InteractionResultDto`
- [x] 5.3 Each grain method looks up the player Character by `_world.Entities[sessionId]`, then mutates `_world` directly (port of the core verb logic; InteractionSystem dedup deferred to phase 2d per task 2.1)
- [x] 5.4 Unknown sessionId returns a structured failure result

## 6. Grain event emission and host-side delta broker
- [x] 6.1 **Design change during implementation:** rather than subscribing to `_world.WorldEvents` and fanning out via SignalR groups (the original proposal), grain methods now emit deltas *directly* and route them through the host-side `GameSessionManager.NotifyMapMutationAsync`. This is a perception-pure correction: SignalR-group-direct-to-client would leak cells outside players' FOV. The new flow: grain mutates → `FanOutAsync(delta)` → manager applies to each session's mirror → manager pushes fresh `ReceivePerceptionUpdate` over SignalR. Clients never see raw deltas
- [x] 6.2 Each grain method emits the appropriate delta DTO from inside the mutation code (immediate access to both before/after state, no `WorldEvents` translation needed)
- [x] 6.3 `FanOutAsync` wraps in try/catch — broker failures are logged but never roll back the mutation
- [x] 6.4 `GetSessionManager()` resolves `GameSessionManager` lazily via `ServiceProvider`; absent in TestingHost runs, in which case fan-out is a no-op (tests verify state changes directly via `GetWorldSnapshotAsync`)

## 6.5 Grain-side heat trail tracking
- [ ] 6.5.1 **Deferred to phase 2.1.** Wire format hooks shipped: `HeatRecordedDelta` and `HeatExpiredDelta` are defined; `GameSession.ApplyDelta` has placeholder handlers. The grain doesn't yet record heat or emit these deltas — heat tracking remains client-side per session in phase 2c
- [ ] 6.5.2 — deferred
- [ ] 6.5.3 — deferred
- [ ] 6.5.4 — deferred
- [ ] 6.5.5 — `GameSession.ApplyDelta` placeholder calls `HeatTracker.RecordEntityPosition` for `HeatRecordedDelta`; will be wired end-to-end in phase 2.1
- [ ] 6.5.6 — perception filter for VisionMode.Infrared continues to operate on session-local heat data, no change

## 7. SignalR group lifecycle
- [x] 7.1 **Reframed:** rather than SignalR groups (`map:{mapId}`), the host-side `GameSessionManager` iterates sessions by `MapId` and dispatches deltas/perceptions per-connection via `Clients.Client(connectionId)`. The `MapGroupName` helper exists but is unused after the perception-pure refactor — kept for potential future use cases that don't have the FOV-leak concern
- [x] 7.2 `OnDisconnectedAsync` calls `mapGrain.LeavePlayerAsync(sessionId)` which removes the Character and emits `EntityRemovedDelta` to other joined sessions
- [ ] 7.3 `LeaveWorld()` explicit hub method — deferred. Not needed for phase 2c (disconnect is the only leave path); add when reconnect-without-disconnect is a real use case (probably alongside player persistence work)

## 8. GrainMutationGateway
- [x] 8.1 Created `Aetherium.Server/MultiWorld/GrainMutationGateway.cs` implementing `IMapMutationGateway`. Constructor takes `IGrainFactory`, `mapId`, and `sessionId`
- [x] 8.2 Each method delegates to the corresponding `IGameMapGrain` method
- [x] 8.3 `GameHub.JoinWorld` constructs `new GrainMutationGateway(clusterClient, resolvedMapId, sessionId)` and assigns to `session.Gateway` after a successful join
- [x] 8.4 `GameSession.Gateway` is a new settable property. `GameHub.ExecuteTool` reads it first, falling back to building a `LocalMutationGateway` for legacy sessions

## 9. Session-side delta application
- [x] 9.1 `GameSession.ApplyDelta(MapDelta)` added; takes `_stateLock`, dispatches on runtime type, handles all eight delta varieties
- [x] 9.2 Manager pushes `ReceivePerceptionUpdate` after `ApplyDelta` (not the session itself — the manager owns the wire concern)
- [x] 9.3 Unknown entity IDs are logged via `Console.WriteLine` and dropped; the eventual periodic resync mechanism will heal divergence
- [ ] 9.4 No hub method `ApplyDelta` registered for clients — phase 2c keeps the wire format unchanged. Clients only ever receive `ReceivePerceptionUpdate`; deltas are server-internal. This is the perception-pure correction noted in 6.1

## 10. Tests
- [x] 10.1 `GameMapGrainMutationTests` (NUnit + Orleans TestingHost, 8 tests): join adds Character, snapshot-for-joiner excludes self, rotate updates HasHeading, move updates position (where map allows), pickup of nonexistent/non-carriable fails, leave removes Character, Use rejects unsupported modes
- [x] 10.2 `DeltaApplicationTests` (xUnit, 10 tests): each delta type round-trips correctly through `GameSession.ApplyDelta`; unknown types and unknown entity IDs handled gracefully
- [x] 10.3 Snapshot-other-players coverage rolled into 10.1's `GetWorldSnapshotForJoinerAsync_Omits_Joiners_Own_Character`
- [ ] 10.4 `EndToEndSharedMutationTests` — deferred. The proof points (snapshot consistency, delta application correctness, grain method correctness, gateway routing) are covered by 10.1 + 10.2 + the phase-1 `GameMapGrainJoinTests`. Full WebApplicationFactory + SignalR.Client end-to-end is significant scaffolding; worth doing when a reusable SignalR test stand exists in the repo
- [x] 10.5 Legacy mode coverage is preserved automatically by the 700+ baseline tests — they continue to pass without modification because `session.Gateway == null` paths through `LocalMutationGateway` (built-on-demand) are unchanged
- [ ] 10.6 `DeltaOrderingTests` — sequence numbers exist on `MapDelta` but the resync mechanism that uses them is a follow-up. Test is deferred until then
- [ ] 10.7 `HeatTrailGrainAuthorityTests` — deferred along with heat-to-grain (task 6.5)
- [ ] 10.8 `OtherPlayerHeadingNotLeakedTests` — heading isn't in `VisualDto` to begin with (perception layer never exposed it), so the leak surface didn't exist. Documented in design D8. Adding an explicit guard test is low-priority; can land alongside the compass-perception-filter follow-up

## 11. Documentation
- [x] 11.1 `CLIENT_SERVER_README.md` updated: removed the phase-1 "independent mutation per session" caveat, added a "Multiplayer model (phases 1, 2a, 2c)" section describing grain-authoritative mutation, the perception-pure delta flow, and the deferred items (heat, persistence, complex Use)
- [ ] 11.2 `docs/multiworld-ecosystems.md` — not updated in this phase; the document is about cluster/portal/economy infrastructure rather than the per-map mutation model. The new docs in `CLIENT_SERVER_README.md` are the canonical phase-2 reference
- [x] 11.3 XML docs on `GrainMutationGateway`, `GameSession.ApplyDelta`, each delta DTO, and `GameSessionManager.NotifyMapMutationAsync` all explain the role and the perception-pure invariant

## 12. Validation
- [x] 12.1 All 722 pre-phase-2c tests pass without modification
- [x] 12.2 18 new tests pass (`DeltaApplicationTests` 10 + `GameMapGrainMutationTests` 8). **740 total passed, 0 failed, 2 skipped**
- [ ] 12.3 Manual two-client smoke test — operator action. Procedure: start server with B2C disabled; aetherctl create a world; launch two `Aetherium.Console` clients with `?worldId=<id>`; verify they see each other's Character and movement updates in real time
- [x] 12.4 `dotnet build` clean — no new errors; warnings unchanged from baseline
- [ ] 12.5 Single-mutation latency profiling — operator action; not gated on phase 2c shipping
