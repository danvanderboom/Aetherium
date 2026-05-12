# Design — Grain-Authoritative Mutation with SignalR Delta Fan-Out

## Context

Phase 1 established the hub→grain world snapshot bridge: sessions hydrate from a grain-served snapshot but each owns an independent `World` instance and mutations stay local. Phase 2a (`add-map-mutation-gateway`, prerequisite for this change) introduced `IMapMutationGateway` so gameplay tools no longer reach into session state directly. This phase delivers the headline change: the grain becomes the single source of truth for state, mutations propagate to all sessions in the same map via SignalR groups, and players actually see each other's actions in real time.

Phase 3 (later, separate change) will eliminate the session's local `World` mirror entirely and serve perception from the grain. That's not in scope here; phase 2 keeps the mirror for perception computation but makes it converge to the grain via deltas.

## Goals / Non-Goals

### Goals
- Mutations in any session route through the grain. The grain is the canonical authority.
- After a mutation, every session in the same map sees a corresponding delta and applies it to its local mirror within milliseconds.
- Two clients joining the same world see each other's `Character` entities and watch them move in real time.
- 714 existing tests pass unchanged. The session-local `MoveView`/`TryPickup` path stays alive for legacy/test mode.
- Delta DTOs are designed for forward compatibility — adding new mutation types is mechanical.

