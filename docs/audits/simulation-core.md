# Audit: Simulation Core & ECS

*Audit date: 2026-07-03 · Scope: `Core` (World, Entity, Component, ContextEvaluator), `Components`, `Entities`, `InteractionSystem`, `Simulation`, `Geometry`, `Events`. Findings marked **Verified** or **Suspected**.*

> **Reconciliation — `develop` @ 2026-07-03.** The grain-authoritative mutation layer routes gameplay through `GameMapGrain` but **reuses the same `InteractionSystem`/`World` logic**, so it inherited the bugs rather than fixing them. **FIXED:** the `World.TryMove` West/East axis swap (`World.cs:287/289` now use correct per-axis deltas) — though `TryMove` remains dead code, so this is "one fewer landmine" rather than a behavior change. **PARTIAL:** the undriven tick pipeline is now driven (`WorldTickService` runs and calls `IWorldGrain.TickAsync` — weather/season/region ticks execute), but NPC behavior still doesn't run (`Monster.Heartbeat` is still never invoked), so monsters remain static. **Also fixed (Phase 1, commit `cd6cf67`):** `Memory.AddMemory` no longer writes to a discarded list copy — it now mutates the stored list directly (both the Server and Console copies), matching `RemoveMemory`; the entire NPC/agent memory feature is inert no longer. **FIXED IN PHASE 2 (`889878f` + hardening `253078a`) — the Critical/Major correctness gaps:** movement is now validated on **all three paths** via new `World.MovementBlocker`/`TryMoveSteps`/`TryChangeLevel` (the root cause was stark — `ObstructsMovement` was written by walls/doors/windows but **never read**, so nothing blocked movement anywhere); `GameMapGrain.MoveAsync` now validates and clamps distance to 1–100. Interactions are reach-checked (`ToggleDoor`/`TryUseWithMode`/lockpick/force/activate — including Z), closing the map-wide-unlock hole on every path. The inventory-capacity exploit is closed (`CapacityBoost.IsEquipped`). `MoveEntity`'s index-drop edge is fixed (idempotent destination write) and `RemoveEntity`'s throw-before-null-check plus the pickup-dupe were already fixed via the atomic `TryRemoveEntity`. Legacy-session cross-path races are serialized through `GameSession.WithStateLock`. **STILL STANDS:** NPC behavior still doesn't tick (`Monster.Heartbeat` never invoked, so monsters are static); `WorldClock`/`WeatherSystem`/scheduler are still not thread-safe (P1-13); and the three action paths, though now all correct, aren't yet converged to one owner (P1-4). Detail in the Reconciliation section at the end.

## Summary

This is the highest-risk subsystem in the audit. The ECS foundation is reasonable (O(1) location indexing, value-typed `WorldLocation` keys, a clean multi-use interaction model), but the movement and simulation layers contain **four verified Critical defects** — a wrong-axis movement bug, total absence of movement validation on the live player path, a memory feature that silently discards all data, and a "tick" service that runs no entity AI — plus a cluster of thread-safety and exploit issues. Many advertised systems (combat, stealth, traps, weather effects, NPC behavior) are inert. Crucially, **the actual player-movement path (`GameSession.MoveView`/`ChangeLevel`) has zero test coverage**, which is exactly where the Critical bugs live.

| Severity | Count | Items |
|---|---|---|
| Critical | 4 | West/East axis swap; movement bypasses all rules; `Memory.AddMemory` discards; no NPC ticking |
| Major | ~13 | usageId bypasses adjacency; open/close no range; capacity exploit; `MoveEntity` index drop; `RemoveEntity` throws/races; vertical movement impossible; weather/clock/scheduler not thread-safe; despawn stub / no spawn cap; two time systems; … |
| Moderate/Minor | many | bump-damage hits mover; no-op relative move; geometry bug; inconsistent events; inert Hidden/traps; hot-path allocations |

## Critical

**West/East movement is on the wrong axis.** *Verified.* `World.TryMove` maps `West → FromDelta(0,-1,0)` and `East → FromDelta(0,+1,0)` (`World.cs:280-283`) — identical to North/South. Moving West actually moves North; X-axis movement via `TryMove(WorldDirection)` is impossible. `GameSession.MoveView` and `Monster.GetValidDirections` both use the correct `(±1,0,0)`, proving `World.cs` is the outlier. (Currently masked because `TryMove` is dead for players — see next — but a live trap for anyone who wires it up.)

