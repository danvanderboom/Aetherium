## Why
Layer-1 character memory (add-character-memory) shipped with a single world-wide exponential half-life: every character forgets every memory at the same fixed rate, forever. `Impressions` are counted but change nothing, and no memory is ever actually forgotten — entries only leave via the location cap. Real memory doesn't work that way: retention decays against a per-memory *stability* that grows with each spaced re-exposure (the Ebbinghaus retention curve plus the spacing effect). A place a character keeps revisiting should stick longer each time — until it is so familiar it never fades — while a corridor glimpsed once a day ago should be genuinely gone. And memory quality should be a character trait, not an engine constant: a forgetful goblin and an eidetic archivist should be world data (per the engine's per-world-data rule), all opt-in per game.

## What Changes
- **Per-memory stability**: `SpaceTimeMemory` gains `StabilitySeconds` (its personal half-life; `0` ⇒ fall back to the world half-life, preserving existing rows' semantics) and `Permanent`. Effective strength stays a pure read: `strength × 0.5^(age/stability)`.
- **Spaced reinforcement**: when a character re-perceives remembered content after at least `MinReinforcementIntervalSeconds`, the memory's stability multiplies by `StabilityGrowthFactor` and its strength refreshes to full. Massed re-exposure (staring at the same tile frame after frame) still bumps `Impressions` and `LastEventTime` but does not grow stability.
- **Permanence through familiarity**: when stability reaches `PermanenceThresholdSeconds`, the memory is marked `Permanent` and never decays — "so familiar it stays forever."
- **Real forgetting**: at write time (never during reads), entries whose effective strength has fallen below `ForgetThreshold` are culled.
- **Per-character profiles**: a new `MemoryProfile` component (`HalfLifeMultiplier`, `StabilityGrowthMultiplier`, `MaxLocationsOverride`) overrides world defaults per character — forgetful vs. sharp is per-entity data.
- **Opt-in**: everything above sits behind `MemoryPolicy.DynamicsEnabled` (default `false`), threaded from world generator parameters (`MemoryDynamicsEnabled`, `MemoryStabilityGrowthFactor`, `MemoryMinReinforcementIntervalSeconds`, `MemoryPermanenceThresholdSeconds`, `MemoryForgetThreshold`). A world that doesn't opt in behaves exactly as today.
- **Read surface**: `MemoryEntryDto` gains `StabilitySeconds`/`Permanent`; effective strength honors per-memory stability and the character's profile multiplier. `aetherctl memory get` displays the new fields. No new commands.

## Impact
- Affected specs:
  - `character-memory` (ADDED: stability & reinforcement, permanence, forgetting, per-character profiles, dynamics opt-in)
- Affected code:
  - `Aetherium.Server/Core/MemoryPolicy.cs` (dynamics fields + shared stability math), `Components/SpaceTimeMemory.cs`, `Components/Memory.cs`, new `Components/MemoryProfile.cs`
  - `Aetherium.Server/GameSession.cs` (recording path: reinforcement gate, permanence, cull), `MultiWorld/GameMapGrain.cs` (parameter threading)
  - `Aetherium.Model/CharacterMemoryDto.cs`, `Aetherium.Server/Management/GameManagementGrain.cs` (read path), `Aetherctl/Commands/MemoryCommands.cs` (display)
  - Tests in `Aetherium.Test` (unit + headless-session integration), `Aetherctl.Test`
- Non-Goals: game-time (WorldClock) decay — Layer 1 decays on real time and this change stays consistent with it (a future change can migrate both together); event-driven memory; NPC perception ticks (NPCs still only accumulate spatial memory once something computes perception for them — but the model here is character-agnostic and `add-identity-recognition` gives NPCs their first memory writes); memory persistence across grain rehydration; retrievability-dependent stability increments (full FSRS) — see design.md for why the simpler model is preferred.