### Non-Goals
- Eliminate the session's local `World` mirror. Phase 3.
- Compute perception in the grain. Phase 3.
- FOV-filtered delta fan-out (clients receive deltas even for cells they can't see, then filter at perception time). Phase 2.1, separate change.
- Real `World` serialization for silo-restart persistence. Out of scope; the recipe-based reactivation from phase 1 still covers initial state.
- Cross-silo player presence. Single-silo only; the SignalR group abstraction is silo-scoped.
- Replace agent-runner-side gameplay (which today calls `session.MoveView` directly through tools). Phase 2a's gateway abstraction already handled this; this phase just makes the gateway grain-routed.

## Decisions

### D1: Grain methods are typed-per-verb, not a polymorphic ExecuteTool

**Decision**: `IGameMapGrain` gets one method per gameplay verb: `MoveAsync`, `RotateAsync`, `PickupAsync`, etc. Each is typed, takes `sessionId` for player lookup, and returns a typed result DTO. The agent tool registry sits in front of these (in the hub and the agent runner) and dispatches based on toolId.

**Alternatives**:
- *Single `ExecuteToolAsync(sessionId, toolId, args)` on the grain*. Mirrors what the hub already does. Smaller grain surface, but tools execute *inside* the grain, requiring the tool's `ToolExecutionContext` to be reconstructed there. The agent tool system's `LocalMutationGateway` path would have to be different from the grain path, doubling the dispatch logic.

**Rationale**: typed methods give us Orleans-native serialization for free, are easier to authorize per-method, and let the gateway abstraction stay symmetric — `LocalMutationGateway` and `GrainMutationGateway` both implement the same interface, one in-process and one over RPC. Tools don't care which is which.

### D2: Mutations route through `IMapMutationGateway`, which routes to the grain

**Decision**: `GameHub.JoinWorld`, after a successful map join, replaces the session's gateway with a `GrainMutationGateway`. The gateway holds a reference to `IGameMapGrain` and translates each method call into a grain RPC. Sessions in legacy mode (no `?worldId=`) keep `LocalMutationGateway` and behave exactly as before.

**Alternatives**:
- *GameHub directly calls grain methods*. Bypasses the gateway. Means tools have to know whether they're in grain-bound mode or legacy mode. Defeats the abstraction.

**Rationale**: phase 2a designed the gateway boundary precisely to enable this swap. Honoring it keeps the tool surface uniform.

### D3: Deltas via SignalR groups, keyed by mapId

**Decision**: every session bound to map `M` joins the SignalR group `map:M`. The grain pushes deltas via `hubContext.Clients.Group("map:" + mapId).SendAsync("ApplyDelta", delta)`. Clients receive `ApplyDelta(delta)` and reconcile their local mirror.

**Alternatives**:
- *Orleans Streams*. Native to the grain world. But requires every client to be an Orleans client (extra dependency, not how SignalR clients work today).
- *Direct per-client SendAsync*. Grain enumerates `_mapState.PlayerIds`, sends to each connection. Doesn't scale as well as group-fan-out; SignalR's group implementation is optimized for this case.

**Rationale**: SignalR groups already exist, already work with the `UFX.Orleans.SignalRBackplane` package the project uses for distributed scaling, and don't add a new dependency or protocol.

### D4: Delta types are per-event, not per-state-diff

**Decision**: define a small set of typed delta DTOs (`EntityAddedDelta`, `EntityRemovedDelta`, `EntityMovedDelta`, `DoorStateChangedDelta`, `ItemTransferredDelta`). The grain emits them as `_world.WorldEvents` fires; clients apply them sequentially.

**Alternatives**:
- *Full perception resend*. After every mutation, recompute every player's perception and push the new `PerceptionDto`. Simple — sessions don't need an `ApplyDelta` method. But with N players and M mutations, perception cost is O(NM); deltas are O(N) per mutation.
- *State-diff snapshots*. Compute a diff between consecutive snapshots. More complex; redundant with the per-event approach.

**Rationale**: per-event deltas are bandwidth- and CPU-cheap, well-suited to SignalR's message-per-call model, and the grain's `_world.WorldEvents` action stream is already shaped for this. Periodic full perception resync (as a recovery mechanism if a session falls behind) is a future addition, not phase 2's core.

### D5: Coarse fan-out now, FOV-filtered later

**Decision**: phase 2c sends every delta to every session in the map regardless of FOV. The session's perception layer filters when rendering, so the player still only *sees* what they should see, but the wire payload includes cells outside their FOV.

**Alternatives**:
- *Grain-side FOV filter*. Grain inspects each session's view location before sending; skips deltas the player can't see. Better privacy (no out-of-FOV info on the wire), more work.

**Rationale**: shipping coarse fan-out first means we can prove live shared mutation works end-to-end before optimizing. The FOV filter is mechanical to add once the infrastructure is in place — separate, smaller change.

### D6: Player Character entities are grain-owned

**Decision**: when `JoinPlayerAsync` succeeds, the grain creates a `Character` entity in `_world` for the joining player (using the `playerId` as the entity ID). The session's hydrated mirror gets this character via the snapshot (the previous filter that excluded `Character` from snapshots is removed). Player movement deltas (`EntityMovedDelta`) propagate to all sessions.

**Alternatives**:
- *Keep Character entities session-local*. Players don't appear in each other's views. Defeats half of what "multiplayer" means.

**Rationale**: live shared mutation requires shared visibility of who's moving. Snapshots already carry entity placements with grain-assigned IDs (phase 1's design); adding `Character` to the included types is a small extension.

### D8: Character heading is server-authoritative on the Character entity

**Decision**: `HeadingDegrees` is a per-`Character` property, stored on the entity's `HasHeading` component, owned by the grain. Rotation mutations (`RotateAsync`) update the component in `_world` and emit a delta. Sessions no longer treat heading as session-local state; the session reads its player's heading from the local `World` mirror.

This honors the perception-pure design principle: a character's facing direction is *objective reality of the world* (the character does face some direction), and whether observers can perceive it is a separate perception-filter question. The default rule for phase 2: perception of *other* characters omits their heading (we don't leak which way someone is facing through the wire). A future change can add the "wielding a compass reveals nearby characters' headings" rule as a perception filter.

**Alternatives**:
- *Heading stays session-local*. Each session owns its own player's heading. Doesn't leak via deltas but corrupts the perception-pure model — heading becomes a client preference rather than a property of the character.
- *Heading on Character but also leaked freely via deltas*. Means every player sees every other player's facing direction at all times. Breaks the "information requires a perception mechanism" rule.

