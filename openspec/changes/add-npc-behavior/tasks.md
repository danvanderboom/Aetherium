> Status (2026-07-03): implemented and verified on `develop` (feat: Phase 5 slice — NPCs tick and render). Suite 937+38 / 0 skipped; server boots with Orleans and the tick loop is healthy. Unchecked items are deliberately out of this slice's scope and belong to later Phase 5 work.

## 1. Server — monsters tick
- [x] 1.1 Add `SimulationOptions.EnableNpcBehavior` and `NpcMoveIntervalTicks`
- [x] 1.2 Add `Monster.NextWanderDirection()` (cardinal, momentum, null when boxed); delegate `Heartbeat` to it; fix the boxed-in `rand.Next` crash; remove dead `SelectRandomDirection`
- [x] 1.3 Drive monster movement from `GameMapGrain.TickAsync` via `TryMoveSteps` + `EntityMovedDelta` fan-out, gated by options and a tick counter, synchronous-mutate then async-broadcast
- [ ] 1.4 Aggro / pursuit / fleeing behavior (out of scope — future Phase 5 / combat P3-7)
- [ ] 1.5 Spawn/despawn lifecycle and event-driven monster cleanup (out of scope — Events domain, tracked separately)

## 2. Protocol + server — visible characters
- [x] 2.1 Add `CharacterDto` and `PerceptionDto.VisibleCharacters`
- [x] 2.2 Add `MappingExtensions.ToCharacterDto` (glyph/color from the entity's Tile)
- [x] 2.3 Populate `VisibleCharacters` in `PerceptionService`, excluding the perceiving player's cell
- [x] 2.4 Harden the two latent `Component.Get<Inventory>()` crashes in the perception path

## 3. Client — render characters
- [x] 3.1 Draw characters above items above terrain in `ClientConsoleMapView` via a pure, tested `ResolveContentLayer`
- [x] 3.2 `DrawCharacter` reuses `DrawTileType` so lighting/infrared/tint apply uniformly
- [ ] 3.3 Render characters in the Unity client (out of scope — Unity live-mode is a separate P3 stub)

## 4. Tests
- [x] 4.1 `NextWanderDirection` open/boxed, and a monster steps to an adjacent cell
- [x] 4.2 Perception includes a visible monster with its tile; the perceiving player is excluded
- [x] 4.3 Client content-layer priority (character > item > terrain > empty)
- [x] 4.4 TestCluster tick moves a spawned monster and fans out perception (seed-tolerant)
