# Design — Hub-to-Grain World Snapshot Bridge

## Context

The Aetherium server has two parallel `World` ownership models. `GameSession.World` is built fresh from `FovDiagnosticWorldBuilder` per connection ([GameHub.cs:80-86](../../../Aetherium.Server/GameHub.cs)) and is the only world the gameplay loop touches. `GameMapGrain._world` is built by the worldgen orchestrator inside an Orleans grain and is the world all cluster, portal, narrative, and economy systems assume exists. The two never meet. `GameHub.JoinWorld` is a stub that admits this with `OperationResult.Error("Joining worlds via GameHub is not yet supported.")`.

The broader architectural plan ("Path C — phased Path A") is to migrate to a model where `GameMapGrain` is the canonical owner of `World` and `GameSession` is a thin client-state holder. That migration runs in three phases:

- **Phase 1 (this change)**: hub asks the grain for a deterministic snapshot on `JoinWorld`; session hydrates a local equivalent. Two joiners see identical layouts and identical entity IDs, but each session still owns its own `World` instance and mutations are independent.
- **Phase 2 (future)**: mutation routes through grain methods (`PickupAsync`, `MoveAsync`, etc.). Sessions become observers receiving deltas. The grain is authoritative.
- **Phase 3 (future)**: session's local `World` mirror is eliminated; perception is computed inside the grain. Sessions can live on different silos from their worlds.

Phase 1 builds the hub→grain bridge with the minimum DTOs and call path that the later phases extend.

## Goals / Non-Goals

### Goals
- `GameHub.JoinWorld(worldId, mapId?)` produces a working session bound to a grain-owned world.
- Two clients joining the same `worldId` see identical initial perception (same terrain, same entities, same entity IDs).
- The 703 existing tests pass unmodified.
- Ad-hoc connections without `?worldId=` keep working via the legacy `FovDiagnosticWorldBuilder` path.
- DTOs (`WorldSnapshot`, `EntityPlacement`, `JoinMapResult`) carry forward into phase 2 — phase 1's persistence shape is the lasting investment.

### Non-Goals
- Live shared mutation between joiners. Two clients picking up the same item each get a copy. Phase 2 problem.
- World persistence across silo restart. `MapState.SerializedWorld` stays null. The grain reactivates from its captured `WorldRecipe`.
- Cross-silo player presence. Same single-silo `UseLocalhostClustering` configuration.
- Migrating any existing test off `new GameSession("test", new FovDiagnosticWorldBuilder(...))`.
- Real `World` serialization. Phase 1 uses deterministic regeneration from a recipe plus a list of entity placements. A full serializer can come later.

## Decisions

### D1: Snapshot = recipe + entity placements, not serialized World

**Decision**: A `WorldSnapshot` carries a `WorldRecipe` (generator key, seed, parameters) and a list of `EntityPlacement` records (id, type name, location, key properties). The session regenerates terrain by replaying the recipe and overlays entities by ID.

**Alternatives considered**:
- *Full Orleans-serialized `World`*. Cleanest semantically but requires `[GenerateSerializer]` on `World`, `Entity`, every `Component`, and survives a `ConcurrentDictionary` round-trip. Significant up-front work that we don't need for phase 1's "identical layout, independent mutation" semantics.
- *Return the live `World` by reference from the grain*. Path B. Bypasses Orleans isolation, locks us to single-silo forever, creates two-owner state forever. Rejected.

**Rationale**: The recipe approach is small to ship, deterministic by construction, and the DTO shape is what phase 2 will extend (adding component state to `EntityPlacement`). No code we write here gets thrown away by phase 2.

### D2: Grain assigns authoritative entity IDs

**Decision**: When `GameMapGrain.InitializeAsync` generates the world, the entity IDs it assigns (currently `Guid.NewGuid().ToString()` in the `Entity` constructor) are the canonical IDs. The snapshot ships these IDs verbatim. `SnapshotWorldBuilder` overrides the local entity's `EntityId` to match the snapshot's value rather than letting the constructor generate fresh ones.

**Alternatives considered**:
- *Deterministic entity IDs from `(seed, position, type)`*. Would let session regenerate IDs without overlay data. But it's brittle — entity-spawn ordering would have to be stable across versions of every generator pass — and it doesn't generalize to phase 2 where entities will be created mid-game with no spawn ordering.

**Rationale**: Grain-as-id-authority is the model phase 2 needs anyway. Doing it now avoids a second migration.

### D3: Spawn locations are grain-assigned

**Decision**: `JoinPlayerAsync` picks a spawn location and adds it to a grain-local `HashSet<WorldLocation>` of in-use spawns. Two simultaneous joiners get distinct spawns.

**Alternatives considered**:
- *Session picks its own spawn from the local hydrated world*. Two joiners with the same RNG seed pick the same cell. Requires per-session seed injection plus collision detection across sessions. More moving parts than letting the single-threaded grain decide.

### D4: Hydration regenerates terrain via the worldgen orchestrator

**Decision**: `SnapshotWorldBuilder.Build()` calls the existing `WorldGenerationOrchestrator` with the recipe's generator, seed, and parameters. Output terrain is then overlaid with the snapshot's entity placements (entity IDs preserved).

**Alternatives considered**:
- *Ship terrain in the snapshot*. Means the snapshot carries every tile of every map — for a 100×100 world that's 10,000 cells. Bandwidth concern, but more importantly: terrain is identical across joiners by construction (deterministic recipe), so it's redundant.
- *Cache the regenerated `World` per recipe on the session manager*. Optimization for "many joiners to the same world." Skip until profiling shows it matters.

### D5: Legacy `OnConnectedAsync` path is preserved via query-string gate