**Rationale**: keeps the engine perception-pure. Lighting/vision-mode preferences (`CurrentLightingMode`, `CurrentVisionMode`) remain session-local because they're per-observer rendering filters, not properties of any character in the world.

### D9: HeatTrailTracker is grain-side; sessions hold a delta-driven mirror

**Decision**: `HeatTrailTracker` is owned by `GameMapGrain`. Heat is *objective reality* — a recently-walked cell is hot regardless of who's looking. Tick and movement mutations record heat in the grain's tracker. Heat changes propagate as new delta types (`HeatRecordedDelta`, `HeatExpiredDelta`) via the same SignalR group fan-out as other deltas. Sessions maintain a local `HeatTrailTracker` mirror that converges to the grain's via deltas.

What stays per-session is *interpretation*: `VisionMode.Infrared` reveals trails the eye can't see in `VisionMode.Normal`. That's a perception-filter on the same underlying heat data.

**Alternatives**:
- *Keep HeatTrailTracker session-local*. Simpler in the short term but each session computes heat independently, so two players see different "objective" heat trails. Wrong shape for an authoritative-world game engine.
- *Grain owns heat, sessions fetch on perception*. Per-perception RPC to the grain. Hot path; would add tens of milliseconds to every perception update.

**Rationale**: heat-as-objective-reality matches the design philosophy. Delta-based replication keeps the cost amortized across actual heat changes rather than per perception.

### D7: Authorization remains at the hub boundary

**Decision**: `GameHub.ExecuteTool` continues to validate the Player profile (or whatever profile the connection has) before dispatching. Grain methods trust their callers. The grain is process-internal and only callable from authenticated code paths.

**Alternatives**:
- *Per-method auth in the grain*. Defense in depth. Every method takes an auth context arg.

