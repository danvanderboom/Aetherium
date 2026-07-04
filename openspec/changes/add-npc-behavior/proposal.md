## Why
Since the Phase 3 worldgen pass-list unification, generated worlds are populated with monsters (Monster/Zombie/Snake entities placed by the population passes). But those monsters never acted — `Monster.Heartbeat` was dead code and nothing in the tick pipeline moved them — and the client never drew them: perception encoded other characters only as an opaque `ThingsSeen` count, and the console map view rendered player/items/terrain only, so a monster showed as the terrain beneath it. The world was populated but frozen and invisible — not yet a game. This is the first Phase 5 vertical slice ("make the game a game").

## What Changes
- **Monsters tick.** `GameMapGrain.TickAsync` now drives NPC movement: each eligible tick it steps every `Monster` one cardinal cell via the validated `World.TryMoveSteps` and fans out an `EntityMovedDelta`, so co-located players see monsters move. The world mutations run synchronously (atomic w.r.t. a reentrant player turn); only the broadcast is awaited. Movement lays heat trails through the existing world-event subscriber, so infrared sees monsters too.
- **Behavior is paced and toggleable.** New `SimulationOptions.EnableNpcBehavior` (default on) and `NpcMoveIntervalTicks` (default 1) control NPC movement without touching `TickHz`.
- **Wander decision is a unit.** `Monster.NextWanderDirection()` (cardinal-only, 50% momentum, `null` when boxed in) encapsulates the choice without moving; `Heartbeat` delegates to it, which also fixes the empty-list `rand.Next` crash the simulation audit flagged.
- **Monsters render.** New `CharacterDto` + `PerceptionDto.VisibleCharacters`, populated in `PerceptionService` parallel to `VisibleItems`, each carrying the entity's `TileType` (glyph/color). The console client draws characters above items above terrain (a monster on treasure is what you need to see). The perceiving player is excluded — they are always the centre marker.
- **Perception hardening.** Two latent `Component.Get<Inventory>()` crashes in the perception path are made null-safe — relevant precisely because NPCs are inventory-less Characters.

## Impact
- Affected specs: `perception` (visible characters), `simulation` (new — NPC behavior on the world tick)
- Affected code: `Aetherium.Server/MultiWorld/GameMapGrain.cs`, `Aetherium.Server/Entities/Monster.cs`, `Aetherium.Server/Simulation/SimulationOptions.cs`, `Aetherium.Server/PerceptionService.cs`, `Aetherium.Server/MappingExtensions.cs`, `Aetherium.Model/{CharacterDto,PerceptionDto}.cs`, `Aetherium.Console/Views/ClientConsoleMapView.cs`

## Status
Implemented in this slice on `develop`. Verified: solution build 0 errors; `Aetherium.Test` 937 passed / 0 skipped (+9); `Aetherctl.Test` 38 passed; server boots with Orleans (`Started silo`, 0 exceptions, tick loop healthy). Out of scope (later Phase 5 items): monster aggro/pursuit and combat (P3-7), agent-driven NPCs (P3-5), spawn/despawn lifecycle and event monsters, and Unity-client rendering of characters.
