## Why
Characters have no concept of *who* they've met. Spatial memory records `Monster:abc123 was at (x,y)` as scenery — there is no "I know this individual," no familiarity that builds across encounters, and no way for gameplay to react when two characters who know each other come face to face. Individual recognition is the foundation of social mechanics (guards remembering an intruder, pack animals knowing pack-mates, a rival recognizing the player across a market), and the natural reactive hook for it already exists: the ECA rule system — it just has no recognition trigger. Recognition should follow believable rules: creatures are good at telling apart individuals of their own kind and poor with other kinds (cross-species "they all look alike"), familiarity should strengthen with repeated meetings and fade between them on the same curve spatial memory uses — all per-world/per-character data, opt-in per game.

## What Changes
- **Individual recognition memory**: a new `IndividualRecognition` component holding `KnownIndividuals` (entity id → first-met, last-seen, encounter count, familiarity strength + stability, permanent flag). Familiarity decays and reinforces using the *same* stability curve as `add-memory-dynamics` (this change depends on it) — meet someone once and they fade; see them daily and they become unforgettable.
- **Kind-dependent acuity**: recognition strength = acuity × effective familiarity, where acuity comes from a per-character `RecognitionProfile` (`OwnKindAcuity` default 0.9, `OtherKindAcuity` default 0.4, optional per-kind overrides, range, enabled) over per-world `RecognitionPolicy` defaults. "Kind" is `CreatureTypeTag.Value` when present, else the entity's type name lowercased — the same rule the `creature_died` trigger uses.
- **Canonical-world proximity sweep**: each `GameMapGrain.TickAsync`, characters with recognition active check other characters within range (distance via `World.Topology`, same z-level). In range ⇒ familiarity updates (first meeting records the individual; spaced re-meetings reinforce). Recognition succeeds when acuity × effective familiarity ≥ the world threshold — deterministic, no RNG (stochasticity is available at the rule level via the existing `chance` condition).
- **Encounter gating**: a pair in continuous contact fires at most one event per encounter; a new encounter begins when the pair has been apart longer than `EncounterTimeoutSeconds`.
- **ECA trigger**: new vocabulary tiles — trigger `character_recognized` (binds recognizer, recognized, both kinds, familiarity, first-meeting flag, location); conditions `recognized_kind_is`, `familiarity_at_least`, `first_meeting_is`; `EcaActionTarget` gains `Recognizer`/`Recognized` so existing actions (`deal_damage`, `apply_status`, `spawn_creature`) work on recognition events. Validator coverage is automatic (tiles are reflection-discovered).
- **Runtime configuration tool**: a `configurecharacter` worldbuilding tool (sets `MemoryProfile` / `RecognitionProfile` fields on an entity by id) so operators can make an NPC forgetful or sharp-eyed live via the existing `aetherctl world edit` command.
- **Read surface**: operator-gated `IGameManagementGrain.GetRecognitionAsync(worldId, entityId)` → JSON (works for PCs and NPCs via the canonical world in `WorldRegistry`); CLI `aetherctl recognition get <worldId> <entityId> [--json]`.

## Impact
- Affected specs:
  - `identity-recognition` (NEW capability: individual memory, acuity, sweep, encounter gating, configuration, runtime profile tool)
  - `eca-scripting` (ADDED: recognition trigger, conditions, action targets)
  - `game-management-grain` (ADDED: recognition memory retrieval)
  - `aetherctl` (ADDED: recognition inspection command)
- Affected code:
  - `Aetherium.Server/Components/IndividualRecognition.cs`, `Components/RecognitionProfile.cs`, `Core/RecognitionPolicy.cs` (new); `Core/World.cs` (policy property)
  - `Aetherium.Server/MultiWorld/GameMapGrain.cs` (sweep in `TickAsync`, parameter threading, event dispatch through `_ecaRuntime`)
  - `Aetherium.Server/Eca/EcaTiles.cs`, `EcaRuntime.cs` (server-internal context generalization), `Aetherium.Model/Eca/EcaConfig.cs` (additive enum members + condition fields)
  - `Aetherium.Server/Agents/Tools/WorldBuilding/ConfigureCharacterTool.cs` (new)
  - `Aetherium.Server/Management/IGameManagementGrain.cs`, `GameManagementGrain.cs`, `Aetherium.Model/RecognitionDto.cs` (new)
  - `Aetherctl/Commands/RecognitionCommands.cs` (new), `Program.cs`; tests in `Aetherium.Test`/`Aetherctl.Test`
- **Depends on**: `add-memory-dynamics` (shared stability/decay math in `MemoryPolicy`).
- Non-Goals: line-of-sight gating (proximity-only this slice); disguise/stealth modifiers; session-mirror recognition (real client sessions are covered by the canonical-world sweep; mirrors do not run recognition); recognition state persistence across grain rehydration; group/faction-level recognition; NPC behavior-tree consumption of recognition state (rules can act via ECA now; tree integration is a later change).
