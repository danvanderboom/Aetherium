## Why
Characters have a fully-designed but dormant `Memory` ECS component (`SpaceTimeMemory`: location, content type, content, strength, bias, impressions) — nothing ever writes or reads it, so "what does this character remember / where do they think they've been" is unanswerable. This is the last missing piece of the original operator triad (perception ✓, location ✓, memory ✗). Activation also unblocks the codebase's visible intent: memory-driven NPC goals (commented-out `Monster.SetGoal`) and fog-of-war rendering.

## What Changes
- **Fix a latent bug** in `Memory.AddMemory`: it appends new memories to a `.ToList()` copy, silently dropping them (only the dedup/update path worked).
- **Record memory at perception time** (Layer 1 of the two-layer design): in `GameSession.GetPerception`, after perception is computed, record into the player character's `Memory` component — the terrain name of each visible tile and each visible non-terrain entity (`Type:EntityId`), at absolute locations. `PerceptionService` stays pure.
- **Per-world memory policy** (per the engine's per-world-data rule): `MemoryPolicy { Enabled (default true), MaxLocations (default 10000), DecayHalfLifeSeconds (default 3600) }` carried on the ECS `World`, overridable via world `GeneratorParameters` (`MemoryEnabled`, `MemoryMaxLocations`, `MemoryDecayHalfLifeSeconds`) threaded through `GameMapGrain.InitializeAsync`.
- **Lazy decay + caps**: effective strength computed at read time (`strength × 0.5^(age/halfLife)`); no background job. When `MaxLocations` is exceeded, the oldest locations are pruned at write time.
- **Read API**: `IGameManagementGrain.GetMemoryAsync(sessionId)` → JSON (`CharacterMemoryDto`), operator-gated (memories carry absolute coordinates — god-view read, like absolute perception).
- **CLI**: `aetherctl memory get <sessionId> [--json]`.

## Impact
- Affected specs:
  - `character-memory` (NEW capability: recording, decay/caps, per-world configuration)
  - `game-management-grain` (ADDED: Character Memory Retrieval)
  - `aetherctl` (ADDED: Memory Inspection Command)
- Affected code:
  - `Aetherium.Server/Components/Memory.cs` (bug fix), `Aetherium.Server/Core/MemoryPolicy.cs` (new), `Core/World.cs` (policy property)
  - `Aetherium.Server/GameSession.cs` (recording), `MultiWorld/GameMapGrain.cs` (config threading)
  - `Aetherium.Server/Management/IGameManagementGrain.cs`, `GameManagementGrain.cs`, `Aetherium.Model/CharacterMemoryDto.cs` (new)
  - `Aetherctl/Commands/MemoryCommands.cs` (new), `Program.cs`; tests in `Aetherium.Test`/`Aetherctl.Test`
- Non-Goals (Layer 2 + later): agent-grain episodic memory for LLM context; event-driven memory (`WorldEvents`); NPC perception ticks (NPCs stay memory-less until they get one — perception is only computed for session-bound characters); memory-influenced perception/fog-of-war rendering; memory persistence across world teardown (rides along when world serialization lands).
