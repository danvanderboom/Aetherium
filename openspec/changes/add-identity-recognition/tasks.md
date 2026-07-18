## 1. Engine model
- [x] 1.1 Add `RecognitionPolicy` (`Enabled=false`, `RangeTiles=6`, `OwnKindAcuity=0.9`, `OtherKindAcuity=0.4`, `RecognitionThreshold=0.25`, `EncounterTimeoutSeconds=300`, `FamiliarityHalfLifeSeconds=86400`, `MeetStrength=0.5`, `MaxIndividuals=1000`) + `World.RecognitionPolicy`
- [x] 1.2 Add `RecognitionProfile` component (enabled override, range override, own/other-kind acuity, per-kind acuity dictionary)
- [x] 1.3 Add `IndividualRecognition` component: `KnownIndividuals` (entityId → FirstMet/LastSeen/Encounters/Strength/StabilitySeconds/Permanent), familiarity math delegated to the shared `MemoryPolicy` stability helpers (dependency: add-memory-dynamics); weakest-first pruning at `MaxIndividuals`
- [x] 1.4 Kind resolution helper (`RecognitionKind.Resolve`): `CreatureTypeTag.Value` if present else CLR type name lowercased (shared with the `creature_died` path via `GameMapGrain.ResolveKind`)
- [x] 1.5 Thread `Recognition*` generator parameters in `GameMapGrain.InitializeAsync` (`ApplyRecognitionPolicy`)

## 2. Recognition sweep
- [x] 2.1 Add the canonical-world sweep to `GameMapGrain.TickAsync` (`RunRecognitionSweepAsync`): for each character with recognition active, find other characters within `World.Topology` distance ≤ range on the same z-level; short-circuit entirely when policy disabled
- [x] 2.2 In-range handling: first meeting records the individual (`MeetStrength`, familiarity stability from policy); spaced re-meeting reinforces via the shared spacing gate; every in-range tick refreshes `LastSeen`
- [x] 2.3 Encounter gating: fire at most one event per pair per encounter (first meeting, or `now − LastSeen > EncounterTimeoutSeconds` at approach)
- [x] 2.4 Recognition determination: `acuity × effectiveFamiliarity ≥ threshold`, acuity from profile per-kind override → own/other-kind defaults

## 3. ECA integration
- [x] 3.1 New tiles: `CharacterRecognizedTrigger` (`character_recognized`), `RecognizedKindIsCondition`, `FamiliarityAtLeastCondition`, `FirstMeetingIsCondition` (reflection-discovered; validator/docs automatic)
- [x] 3.2 Extend `EcaActionTarget` with `Recognizer`/`Recognized` (additive); add to `deal_damage`/`apply_status` `validTargets`; unresolvable target keeps skip semantics
- [x] 3.3 Generalize `EcaEventContext` (server-internal): recognition fields + trigger-agnostic `EventX/Y/Z` (renamed from VictimX/Y/Z); `EcaConditionDescriptor` gains flat-union fields for the new conditions
- [x] 3.4 Dispatch: sweep events evaluate through `_ecaRuntime` and execute via `ExecuteEcaRequestAsync`, mirroring `RunCreatureDiedRulesAsync`

## 4. Configuration tool
- [x] 4.1 `ConfigureCharacterTool` (`configurecharacter`, `world_edit`, `WorldBuildingToolContext`): sets `MemoryProfile`/`RecognitionProfile` fields on an entity by id, creating components as needed; validates entity exists and at least one field supplied

## 5. Read surface
- [x] 5.1 `RecognitionDto` in `Aetherium.Model`; `IGameManagementGrain.GetRecognitionAsync(worldId, entityId)` → JSON, operator-gated, canonical world via `WorldRegistry`, null for unknown world/entity
- [x] 5.2 CLI `aetherctl recognition get <worldId> <entityId> [--json]`; registered in `Program.cs`; `Common.ProcessExitCode` on failure (no `Environment.Exit`)

## 6. Tests (linked to spec requirements)
- [x] 6.1 Unit: first in-range meeting records the individual with meet strength; spaced re-meeting reinforces stability; continuous contact does not compound — Individual Recognition Memory (`IdentityRecognitionTests`)
- [x] 6.2 Unit: own-kind recognized after a meeting at defaults; other-kind still stranger; per-kind acuity override flips the outcome — Kind-Dependent Recognition Acuity
- [x] 6.3 Unit: familiarity decays on the shared curve; frequent meetings latch permanence; `MaxIndividuals` prunes weakest first — Individual Recognition Memory
- [x] 6.4 Server: the sweep records a co-present character (via a map-grain tick) readable through `GetRecognitionAsync`; policy disabled ⇒ no state — Recognition Proximity Sweep, Per-World Recognition Configuration (`HeadlessDrivingTests.Recognition_*`)
- [x] 6.5 Unit: pair in continuous contact yields one new-encounter observation; separation past timeout re-opens a new encounter — Encounter-Gated Recognition Events (`IdentityRecognitionTests.Observe_EncounterGating_ByTimeout`; note: driven at the component level for deterministic timing rather than through wall-clock ticks)
- [x] 6.6 Unit: an ECA rule on `character_recognized` with `recognized_kind_is` + `familiarity_at_least` executes against `Recognizer`/`Recognized`; `first_meeting_is` distinguishes stranger vs known; mismatched target skips — ECA trigger/conditions/targets (`RecognitionEcaTests`)
- [x] 6.7 Unit: `configurecharacter` sets an entity's acuity/memory profile live (forgetful override lowers retained strength) — Runtime Profile Configuration (`ConfigureCharacterToolTests`)
- [x] 6.8 Server: `GetRecognitionAsync` returns individuals with effective familiarity; operator gate + unknown world/entity return null — Recognition Memory Retrieval
- [x] 6.9 CLI: structural coverage for `recognition get` (`HeadlessDrivingCommandsTests.RecognitionGet_*`)
- [x] 6.10 Validator: a definition using the new trigger/conditions/targets validates clean; vocabulary discovery of the new tiles — ECA vocabulary coverage (`RecognitionEcaTests.Validator_AcceptsRecognitionRule`, `Vocabulary_DiscoversRecognitionTiles`)

## 7. Docs
- [x] 7.1 Update `docs/eca-scripting.md` vocabulary table and `docs/agents/README.md` recognition section
