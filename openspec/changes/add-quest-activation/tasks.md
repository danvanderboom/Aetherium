## Slice 1 — grain activation + travel_to completion (this pass)

### 1. Grain API
- [x] 1.1 Add `StartQuestAsync(questId)` to `INarrativeStateGrain` + `NarrativeStateGrain` (validate via `CanStartQuestAsync`, resolve quest def, populate `ActiveQuestIds` / `ActiveQuestObjectives` / `CompletedObjectives`, persist, return bool)
- [x] 1.2 Add `GetActiveQuestIdsAsync()`
- [x] 1.3 Extract a `FindQuestAsync(questId)` helper (base narrative + generated) used by activation
- [x] 1.4 Fix `OnActivateAsync` identity init (guarded on `== null`, which never fires under Orleans; now keys off unset `StateId`) — latent bug that left `NarrativeId` empty

### 2. travel_to completion
- [x] 2.1 Add direct-target fast path to `IsTravelToObjectiveCompleteAsync` (match `worldSelector.worldId`/`mapSelector.mapId` directly; keep the resolver path for tag/template selectors)

### 3. Tests
- [x] 3.1 Activation: `StartQuestAsync` populates `ActiveQuestIds`; returns false for unknown/already-active
- [x] 3.2 Prerequisites gate activation (`StartQuestAsync` false until prereq completed)
- [x] 3.3 Full loop: add quest (travel_to, direct target) → start → `RecordEventAsync("player_arrived")` → objective in `CompletedObjectives`, quest in `CompletedQuestIds`, removed from `ActiveQuestIds` (+ negative case: wrong arrival doesn't complete)
- [x] 3.4 Full solution build + suite green (983 passed / 0 failed / 1 seed-tolerant skip)

## Slice 2 — player surface + broader objectives (follow-up, not this pass)
- [ ] 4.1 `GameHub.AcceptQuest` / `ListAvailableQuests` / `GetQuestLog` (resolve narrative-state grain from session world)
- [ ] 4.2 Agent tool + `aetherctl quest` command
- [ ] 4.3 Objective completion for `collect` (on `item_collected`), `reach_location`, `kill`
- [ ] 4.4 Emit `player_arrived` on `JoinWorld`