**Rationale**: doubling the auth surface is overkill for an in-process grain. Revisit if grain methods ever get exposed to untrusted code (which they shouldn't).

## Risks / Trade-offs

- **State drift if deltas are dropped**: a network blip during a SignalR session could cause a session to miss a delta and diverge from the grain. Mitigation: periodic full perception resync (e.g. every 10s) as a recovery mechanism, plus an explicit `ResyncSnapshot` hub method clients can call. Phase 2c ships the coarse mechanism; the resync is a follow-up.
- **Mutation logic temporarily duplicated** between `InteractionSystem` (used by `LocalMutationGateway`) and `GameMapGrain` (used by `GrainMutationGateway`). Bug fixes need to land in both places until `remove-legacy-mutation-paths` (phase 2d) ships. Mitigation: keep phases 2b+c and phase 2d's timelines tight; or have the grain methods *delegate* to `InteractionSystem` internally so there's only one implementation. The latter is cleaner — InteractionSystem becomes stateless (takes a `World` arg instead of a `Session`), grain methods construct a temporary `InteractionSystem`-equivalent context around `_world`. Worth doing during 2b+c.
- **Player Character ID collision**: today every `Character` constructor produces a fresh `Guid.NewGuid()` `EntityId`. Phase 2's player Character needs `EntityId == playerId` so deltas about that player route correctly. Adding `Character(string entityId)` constructor or post-construction `OverrideId(...)` is the small API change required. Same shape as the entity-id override we already did in `EntityFactory`.
- **Grain method auth gap**: if any code path bypasses `GameHub.ExecuteTool`'s profile check (e.g. internal grain-to-grain calls, the agent runner), it gets unchecked grain access. The agent runner currently uses `IGameManagementGrain` for action dispatch — that grain has its own auth model. Phase 2 should audit grain-to-grain auth boundaries. Mitigation: add a defense-in-depth permission check at the grain level for high-privilege operations (probably just `world_edit` capability; movement and inventory are low-stakes).
- **SignalR group cleanup on disconnect**: SignalR removes connections from groups automatically on disconnect, but the `_mapState.PlayerIds` set is grain-owned and must be cleared explicitly. The phase 1 `RemovePlayerAsync` already handles this; ensure `OnDisconnectedAsync` invokes it.
- **WorldEvents subscriber exceptions**: the grain subscribes to its own world's event action. If translation to a delta throws, we don't want to fail the mutation. Mitigation: wrap the subscriber in try/catch; log; continue.

## Migration Plan

This change is shippable as a single PR but is large (estimated 1500-2500 lines including tests). Suggested in-PR ordering:

1. **DTOs first**: ship `EntityAddedDelta`, `EntityRemovedDelta`, etc. in `Aetherium.Server.MultiWorld` or `Aetherium.Model.Deltas`. No behavior dependency.
2. **Stateless InteractionSystem refactor**: make `InteractionSystem` methods take `(World, Character, ...)` instead of `(GameSession, ...)`. This is the change that lets `LocalMutationGateway` and the grain methods share one implementation. Bug fixes consolidate.
3. **Grain mutation methods**: add to `IGameMapGrain`, implement in `GameMapGrain` by delegating to `InteractionSystem` operating on `_world`.
4. **Grain event subscription**: hook `_world.WorldEvents` → translate to deltas → fan out via injected `IHubContext<GameHub>`.
5. **GrainMutationGateway**: implement the gateway interface by calling grain methods.
6. **JoinWorld swap**: after a successful join, replace the session's gateway with `GrainMutationGateway`.
7. **Session.ApplyDelta**: client-side reconciliation. Tested against each delta type.
8. **SignalR group lifecycle**: join on `JoinWorld`, leave on disconnect.
9. **WorldSnapshot adjustments**: stop filtering `Character`; teach `EntityFactory` to construct a `Character` with a specified ID.
10. **End-to-end test**: two SignalR clients, one mutation, observe delta arrival.

Rollback: each step above can be reverted independently. The riskiest is step 9 (the snapshot shape changes), which we'd need to follow carefully if compatibility with stored snapshots is ever a concern (currently snapshots aren't persisted, so this is moot for phase 2).

## Resolved Decisions (was Open Questions)

1. **Character heading/vision/lighting state.** RESOLVED: heading moves to `Character` entity (server-authoritative), per D8. Lighting and vision modes remain session-local because they're per-observer rendering preferences, not character properties.

2. **Player and inventory persistence across sessions.** DEFERRED to a separate later change. Each map will eventually decide its own policy: player remains visible at position, disappears until reconnect, or is repositioned on rejoin with "same location" available as an option. Implementing this requires (a) a player-identity story (auth/B2C-claim-keyed `IPlayerGrain`), (b) snapshot-on-disconnect, (c) restore-on-reconnect with placement policy. All meaningful infrastructure that doesn't fit in phase 2's "live shared mutation" scope. Tracked as a future `add-player-persistence` change. For phase 2: on disconnect, drop the player Character from `_world` and emit `EntityRemovedDelta`; on rejoin, a fresh player is created.

3. **Delta ordering guarantees.** Orleans serializes grain methods, so the grain emits deltas in a single linear order. SignalR per-connection ordering is FIFO within a hub method invocation. ACTION ITEM (task 10.x): write a test that asserts a sequence of N mutations from one session arrives in the same order at a second session. If this turns out to be flaky, fall back to attaching a monotonic sequence number to each delta and having clients reorder.

4. **Heat trail tracking.** RESOLVED: heat trails are grain-authoritative, per D9. Sessions maintain a delta-driven mirror. Adds `HeatRecordedDelta` / `HeatExpiredDelta` to the delta set.

5. **Compatibility with the agent runner.** RESOLVED: `IGameManagementGrain` forwards to `IGameMapGrain`. The existing agent-runner → management-grain → map-grain chain is preserved. No agent-side change in phase 2.
