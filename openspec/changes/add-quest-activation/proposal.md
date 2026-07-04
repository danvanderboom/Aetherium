## Why
The narrative system has a complete, wired objective-completion loop — `GameHub` emits `player_arrived` on portal travel, `NarrativeConsequenceEngine.ProcessEventAsync` forwards it to `NarrativeStateGrain.RecordEventAsync`, and `HandlePlayerArrivedEventAsync` resolves each active quest's `travel_to` objective (via `CrossWorldConstraintResolver`) and marks the quest complete when all objectives are done. But it is dead: `NarrativeStateGrain.ActiveQuestIds` is **never populated**. There is a `CanStartQuestAsync` (prerequisite check) but no method that actually *starts* a quest, so `HandlePlayerArrivedEventAsync` iterates an always-empty set and no quest can ever progress or complete. This is Phase 5 item **P3-2** (narrative/quest activation): the populated, difficulty-tuned worlds have no goals because quests can't be activated.

## What Changes
**Slice 1 — grain-level quest activation + `travel_to` completion (this pass):**
- Add `StartQuestAsync(questId)` to `INarrativeStateGrain`: validates via the existing `CanStartQuestAsync` (prerequisites met, not already active/completed), resolves the `QuestDefinition` (base narrative or generated), adds it to `ActiveQuestIds`, seeds `ActiveQuestObjectives`, and initializes `CompletedObjectives`. Returns whether the quest started.
- Add `GetActiveQuestIdsAsync()` so callers can read the active set.
- Add a **direct-target fast path** to `travel_to` completion: when an objective names its destination outright (`worldSelector.worldId` and/or `mapSelector.mapId`), match arrival directly instead of routing through cluster tag/template resolution. This makes single-cluster and direct-target travel objectives complete even before cluster metadata is populated — and makes completion verifiable without a fully wired cluster. Tag/template selectors still resolve via `CrossWorldConstraintResolver`.
- Tests (the grain had **zero**): quest activation populates `ActiveQuestIds`; prerequisites gate activation; and the full loop — add quest → start → `player_arrived` → objective completes → quest moves to `CompletedQuestIds`.
- **Latent bug fixed (found by the new tests):** `OnActivateAsync` guarded identity initialization on `_state.State == null`, but Orleans always supplies a non-null default state — so `NarrativeId` was never set from the grain key, and every internal `GetGrain<INarrativeGrain>(NarrativeId)` threw on an empty primary key. Guard now keys off an unset `StateId`. This is why the grain worked in no real scenario and had no tests.

**Slice 2 — player-facing surface + broader objectives (follow-up, not this pass):**
- A player/agent activation path: `GameHub.AcceptQuest`/`ListAvailableQuests`/`GetQuestLog` (+ an agent tool and `aetherctl quest` command), resolving the narrative-state grain from the session's world like `ProcessNarrativeEventAsync` does.
- Objective completion for more of the existing types (`collect` on the already-emitted `item_collected` event, `reach_location`, `kill`).
- Emit `player_arrived` on `JoinWorld` (only `UsePortal` emits it today).

## Impact
- Affected specs: `narrative` (ADDED: quest activation & progression)
- Affected code: `Aetherium.Server/Narrative/State/INarrativeStateGrain.cs`, `NarrativeStateGrain.cs`; new `Aetherium.Test/Narrative/NarrativeStateGrainTests.cs`
- Build impact: additive grain API; no breaking changes. Existing `MarkQuestCompletedAsync`/`RecordEventAsync`/`HandlePlayerArrivedEventAsync` behavior is unchanged except that active quests now exist for them to act on.

## Status
Slice 1 implemented on `feat/phase5-quest-activation` (branched from `develop`). Verified: full solution build 0 errors; new `NarrativeStateGrainTests` (6) green — activation, prerequisites, and the full add→start→arrive→complete loop; **983 tests pass / 0 failed** (1 pre-existing seed-tolerant skip). Slice 2 (player-facing `GameHub`/tool/CLI surface, broader objective types, `JoinWorld` arrival) tracked but out of scope for this pass.
