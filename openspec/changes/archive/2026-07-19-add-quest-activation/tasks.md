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

## Slice 2 — player surface + broader objectives (this pass)

### 4. Broader objective completion
- [x] 4.3 Generalize `RecordEventAsync` → `AdvanceObjectivesForEventAsync`: `collect` (on `item_collected`, count-based via new `ObjectiveProgress`), `kill` (on `enemy_defeated`, count-based), `reach_location` (on `player_arrived`/`location_reached`, explicit world/map or fuzzy locationHint). Existing `travel_to` path preserved verbatim.
- [x] 4.3a Add `NarrativeState.ObjectiveProgress` (`[Id(10)]`, QuestId→ObjectiveId→count) for partial progress on count-based objectives.

### 5. Player-facing surface
- [x] 5.1 `GameHub.ListAvailableQuests` / `AcceptQuest` / `GetQuestLog` resolving the narrative-state grain from the session world; new `QuestSummaryDto`/`QuestObjectiveDto`/`QuestLogDto`.
- [x] 5.2 Shared `NarrativeStateResolver` (worldId → narrativeId → scope → grain) reused by hub, tools, and CLI.
- [x] 5.3 `GetActiveQuestsAsync()` on the state grain for the quest log.

### 6. Agent tools + CLI
- [x] 6.1 Player-profile quest tools: `list_quests`, `accept_quest`, `quest_log` (category `quest`); add `quest` to the Player profile.
- [x] 6.2 `aetherctl quest available|accept|log <worldId>` command.

### 7. Arrival on join
- [x] 7.1 Emit `player_arrived` on `GameHub.JoinWorld` (previously only `UsePortal` emitted it).

### 8. Tests
- [x] 8.1 Grain: collect (single + count accumulation + non-matching item ignored), kill (count), reach_location (hint match + unrelated arrival negative) — 12 grain tests total.
- [x] 8.2 Profile: quest tools reachable by Player, denied to Explorer.
- [x] 8.3 Full solution build + suite green.

## Deferred (future)
- `kill` completion has no production event yet — combat (P3-7) will emit `enemy_defeated`; the grain path is implemented and unit-tested now so it activates as soon as combat lands.
- Rich reward granting on quest completion, and objective types `talk_to` / `explore` / `rescue` / `defend`, remain unwired.