**Decision**: `OnConnectedAsync` still creates a private `FovDiagnosticWorldBuilder("open_space")` world by default. If the SignalR connection query string includes `?worldId=<id>`, it additionally auto-calls `JoinWorld(worldId)` after the initial session setup, replacing the private world.

**Alternatives considered**:
- *Make `JoinWorld` mandatory* — fail the connection if no `worldId` is supplied. Cleaner but breaks every existing test setup and ad-hoc dev run.
- *Keep `OnConnectedAsync` as-is and require explicit `JoinWorld` call after connect*. Doable but adds a round trip on every real-world join. Auto-join via query param matches typical SignalR client patterns.

### D6: New change touches only `client-server-communication` spec

**Decision**: The grain-side methods (`GetWorldSnapshotAsync`, `JoinPlayerAsync`) are added as ADDED requirements within the `client-server-communication` spec, framed as the contract between hub and grain. No new capability spec.

**Alternatives considered**:
- *Create a new `world-hosting` or `game-map-grain` capability*. There's no current spec for `IGameMapGrain` at all — `openspec list --specs` shows only `game-management-grain` (a different grain). Creating a brand-new capability spec for this slice is overkill when the requirement is functionally "the hub can bridge to a grain to share worlds."

**Rationale**: Keeps the change reviewable. A `game-map-grain` capability spec can be authored separately when phase 2 expands the grain's gameplay surface (and there's much more to specify).

## Risks / Trade-offs

- **Generator non-determinism**: some generation passes use `Guid.NewGuid()` (e.g., `AdaptiveQuestGenerator`, `AdaptiveNarrativeGenerator`). For phase 1 the snapshot ships the grain's chosen entity IDs verbatim, so non-determinism inside the grain's generation is invisible to joiners — the grain decides once, the snapshot replays the decision. Risk only surfaces if generation re-runs in a different ordering. Mitigated by sourcing IDs from the snapshot rather than re-rolling.
- **`Entity.EntityId` mutability**: the `Entity` base class currently assigns `EntityId = Guid.NewGuid().ToString()` in its constructor and exposes it as a read-only property. Phase 1 needs to override it during hydration. The cleanest fix is to expose a constructor overload `protected Entity(string entityId)` or an internal `OverrideEntityId(string)` method used only by `SnapshotWorldBuilder`. Either is small but touches a base class — worth confirming the surface in code review.
- **Entity factory mapping rots**: when someone adds a new `Entity` subclass under `Aetherium.Server/Entities/`, they need to also register a factory mapping in `SnapshotWorldBuilder`. Mitigation: `EntityFactoryCompletenessTests` (task 6.2) reflection-scans the assembly and asserts every concrete subclass has a mapping. CI fails on missing entries.
- **Phase 1 ships, someone mistakes "shared layout" for "shared state"**: a player pickups an item and assumes other players see it disappear. Documented in `JoinWorldResult` summary, `WorldSnapshot` summary, the `CLIENT_SERVER_README.md`, and the `PHASE 1` comment in `GameSession.ReplaceWorld`. Phase 2 fixes the semantics; phase 1 is honest about the limitation.
- **Snapshot regeneration cost**: each joiner pays the full generator cost on hydration. For a 100×100 dungeon this is single-digit milliseconds; for larger worlds or generators with expensive passes (Perlin noise, river carving) it could approach seconds. Mitigation: an optional session-manager cache keyed by `(generatorType, seed, parameters)` — deferred until profiling demonstrates the need.
- **Orleans `[GenerateSerializer]` audit**: `WorldLocation` and `WorldSize` are referenced by the new DTOs. If they aren't already annotated, the grain call will fail at runtime with an obscure serialization error. Task 1.4 forces the audit up front.

## Migration Plan

Phase 1 is implemented in a single PR. Rollback is per-file:

- The new DTOs and `SnapshotWorldBuilder` / `WorldSnapshotBuilder` live in dedicated files — delete to revert.
- `IGameMapGrain.GetWorldSnapshotAsync` and `JoinPlayerAsync` are additive — remove the interface methods and grain implementations.
- `GameHub.JoinWorld` reverts to the existing stub.
- `OnConnectedAsync` query-param fork is a guarded block — remove it and behaviour returns to today's.
- `GameSession.ReplaceWorld` and `GameSessionManager.ReplaceSessionWorld` are additive methods — remove them.

No destructive changes to existing types except `Entity.EntityId` mutation surface (D2 risk). That surface should be tightly scoped to internal use by `SnapshotWorldBuilder`.

## Open Questions

- **Multi-map worlds**: today every world has exactly one map (the management grain creates one map per world). The hub's `JoinWorld(worldId, mapId?)` accepts an optional `mapId` for future multi-map worlds. Should phase 1 reject `mapId != null` for now to keep the contract honest, or accept and use whatever's there? Lean toward accepting — it's the same call.
- **What happens on disconnect during phase 1**: should the grain auto-remove the player from `_mapState.PlayerIds`, or wait for an explicit `LeaveWorld` from `OnDisconnectedAsync`? Currently `OnDisconnectedAsync` calls `UnregisterSessionAsync` on the management grain but doesn't touch the map grain. For phase 1, hook map-grain leave into the disconnect path — small addition, prevents stale `PlayerIds`.
- **Snapshot freshness**: if entities are modified after `InitializeAsync` (a door is opened, an item is destroyed), the phase 1 snapshot doesn't reflect that — entity placements come from the live `_world` at snapshot-call time, but the recipe-regenerated terrain doesn't know about it. Concretely: doors and items don't currently get mutated outside the `_world` Entity table, so this should be fine. Worth verifying with a test that opens a door in the grain and re-snapshots.
