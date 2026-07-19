## Context

Three systems meet here:
- **Memory** (add-character-memory + add-memory-dynamics): perception-time spatial memory with a stability/decay curve. Purely spatial — no notion of individuals.
- **ECA scripting** (add-eca-scripting): per-world `when/if/do` rules over a reflection-discovered vocabulary, evaluated by a pure `EcaRuntime` in `GameMapGrain`, currently with exactly one trigger (`creature_died`).
- **Sessions vs. canonical world**: session-bound player characters are simulated in two places — the canonical `_world` in `GameMapGrain` (authoritative, post gateway-first fix) and a session-local mirror in `GameSession` (client view). Headless operator sessions bind directly to the canonical world via `WorldRegistry`. NPCs exist only canonically and act in `StepNpcsAsync` behavior-tree ticks.

The request: characters (PCs and NPCs alike) recognize *individuals* — good at their own kind, poor at others, familiarity building and fading like real memory — and recognition proximity fires ECA rules. All configurable, opt-in.

## Goals / Non-Goals

**Goals**
- One recognition model shared by every character type, using the memory-dynamics stability curve for familiarity.
- Deterministic recognition (replayable, testable); randomness only where rules opt into it (`chance`).
- ECA as the reactive surface: recognition raises a first-class trigger; existing actions gain recognition targets.
- Opt-in per world; per-character override components; runtime-configurable via worldbuilding tooling.
- Operator read API + CLI for both PCs and NPCs.

**Non-Goals**
- Line-of-sight or lighting gating (recognition is proximity-based this slice; a LOS gate composes later via `World.Topology` line checks).
- Disguises, stealth, appearance-change mechanics.
- Recognition running in session-mirror worlds (see Decision 2).
- Persistence across grain rehydration (component state is not snapshot-serialized; consistent with memory).
- Behavior-tree consumption of recognition (ECA covers reaction now; tree blackboard integration is its own change).

## Decisions

### 1. Familiarity is the memory-dynamics curve applied to individuals

`IndividualRecognition.KnownIndividuals: entityId → { FirstMet, LastSeen, Encounters, Strength, StabilitySeconds, Permanent }` reuses `MemoryPolicy`'s pure stability math (effective familiarity = `strength × 0.5^(age/stability)`; spaced re-meeting ⇒ stability × growth factor; permanence latch). One implementation of the curve, two consumers — spatial memory and social memory stay behaviorally consistent by construction. Defaults differ where it matters: initial familiarity stability is `FamiliarityHalfLifeSeconds` (default 86400 — you remember a face longer than a floor tile), and the dictionary is capped by `MaxIndividuals` (default 1000, weakest-effective-familiarity pruned first).

This is why the change **depends on add-memory-dynamics**: without it there is no shared stability model to reuse.

### 2. Recognition runs only on the canonical world, in the grain tick

