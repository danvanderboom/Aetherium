## 1. Engine model
- [ ] 1.1 Add `RecognitionPolicy` (`Enabled=false`, `RangeTiles=6`, `OwnKindAcuity=0.9`, `OtherKindAcuity=0.4`, `RecognitionThreshold=0.25`, `EncounterTimeoutSeconds=300`, `FamiliarityHalfLifeSeconds=86400`, `MeetStrength=0.5`, `MaxIndividuals=1000`) + `World.RecognitionPolicy`
- [ ] 1.2 Add `RecognitionProfile` component (enabled override, range override, own/other-kind acuity, per-kind acuity dictionary)
- [ ] 1.3 Add `IndividualRecognition` component: `KnownIndividuals` (entityId → FirstMet/LastSeen/Encounters/Strength/StabilitySeconds/Permanent), familiarity math delegated to the shared `MemoryPolicy` stability helpers (dependency: add-memory-dynamics); weakest-first pruning at `MaxIndividuals`
- [ ] 1.4 Kind resolution helper: `CreatureTypeTag.Value` if present else CLR type name lowercased (shared with the `creature_died` path)
- [ ] 1.5 Thread `Recognition*` generator parameters in `GameMapGrain.InitializeAsync`

## 2. Recognition sweep
- [ ] 2.1 Add the canonical-world sweep to `GameMapGrain.TickAsync`: for each character with recognition active, find other characters within `World.Topology` distance ≤ range on the same z-level; short-circuit entirely when policy disabled
- [ ] 2.2 In-range handling: first meeting records the individual (`MeetStrength`, familiarity stability from policy) ; spaced re-meeting reinforces via the shared spacing gate; every in-range tick refreshes `LastSeen`
- [ ] 2.3 Encounter gating: fire at most one event per pair per encounter (first meeting, or `now − LastSeen > EncounterTimeoutSeconds` at approach)
- [ ] 2.4 Recognition determination: `acuity × effectiveFamiliarity ≥ threshold`, acuity from profile per-kind override → own/other-kind defaults

## 3. ECA integration
- [ ] 3.1 New tiles: `CharacterRecognizedTrigger` (`character_recognized`), `RecognizedKindIsCondition`, `FamiliarityAtLeastCondition`, `FirstMeetingIsCondition` (reflection-discovered; validator/docs automatic)
- [ ] 3.2 Extend `EcaActionTarget` with `Recognizer`/`Recognized` (additive); add to `deal_damage`/`apply_status` `validTargets`; unresolvable target keeps skip semantics
- [ ] 3.3 Generalize `EcaEventContext` (server-internal): recognition fields + trigger-agnostic `EventX/Y/Z`; `EcaConditionDescriptor` gains flat-union fields for the new conditions
- [ ] 3.4 Dispatch: sweep events evaluate through `_ecaRuntime` and execute via `ExecuteEcaRequestAsync`, mirroring `RunCreatureDiedRulesAsync`

## 4. Configuration tool
- [ ] 4.1 `ConfigureCharacterTool` (`configurecharacter`, `world_edit`, `WorldBuildingToolContext`): sets `MemoryProfile`/`RecognitionProfile` fields on an entity by id, creating components as needed; validates entity exists and fields are in range

## 5. Read surface
- [ ] 5.1 `RecognitionDto` in `Aetherium.Model`; `IGameManagementGrain.GetRecognitionAsync(worldId, entityId)` → JSON, operator-gated, canonical world via `WorldRegistry`, null for unknown world/entity
- [ ] 5.2 CLI `aetherctl recognition get <worldId> <entityId> [--json]`; register in `Program.cs`; `Common.ProcessExitCode` on failure (no `Environment.Exit`)

## 6. Tests (linked to spec requirements)
- [ ] 6.1 Unit: first in-range meeting records the individual with meet strength; spaced re-meeting reinforces stability; continuous contact does not compound — Individual Recognition Memory
- [ ] 6.2 Unit: own-kind recognized after one prior meeting at defaults; other-kind still stranger; per-kind acuity override flips the outcome — Kind-Dependent Recognition Acuity
- [ ] 6.3 Unit: familiarity decays on the shared curve; frequent meetings latch permanence; `MaxIndividuals` prunes weakest first — Individual Recognition Memory
- [ ] 6.4 Server: sweep detects a PC's canonical body near an NPC and both directions update; policy disabled ⇒ no state changes — Recognition Proximity Sweep, Per-World Recognition Configuration
- [ ] 6.5 Server: pair in continuous contact fires exactly one `character_recognized`; separation past timeout + re-approach fires again — Encounter-Gated Recognition Events
- [ ] 6.6 Server: an ECA rule on `character_recognized` with `recognized_kind_is` + `familiarity_at_least` executes its action against `Recognizer`/`Recognized` targets; `first_meeting_is` distinguishes stranger vs known — ECA trigger/conditions/targets
- [ ] 6.7 Server: `configurecharacter` makes an NPC's acuity/memory profile take effect live — Runtime Profile Configuration
- [ ] 6.8 Server: `GetRecognitionAsync` returns individuals with effective familiarity for a PC and an NPC; operator gate denies when disabled — Recognition Memory Retrieval
- [ ] 6.9 CLI: structural coverage for `recognition get`
- [ ] 6.10 Validator: a rules.yaml using the new trigger/conditions/targets validates; unknown kinds still error — ECA vocabulary coverage

## 7. Docs
- [ ] 7.1 Update `docs/eca-scripting.md` vocabulary table and `docs/agents/README.md` recognition section