**All player movement bypasses movement rules.** *Verified — cross-confirmed by the protocol audit.* `GameSession.MoveView` (`GameSession.cs:220-255`) and `ChangeLevel` (`:307-318`) call `World.MoveEntity` directly with no passability, `ObstructsMovement`, `CanAscend/Descend`, or collision check. Every entry point (`GameHub.MovePlayer`, `GameManagementGrain.MoveAsync`, `MoveTool`) routes here. Players and agents walk through walls, closed doors, off-map, and between levels; the validated `World.TryMove` is dead code.

**`Memory.AddMemory` silently discards every new memory.** *Verified.* `Components/Memory.cs:35` copies the list (`.ToList()`), adds to the copy (`:49`), and throws it away; only the impression-increment path persists, and it's never reachable because nothing is ever stored. `Knows()`/`Knowledge()` are always empty — the entire NPC/agent memory feature is inert.

**No tick-based NPC behavior runs; `WorldTickService` is a no-op.** *Verified.* `WorldTickService.ExecuteAsync` is just `Task.Delay` with a TODO (`WorldTickService.cs:30-56`); nothing calls `Monster.Heartbeat` server-side; the manual tick chain (`WorldGrain.TickAsync → GameMapGrain → MapRegionGrain`) updates only weather/season/modifiers/events, never entity AI. Monsters are static decorations, making "SurviveTurns" benchmarks and the combat prompt unfalsifiable.

## Major (verified)