Where should proximity be checked? Candidates: session `GetPerception` (covers PCs, but real client sessions perceive a *mirror* world whose `WorldEvents` never reach the grain's ECA runtime — and headless sessions perceive in `GameManagementGrain`, which has no ECA runtime either); `EntityMoved` world events (fires per move, misses two stationary characters, and the subscriber is synchronous); or the `GameMapGrain.TickAsync` sweep (canonical positions for *all* characters, grain context for ECA execution, natural cadence).

Chosen: **one sweep in `TickAsync`**, alongside `StepNpcsAsync`. Every character with recognition active — PCs (their canonical bodies move gateway-first now, so canonical position is truthful) and NPCs alike — checks other characters in range. This gives a single detection site, a single familiarity store (canonical entities' components), and direct in-grain ECA dispatch (`_ecaRuntime.Evaluate` → `ExecuteEcaRequestAsync`), mirroring exactly how `creature_died` flows. Session mirrors never run recognition; `GetRecognitionAsync` reads canonical components via `WorldRegistry`, so PC and NPC state are read the same way.

Trade-off: local (non-grain) worlds get no recognition events — the same limitation `creature_died` already has; documented, not fought.

### 3. Deterministic threshold, not a recognition roll

`recognized ⇔ acuity(recognizer, kindOf(target)) × effectiveFamiliarity ≥ RecognitionThreshold` (default 0.25). With defaults (meet strength 0.5), one prior meeting makes an own-kind individual recognizable (0.9 × 0.5 = 0.45) while an other-kind individual stays a stranger (0.4 × 0.5 = 0.20) until repeat meetings raise familiarity — the requested "good with own kind, poor with others" falls out of the numbers rather than special-case code. Deterministic evaluation keeps replays and tests exact; rules wanting unpredictability add the existing `chance` condition.

First meetings are their own signal: the individual is recorded (that's what makes later recognition possible) and the trigger fires with `firstMeeting=true`, so rules can distinguish "stranger appeared" from "I know them."

### 4. Encounter gating via LastSeen

Firing every tick while two characters stand together would spam rules. A pair fires at most once per *encounter*: an event fires only if `now − LastSeen > EncounterTimeoutSeconds` (default 300) or it is the first meeting; every in-range tick still refreshes `LastSeen` (which is also what makes the reinforcement spacing gate behave: continuous contact is massed exposure and does not compound stability). Parting for longer than the timeout ends the encounter, so the next approach fires again.

### 5. ECA surface: new tiles, additive enum, server-internal context generalization

- New tiles in `EcaTiles.cs`: `CharacterRecognizedTrigger`, `RecognizedKindIsCondition`, `FamiliarityAtLeastCondition`, `FirstMeetingIsCondition`. `EcaVocabulary` discovers them by reflection — validator, docs, and runtime pick them up with no registration.
- `EcaActionTarget` (serialized Model enum) gains `Recognizer` and `Recognized` — additive, wire-safe. `deal_damage`/`apply_status` add them to `validTargets`; a target that doesn't resolve for the current event (e.g. `killer` on a recognition event) resolves to null and the action is skipped — existing runtime semantics, now doing double duty as trigger/target mismatch handling. (A validator cross-check of trigger↔target compatibility is a worthwhile later hardening, noted, not done here.)
- `EcaEventContext` is server-internal (never serialized), so it generalizes freely: nullable recognition fields (`RecognizerEntityId`, `RecognizerKind`, `RecognizedEntityId`, `RecognizedKind`, `Familiarity`, `FirstMeeting`) join the existing flat union, and the event location fields become trigger-agnostic (`EventX/Y/Z`, populated by both raise sites; `spawn_creature` reads them).
- `EcaConditionDescriptor` (Model, serialized) gains the flat-union fields the new conditions bind (`RecognizedKind`, `MinFamiliarity`, `FirstMeeting`) — same discipline as existing descriptors.

### 6. Sweep cost and the topology rule

Proximity uses `World.Topology` distance (never raw coordinate deltas — the grid-topologies structural rule), same z-level, range default 6. Implementation iterates the world's character/monster set pairwise-by-recognizer rather than enumerating tiles, so cost is O(recognizers × characters) distance checks per tick — negligible at current populations, zero when the policy is disabled (the sweep short-circuits before any iteration). `RecognitionPolicy.Enabled` defaults `false`; generator parameters: `RecognitionEnabled`, `RecognitionRangeTiles`, `RecognitionOwnKindAcuity`, `RecognitionOtherKindAcuity`, `RecognitionThreshold`, `RecognitionEncounterTimeoutSeconds`, `RecognitionFamiliarityHalfLifeSeconds`, `RecognitionMaxIndividuals`.

### 7. Runtime configuration through the existing worldbuilding seam

`ConfigureCharacterTool` (`configurecharacter`, requires `world_edit`, `WorldBuildingToolContext`) sets `MemoryProfile`/`RecognitionProfile` fields on an entity by id, creating the component if absent. No new CLI verb needed: `aetherctl world edit <worldId> configurecharacter --args '{"entityId":"...","ownKindAcuity":0.95,"halfLifeMultiplier":0.2}'` already routes through `ExecuteWorldToolAsync`. This is what makes "make this NPC forgetful, that one eagle-eyed" a live operator action and an aetherctl-scriptable test scenario.

## Risks / Trade-offs

- **PC canonical-position dependence**: the sweep trusts canonical bodies. Correct post gateway-first fix (68e7ce1); had that fix not landed, PC recognition would have fired at stale positions.
- **Recognition through walls** reads oddly in dungeons. Accepted this slice (documented); LOS gate is a clean later addition.
- **Tick-cadence detection** can miss a character sprinting through range between ticks. Accepted: encounter semantics ("came near enough for long enough") arguably *should* exclude a blur passing by.
- **Two stores for PC social memory** do not exist — mirrors never run recognition, so there is exactly one store (canonical). The cost is that a future offline/local-session mode would need its own wiring.

## Migration

None. New capability, default-off policy, additive Model changes only.
