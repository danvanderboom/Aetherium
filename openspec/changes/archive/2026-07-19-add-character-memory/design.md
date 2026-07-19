## Context
`Memory`/`SpaceTimeMemory` (`Aetherium.Server/Components/`) exist on every `Character` but are dead code: no writer, no reader, and a copy-mutation bug in `AddMemory` that drops new entries. Perception is computed per session on demand (`GameSession.GetPerception` → `PerceptionService.ComputePerception`), and every successful action recomputes it, so perception time is the one place the engine already knows exactly what a character can currently see. `PerceptionDto.Visuals` is keyed by relative `"x,y,z"` offsets from the player; `GameSession.ViewLocation` converts those back to absolute locations.

## Goals / Non-Goals
- Goals: activate the existing component with perception-time recording; per-world policy data; lazy decay + caps; operator-gated read via grain + CLI; deterministic tests using the scripted-action + snapshot tooling.
- Non-Goals: agent/LLM episodic memory (Layer 2 follow-up), event-driven recording, NPC perception ticks, fog-of-war rendering, persistence across teardown.

## Decisions
- **Record in `GameSession.GetPerception`, not `PerceptionService`.** The session owns the player, world, and view location; the service stays a pure computation. Recording converts each relative `Visuals` key back to absolute via `ViewLocation` and reads terrain/entities from the `World` at that location (accurate, avoids depending on `VisualDto` shape).
- **What is recorded:** per visible tile, `("terrain", <TerrainName>)`; per visible non-terrain entity other than the player, `("entity", "<TypeName>:<EntityId>")` — type for aggregate recall, id for "I saw *that* one". Dedup + `Impressions` reinforcement come free from `AddMemory`.
- **Lazy decay:** `MemoryPolicy.EffectiveStrength(strength, age, halfLife) = strength × 0.5^(ageSeconds/halfLife)` computed at read time (pure, unit-testable); no background job, no mutation on read. `halfLife <= 0` disables decay.
- **Caps at write time:** after recording, if `LocationsTracked > MaxLocations`, remove whole locations oldest-first (by that location's most recent `LastEventTime`) until within cap. Whole-location pruning keeps the map coherent (no half-remembered tiles).
- **Policy is per-world data on the ECS `World`** (default: enabled, 10000 locations, 3600s half-life), overridable via `GeneratorParameters` → `GameMapGrain.InitializeAsync` — the established WorldConfig→map threading recipe. Legacy builder-created worlds get defaults.
- **Read is operator-gated.** Memories store absolute coordinates, so `GetMemoryAsync` sits behind the same `OperatorAccess` gate as absolute perception and world snapshots. A future player-facing "recall" would relativize; out of scope.
- **Bug fix over redesign:** `AddMemory`'s dropped-append is fixed by mutating the stored list; existing dedup semantics preserved.

## Risks / Trade-offs
- Perception gains a write side-effect → bounded by visible-set size (a viewport of tiles), gated by `Enabled`; measured as negligible next to FOV/lighting computation.
- `Remember` uses wall-clock `DateTime.Now` (as the component always did) — decay is real-time, not game-time. Acceptable for operator/debug use; switching to game time (`GetCurrentGameTime`) is a deliberate later change since it alters semantics for time-scaled worlds.
- Unbounded content growth *within* a location is naturally limited by dedup (same content = impression bump, not a new row).

## Migration Plan
Additive plus one bug fix in dead code. No signature changes to existing methods. Rollback = remove recording hook + new members; the component returns to dormant.

## Open Questions
- None blocking. Game-time decay and NPC perception ticks are noted follow-ups.