- **`TryUseWithMode` bypasses adjacency/context** (`InteractionSystem.cs:132-135`, unlock/lockpick/force-open branches `:256-302`) — passing a `usageId` skips the context filter; any matching door can be unlocked from any distance. (Same root cause as the protocol audit's map-wide-interaction finding.)
- **`TryOpen`/`TryClose` have no range check** (`ToggleDoor`, `:309-356`) — any door by ID, anywhere. `TryActivate` checks Manhattan distance but ignores Z (`:367-370`), allowing activation through floors.
- **Infinite inventory-capacity exploit** (`TryEquip`, `:638-644`) — `Capacity += AdditionalCapacity` with no equipped-state tracking; equip the same backpack N times for N×5 capacity. Equipping a cloak embeds a whole `Entity` as a player component, never removed/read (`:652`).
- **`MoveEntity` can drop entities out of the location index** (`World.cs:233-249`) — replaces `WorldLocation` before checking the destination bucket; if the bucket was pruned, the entity is added nowhere and no event fires. With `MoveView` targeting arbitrary tiles, a player can vanish from the index while remaining in `Entities`.
- **`RemoveEntity` throws and races** (`World.cs:198-200`) — `Entities[Id]` throws `KeyNotFoundException` before the dead null-check; two concurrent pickups of the same item both pass `TryAdd`, then the second `RemoveEntity` throws unhandled → item duplication + hub exception.
- **Vertical movement is impossible in production** — `CanAscend/CanDescend` are checked on the character's own `WorldLocation` component (`World.cs:323-326`), which nothing ever sets and which `MoveEntity` replaces on every move. Z-transitions only happen via the unvalidated `ChangeLevel` bypass.
- **`WeatherSystem` transition is per-call, not per-time** (`WeatherSystem.cs:58-92`) — `hoursSinceChange` is computed then ignored; flat ~10% per invocation, so change rate scales with tick frequency × region count. `_regionWeather` is a plain `Dictionary` mutated from concurrent region ticks (not thread-safe); Foggy/Stormy are unreachable via transitions; `SeasonManager.GetWeatherModifier` is dead.
- **`EventScheduler` is not thread-safe and not region-scoped** (`EventScheduler.cs:16,106-133`) — a plain `Dictionary` processed from every `MapRegionGrain.TickAsync` with no `RegionId` filter; concurrent ticks race on `IsTriggered` and fire events multiple times.
- **Despawn is a stub; spawns have no cap** (`SpawnControllerGrain.cs:118-131`, `GameMapGrain.SpawnEntityAsync`) — despawn only untracks IDs; no max-entity check; entity counts grow monotonically, event monsters are permanent, long worlds leak entities.
- **`WorldClock` is not thread-safe and reads corrupt `Tick`** (`WorldClock.cs:57-110`) — unsynchronized singleton used from many grains + PerceptionService; `GetTimeOfDay()/GetDay()` advance `_lastTick`, so `Tick()`'s elapsed value shrinks whenever the clock is read between ticks → lost/double-counted game time.
- **Two parallel, unsynchronized time systems** — per-session `TimeScale=60` anchored to `GameStartTime` (perception day/night) vs global `WorldClock` (weather/spawn/events). Each session sees a different time-of-day from the simulation, and **actions never advance time** (pure real-time; no turn cost anywhere).

## Moderate / Minor (verified unless noted)

Bump-collision damages the *mover* and death is commented out (`World.cs:299-313`, O(n) scan); `TryMove(RelativeDirection)` is a silent no-op (`FromDelta(0,0,0)`, `:253-266`); `GeometryHelper.IntersectingPoint` passes the plane normal instead of the line direction (`:27-28`, masked by a test that only checks that case); `Component.HasComponent/HasAllComponents` are broken but dead; three `WorldEventType`s (`ItemDropped`, `ItemUsed`, `DoorLocked`) are never emitted; `Hidden`/secret-door/trap components are set but never read (stealth and traps inert); closing a door on an occupied tile entombs the occupant; `Get<T>` throws on missing, `Has<T>` is an O(n) tree scan, `Set<T>` removes by `typeof(T)` but adds by `GetType()` (base-typed calls leak the old entry); `WorldLocation.GetHashCode` allocates a string per call on the hottest key; duplicated engine code between `Aetherium.Console/Core` and `Aetherium.Server/Core` (fixes must be made twice); no toroidal wrap despite `TorusWorldBuilder`.

## Verified leads (from the brief)

1. **Confirmed** — combat is a `if(false)` placeholder (`ContextEvaluator.cs:115-120`); no attack tool/action exists anywhere; `combat.md`, `combat-survival.json`, `Health`, and several analytics layers all reference combat that can't happen.
2. **Refuted (with nuance)** — component removal *does* exist (`Component.Clear<T>`, used by door open/close); the real API weaknesses are the throwing `Get<T>`, O(n) `Has<T>`, and the `Set<T>` type mismatch.
3. **Partial** — the historical `Monster.GetValidDirections` enumeration-mutation crash is fixed (deferred-removal pattern) in both copies; remaining hazards: empty-list `rand.Next` throw when a monster is boxed in, and the server `Monster.Heartbeat` is dead code.
4. **Partial** — the `NotImplementedException` is at `Monster.cs:78` (not 145), a default arm covering all 6 handled `WorldDirection` values — unreachable for valid enums.

## Strengths

- O(1) location indexing via `ConcurrentDictionary` with a separate `Characters` index and empty-bucket pruning (`World.cs:19-21,211-214`).
- `WorldLocation` value equality/operators are correct and symmetric.
- The multi-use interaction model (GetUseOptions → context filter → reactive disambiguation) is clean and matches its spec.
- `ContextEvaluator` is a pure, null-defensive function of session state.
- Simulation composition is tidy: priority-sorted `TemporalModifierRegistry`, options-driven config, layered spawn-rate model; `SeasonManager`/`WorldClock` conversions are algebraically correct.
- `GameMapGrain.SpawnEntityAsync` validates passability and occupancy before spawning.

## Spec alignment

- **engine-core** — entity defaults, Set/Get/Has happy path, add/remove indexing all met; but impassable-terrain prevention is silently voided in production (no player path calls `TryMove`), and the West/East bug moots X-axis movement constraints. The CanAscend "on current location" design is implemented literally and encoded in the spec, yet unreachable in production.
- **world-entities** — 10-slot capacity met but violated by the equip exploit; key↔door matching met, but **locking is unimplemented** (`DoorLocked` never fires) though the spec says keys "unlock/lock."
- **interaction** — multi-use tools, auto-execute, reactive disambiguation, "No effect" all met; but the spec's "Use with usageId does not perform option discovery" is implemented so literally it becomes the adjacency-bypass hole — spec and code are aligned on a bad requirement.

## Test coverage

Covered: `EngineCoreTests` (entity/World invariants — but only North/South/Up/Down `TryMove`, so the **West/East bug sits in the untested gap**), `InteractionSystemTests` (25), `InventoryTests` (11), `GeometryTests` (1, masks the intersection bug). **Gaps:** `MoveView`/`ChangeLevel` (the real player-movement path — untested); `Memory` (a test would immediately catch the discard bug); all concurrency (dual pickup, index races); the entire `Simulation` namespace (WorldClock/Weather/Season/Spawn — no tests); EventScheduler recurrence/region-scoping; `TryEquip` capacity exploit; WorldEvent-emission assertions; door-close-on-occupied; `ComponentTests.cs` is 100% commented out.
