using Orleans;
using Orleans.Runtime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Aetherium.Server.Narrative;
using Aetherium.Model.Narrative;
using Aetherium.Server.Narrative.CrossWorld;
using Aetherium.Server.MultiWorld;
using Aetherium.Server.MetaProgression;

using Aetherium.Model.Narrative;
namespace Aetherium.Server.Narrative.State
{
    /// <summary>
    /// Orleans grain implementation for managing runtime narrative state with persistence.
    /// </summary>
    public class NarrativeStateGrain : Grain, INarrativeStateGrain
    {
        private readonly IPersistentState<NarrativeState> _state;

        public NarrativeStateGrain(
            [PersistentState("narrativeState", "narrativeStore")] IPersistentState<NarrativeState> state)
        {
            _state = state;
        }

        public override Task OnActivateAsync(CancellationToken cancellationToken)
        {
            // Orleans always hands back a non-null default state instance when nothing is persisted
            // yet, so a `== null` check never fires and the key-derived identity was never set —
            // leaving NarrativeId empty and every internal GetGrain<INarrativeGrain>(NarrativeId)
            // throwing on an empty primary key. Initialize identity whenever the state is fresh
            // (StateId not yet assigned).
            if (_state.State == null)
            {
                _state.State = new NarrativeState();
            }

            if (string.IsNullOrEmpty(_state.State.StateId))
            {
                var key = this.GetPrimaryKeyString();
                _state.State.StateId = key;
                _state.State.NarrativeId = ExtractNarrativeId(key);
                _state.State.WorldId = ExtractWorldId(key);
            }

            return base.OnActivateAsync(cancellationToken);
        }

        public Task<NarrativeState?> GetStateAsync()
        {
            return Task.FromResult<NarrativeState?>(_state.State);
        }

        public async Task MarkQuestCompletedAsync(string questId)
        {
            if (_state.State == null)
                return;

            _state.State.CompletedQuestIds.Add(questId);
            _state.State.ActiveQuestIds.Remove(questId);

            await _state.WriteStateAsync();

            // Record quest completion in meta-progression
            try
            {
                // Determine if this is a cross-world quest by checking objectives
                var narrativeGrain = GrainFactory.GetGrain<INarrativeGrain>(_state.State.NarrativeId);
                var narrative = await narrativeGrain.GetNarrativeAsync();
                
                bool isCrossWorld = false;
                QuestDefinition? quest = null;
                
                if (narrative != null)
                {
                    quest = narrative.Quests.FirstOrDefault(q => q.QuestId == questId);
                }
                
                if (quest == null)
                {
                    quest = _state.State.GeneratedQuests.FirstOrDefault(q => q.QuestId == questId);
                }

                if (quest != null && quest.Objectives != null)
                {
                    // Check if quest has any travel_to objectives
                    isCrossWorld = quest.Objectives.Any(obj => obj.Type == "travel_to");
                }

                // Get player ID from state (use world ID as a fallback)
                // Note: In a full implementation, you'd get the player ID from context
                var playerId = _state.State.WorldId ?? _state.State.StateId;
                var metaProgGrain = GrainFactory.GetGrain<IMetaProgressionGrain>(playerId);
                await metaProgGrain.RecordQuestCompletionAsync(questId, isCrossWorld);
            }
            catch (Exception ex)
            {
                // Non-breaking: log but don't fail quest completion
                Console.WriteLine($"[NarrativeStateGrain] Failed to record quest completion: {ex.Message}");
            }
        }

        public async Task RecordEventAsync(string eventType, Dictionary<string, object> eventData)
        {
            if (_state.State == null)
                return;

            var narrativeEvent = new NarrativeEvent
            {
                EventType = eventType,
                EventData = eventData ?? new Dictionary<string, object>()
            };

            _state.State.Events.Add(narrativeEvent);

            // Keep only last 1000 events to prevent unbounded growth
            if (_state.State.Events.Count > 1000)
            {
                _state.State.Events.RemoveAt(0);
            }

            // Advance any active-quest objectives this event can progress or complete
            // (travel_to / reach_location on arrival, collect on item_collected, kill on
            // enemy_defeated). Prior to this only travel_to on player_arrived was handled.
            await AdvanceObjectivesForEventAsync(eventType, eventData ?? new Dictionary<string, object>());

            await _state.WriteStateAsync();
        }

        /// <summary>
        /// Walks every active quest's incomplete objectives and lets the given event advance
        /// or complete them. When an objective completes and it was the quest's last, the quest
        /// is marked complete. Count-based objectives (collect/kill) accumulate in
        /// <see cref="NarrativeState.ObjectiveProgress"/> until they reach their required count.
        /// </summary>
        private async Task AdvanceObjectivesForEventAsync(string eventType, Dictionary<string, object> eventData)
        {
            if (_state.State == null)
                return;

            // Snapshot: MarkQuestCompletedAsync mutates ActiveQuestIds mid-iteration.
            var activeQuestIds = _state.State.ActiveQuestIds.ToList();
            if (activeQuestIds.Count == 0)
                return;

            foreach (var questId in activeQuestIds)
            {
                var quest = await FindQuestAsync(questId);
                if (quest?.Objectives == null || quest.Objectives.Count == 0)
                    continue;

                bool anyObjectiveCompleted = false;

                foreach (var objective in quest.Objectives)
                {
                    // Skip objectives already completed.
                    if (_state.State.CompletedObjectives.TryGetValue(questId, out var done) &&
                        done.Contains(objective.ObjectiveId))
                        continue;

                    if (await TryCompleteObjectiveAsync(questId, objective, eventType, eventData))
                        anyObjectiveCompleted = true;
                }

                // Only re-check quest completion when an objective actually finished.
                if (anyObjectiveCompleted && AreAllObjectivesComplete(quest))
                    await MarkQuestCompletedAsync(questId);
            }
        }

        /// <summary>
        /// Attempts to complete a single objective in response to an event. Returns true only when
        /// the objective transitions to complete (a mere progress increment on a counting objective
        /// returns false — its progress is still persisted by the caller).
        /// </summary>
        private async Task<bool> TryCompleteObjectiveAsync(
            string questId, QuestObjective objective, string eventType, Dictionary<string, object> eventData)
        {
            switch (objective.Type?.ToLowerInvariant())
            {
                case "travel_to":
                    if (eventType != "player_arrived")
                        return false;
                    return await TryCompleteTravelToAsync(questId, objective, eventData);

                case "reach_location":
                    if (eventType != "player_arrived" && eventType != "location_reached")
                        return false;
                    if (!MatchesReachLocation(objective, eventData))
                        return false;
                    MarkObjectiveComplete(questId, objective.ObjectiveId);
                    return true;

                case "collect":
                    if (eventType != "item_collected")
                        return false;
                    return TryAdvanceCountingObjective(questId, objective, eventData, MatchesCollect);

                case "kill":
                    if (eventType != "enemy_defeated")
                        return false;
                    return TryAdvanceCountingObjective(questId, objective, eventData, MatchesKill);

                default:
                    return false;
            }
        }

        private async Task<bool> TryCompleteTravelToAsync(
            string questId, QuestObjective objective, Dictionary<string, object> eventData)
        {
            // travel_to matching requires both a world and a map on the arrival event.
            if (!eventData.TryGetValue("worldId", out var worldIdObj) ||
                !eventData.TryGetValue("mapId", out var mapIdObj))
                return false;

            var arrivedWorldId = worldIdObj?.ToString();
            var arrivedMapId = mapIdObj?.ToString();
            if (string.IsNullOrEmpty(arrivedWorldId) || string.IsNullOrEmpty(arrivedMapId))
                return false;

            if (await IsTravelToObjectiveCompleteAsync(objective, arrivedWorldId, arrivedMapId))
            {
                MarkObjectiveComplete(questId, objective.ObjectiveId);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Advances a count-based objective (collect/kill) by one when <paramref name="matcher"/>
        /// accepts the event, completing it once the accumulated count reaches the required count.
        /// </summary>
        private bool TryAdvanceCountingObjective(
            string questId,
            QuestObjective objective,
            Dictionary<string, object> eventData,
            Func<QuestObjective, Dictionary<string, object>, bool> matcher)
        {
            if (!matcher(objective, eventData))
                return false;

            int required = GetRequiredCount(objective);
            int current = IncrementProgress(questId, objective.ObjectiveId);

            if (current >= required)
            {
                MarkObjectiveComplete(questId, objective.ObjectiveId);
                return true;
            }
            // Progressed but not yet complete — progress is persisted by the caller.
            return false;
        }

        private void MarkObjectiveComplete(string questId, string objectiveId)
        {
            if (_state.State == null)
                return;

            if (!_state.State.CompletedObjectives.TryGetValue(questId, out var set))
            {
                set = new HashSet<string>();
                _state.State.CompletedObjectives[questId] = set;
            }
            set.Add(objectiveId);

            // Once complete, partial progress is no longer needed.
            if (_state.State.ObjectiveProgress.TryGetValue(questId, out var prog))
                prog.Remove(objectiveId);
        }

        private int IncrementProgress(string questId, string objectiveId)
        {
            if (_state.State == null)
                return 0;

            if (!_state.State.ObjectiveProgress.TryGetValue(questId, out var prog))
            {
                prog = new Dictionary<string, int>();
                _state.State.ObjectiveProgress[questId] = prog;
            }
            int next = (prog.TryGetValue(objectiveId, out var c) ? c : 0) + 1;
            prog[objectiveId] = next;
            return next;
        }

        private static bool MatchesCollect(QuestObjective objective, Dictionary<string, object> eventData)
        {
            var wantType = GetStringParam(objective.Parameters, "itemType");
            if (string.IsNullOrEmpty(wantType))
                return true; // any collected item counts

            var gotType = GetStringValue(eventData, "itemType") ?? GetStringValue(eventData, "itemId");
            return !string.IsNullOrEmpty(gotType) &&
                   string.Equals(gotType, wantType, StringComparison.OrdinalIgnoreCase);
        }

        private static bool MatchesKill(QuestObjective objective, Dictionary<string, object> eventData)
        {
            var want = GetStringParam(objective.Parameters, "enemyType")
                       ?? GetStringParam(objective.Parameters, "target");
            if (string.IsNullOrEmpty(want))
                return true; // any defeated enemy counts

            var got = GetStringValue(eventData, "enemyType")
                      ?? GetStringValue(eventData, "target")
                      ?? GetStringValue(eventData, "enemyId");
            return !string.IsNullOrEmpty(got) &&
                   string.Equals(got, want, StringComparison.OrdinalIgnoreCase);
        }

        private static bool MatchesReachLocation(QuestObjective objective, Dictionary<string, object> eventData)
        {
            // An explicit world/map target takes priority and matches exactly.
            var wantWorld = GetStringParam(objective.Parameters, "worldId");
            var wantMap = GetStringParam(objective.Parameters, "mapId");
            if (!string.IsNullOrEmpty(wantWorld) || !string.IsNullOrEmpty(wantMap))
            {
                var gotWorld = GetStringValue(eventData, "worldId");
                var gotMap = GetStringValue(eventData, "mapId");
                bool worldOk = string.IsNullOrEmpty(wantWorld) ||
                               string.Equals(wantWorld, gotWorld, StringComparison.OrdinalIgnoreCase);
                bool mapOk = string.IsNullOrEmpty(wantMap) ||
                             string.Equals(wantMap, gotMap, StringComparison.OrdinalIgnoreCase);
                return worldOk && mapOk;
            }

            // Otherwise fuzzy-match a locationHint against common arrival fields.
            var hint = GetStringParam(objective.Parameters, "locationHint")
                       ?? GetStringParam(objective.Parameters, "location");
            if (string.IsNullOrEmpty(hint))
                return true; // no constraint → any arrival satisfies it

            foreach (var key in new[] { "location", "zone", "locationHint", "mapId", "mapName", "worldId" })
            {
                var val = GetStringValue(eventData, key);
                if (!string.IsNullOrEmpty(val) &&
                    val.IndexOf(hint, StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }
            return false;
        }

        private static string? GetStringParam(Dictionary<string, object>? parameters, string key)
            => parameters != null && parameters.TryGetValue(key, out var v) ? v?.ToString() : null;

        private static string? GetStringValue(Dictionary<string, object>? data, string key)
            => data != null && data.TryGetValue(key, out var v) ? v?.ToString() : null;

        private static int GetRequiredCount(QuestObjective objective)
        {
            foreach (var key in new[] { "requiredCount", "count", "requiredQuantity", "quantity" })
            {
                if (objective.Parameters != null &&
                    objective.Parameters.TryGetValue(key, out var v) &&
                    TryToInt(v, out var n) && n > 0)
                    return n;
            }
            return 1;
        }

        private static bool TryToInt(object? value, out int result)
        {
            switch (value)
            {
                case int i: result = i; return true;
                case long l: result = (int)l; return true;
                case double d: result = (int)d; return true;
                default:
                    return int.TryParse(value?.ToString(), out result);
            }
        }

        /// <summary>
        /// Checks if a travel_to objective matches the arrival location.
        /// </summary>
        private async Task<bool> IsTravelToObjectiveCompleteAsync(
            QuestObjective objective,
            string arrivedWorldId,
            string arrivedMapId)
        {
            if (objective.Parameters == null || objective.Parameters.Count == 0)
                return false;

            // Extract constraint from objective parameters
            var constraint = ExtractCrossWorldConstraintFromObjective(objective);
            if (constraint == null)
                return false;

            // Direct-target fast path: when the objective names its destination outright (a world id
            // and/or map id), match arrival against it directly. This completes single-cluster and
            // direct-target travel objectives even before cluster metadata is populated, and avoids a
            // grain round-trip. Tag/template selectors fall through to the cluster resolver below.
            var directWorldId = constraint.WorldSelector?.WorldId;
            var directMapId = constraint.MapSelector?.MapId;
            if (!string.IsNullOrEmpty(directWorldId) || !string.IsNullOrEmpty(directMapId))
            {
                bool worldMatches = string.IsNullOrEmpty(directWorldId) || directWorldId == arrivedWorldId;
                bool mapMatches = string.IsNullOrEmpty(directMapId) || directMapId == arrivedMapId;
                return worldMatches && mapMatches;
            }

            // Get cluster ID from world
            var worldGrain = GrainFactory.GetGrain<IWorldGrain>(arrivedWorldId);
            var worldInfo = await worldGrain.GetInfoAsync();

            if (worldInfo == null || string.IsNullOrEmpty(worldInfo.ClusterId))
                return false;

            // Resolve constraint to target location
            var (targetWorldId, targetMapId) = await CrossWorldConstraintResolver.ResolveTargetAsync(
                constraint,
                worldInfo.ClusterId,
                GrainFactory);

            // Check if arrival matches target
            return targetWorldId == arrivedWorldId && targetMapId == arrivedMapId;
        }

        /// <summary>
        /// Extracts a CrossWorldConstraint from a quest objective's parameters.
        /// </summary>
        private static CrossWorldConstraint? ExtractCrossWorldConstraintFromObjective(QuestObjective objective)
        {
            if (objective.Parameters == null)
                return null;

            var constraint = new CrossWorldConstraint();

            // Extract world selector
            if (objective.Parameters.TryGetValue("worldSelector", out var worldSelObj) &&
                worldSelObj is Dictionary<string, object> worldSelDict)
            {
                constraint.WorldSelector = new WorldSelector
                {
                    WorldId = worldSelDict.TryGetValue("worldId", out var wid) ? wid?.ToString() : null,
                    WorldTag = worldSelDict.TryGetValue("worldTag", out var wtag) ? wtag?.ToString() : null,
                    WorldTemplate = worldSelDict.TryGetValue("worldTemplate", out var wtmpl) ? wtmpl?.ToString() : null
                };

                if (worldSelDict.TryGetValue("excludeWorldIds", out var exclObj) &&
                    exclObj is List<string> excludeList)
                {
                    constraint.WorldSelector.ExcludeWorldIds = excludeList;
                }
            }

            // Extract map selector
            if (objective.Parameters.TryGetValue("mapSelector", out var mapSelObj) &&
                mapSelObj is Dictionary<string, object> mapSelDict)
            {
                constraint.MapSelector = new MapSelector
                {
                    MapId = mapSelDict.TryGetValue("mapId", out var mid) ? mid?.ToString() : null,
                    MapTag = mapSelDict.TryGetValue("mapTag", out var mtag) ? mtag?.ToString() : null,
                    MapName = mapSelDict.TryGetValue("mapName", out var mname) ? mname?.ToString() : null
                };
            }

            // Extract requires unlock
            if (objective.Parameters.TryGetValue("requiresUnlock", out var reqUnlockObj))
            {
                constraint.RequiresUnlock = reqUnlockObj is bool reqUnlock ? reqUnlock :
                                             bool.TryParse(reqUnlockObj?.ToString(), out var parsed) && parsed;
            }

            // Return constraint if we have at least a world or map selector
            return (constraint.WorldSelector != null || constraint.MapSelector != null) ? constraint : null;
        }

        /// <summary>
        /// Checks if all objectives for a quest are complete.
        /// </summary>
        private bool AreAllObjectivesComplete(QuestDefinition quest)
        {
            if (_state.State == null || quest.Objectives == null || quest.Objectives.Count == 0)
                return false;

            if (!_state.State.CompletedObjectives.TryGetValue(quest.QuestId, out var completed))
                return false;

            return quest.Objectives.All(obj => completed.Contains(obj.ObjectiveId));
        }

        public async Task AddGeneratedQuestAsync(QuestDefinition quest)
        {
            if (_state.State == null)
                return;

            // Remove existing quest with same ID if present
            _state.State.GeneratedQuests.RemoveAll(q => q.QuestId == quest.QuestId);
            _state.State.GeneratedQuests.Add(quest);

            // The consequence engine generates quests on every qualifying event with no
            // natural bound; keep the most recent and let stale offers fall off.
            const int maxGeneratedQuests = 500;
            if (_state.State.GeneratedQuests.Count > maxGeneratedQuests)
            {
                _state.State.GeneratedQuests.RemoveRange(
                    0, _state.State.GeneratedQuests.Count - maxGeneratedQuests);
            }

            await _state.WriteStateAsync();
        }

        public async Task<List<QuestDefinition>> GetAvailableQuestsAsync()
        {
            if (_state.State == null)
                return new List<QuestDefinition>();

            // Get base narrative
            var narrativeGrain = GrainFactory.GetGrain<INarrativeGrain>(_state.State.NarrativeId);
            var narrative = await narrativeGrain.GetNarrativeAsync();

            var availableQuests = new List<QuestDefinition>();

            // Add base narrative quests
            if (narrative != null)
            {
                foreach (var quest in narrative.Quests)
                {
                    if (await CanStartQuestAsync(quest.QuestId))
                    {
                        availableQuests.Add(quest);
                    }
                }
            }

            // Add generated quests
            foreach (var quest in _state.State.GeneratedQuests)
            {
                if (await CanStartQuestAsync(quest.QuestId))
                {
                    availableQuests.Add(quest);
                }
            }

            return availableQuests;
        }

        public Task<HashSet<string>> GetCompletedQuestIdsAsync()
        {
            if (_state.State == null)
                return Task.FromResult(new HashSet<string>());

            return Task.FromResult(_state.State.CompletedQuestIds);
        }

        public async Task<bool> CanStartQuestAsync(string questId)
        {
            if (_state.State == null)
                return false;

            // Already completed
            if (_state.State.CompletedQuestIds.Contains(questId))
                return false;

            // Already active
            if (_state.State.ActiveQuestIds.Contains(questId))
                return false;

            // Check prerequisites
            var narrativeGrain = GrainFactory.GetGrain<INarrativeGrain>(_state.State.NarrativeId);
            var narrative = await narrativeGrain.GetNarrativeAsync();

            if (narrative != null)
            {
                var quest = narrative.Quests.FirstOrDefault(q => q.QuestId == questId);
                if (quest != null && quest.PrerequisiteQuestIds != null)
                {
                    foreach (var prereqId in quest.PrerequisiteQuestIds)
                    {
                        if (!_state.State.CompletedQuestIds.Contains(prereqId))
                            return false;
                    }
                }
            }

            // Check generated quests
            var generatedQuest = _state.State.GeneratedQuests.FirstOrDefault(q => q.QuestId == questId);
            if (generatedQuest != null && generatedQuest.PrerequisiteQuestIds != null)
            {
                foreach (var prereqId in generatedQuest.PrerequisiteQuestIds)
                {
                    if (!_state.State.CompletedQuestIds.Contains(prereqId))
                        return false;
                }
            }

            return true;
        }

        public async Task<bool> StartQuestAsync(string questId)
        {
            if (_state.State == null || string.IsNullOrEmpty(questId))
                return false;

            // CanStartQuestAsync enforces: not already completed, not already active, prerequisites met.
            if (!await CanStartQuestAsync(questId))
                return false;

            var quest = await FindQuestAsync(questId);
            if (quest == null)
                return false; // unknown quest id

            _state.State.ActiveQuestIds.Add(questId);
            _state.State.ActiveQuestObjectives[questId] =
                quest.Objectives?.Select(o => o.ObjectiveId).ToList() ?? new List<string>();
            if (!_state.State.CompletedObjectives.ContainsKey(questId))
                _state.State.CompletedObjectives[questId] = new HashSet<string>();

            await _state.WriteStateAsync();
            return true;
        }

        public Task<HashSet<string>> GetActiveQuestIdsAsync()
        {
            return Task.FromResult(_state.State?.ActiveQuestIds ?? new HashSet<string>());
        }

        public async Task<List<QuestDefinition>> GetActiveQuestsAsync()
        {
            var result = new List<QuestDefinition>();
            if (_state.State == null)
                return result;

            foreach (var questId in _state.State.ActiveQuestIds)
            {
                var quest = await FindQuestAsync(questId);
                if (quest != null)
                    result.Add(quest);
            }
            return result;
        }

        /// <summary>
        /// Resolves a quest definition by id from the base narrative or the generated quests.
        /// </summary>
        private async Task<QuestDefinition?> FindQuestAsync(string questId)
        {
            if (_state.State == null)
                return null;

            var narrativeGrain = GrainFactory.GetGrain<INarrativeGrain>(_state.State.NarrativeId);
            var narrative = await narrativeGrain.GetNarrativeAsync();
            var quest = narrative?.Quests?.FirstOrDefault(q => q.QuestId == questId);
            return quest ?? _state.State.GeneratedQuests.FirstOrDefault(q => q.QuestId == questId);
        }

        public async Task UpdateRelationshipAsync(string npc1Id, string npc2Id, float relationshipValue)
        {
            if (_state.State == null)
                return;

            if (!_state.State.Relationships.ContainsKey(npc1Id))
            {
                _state.State.Relationships[npc1Id] = new Dictionary<string, float>();
            }

            _state.State.Relationships[npc1Id][npc2Id] = relationshipValue;

            // Relationships are symmetric by default
            if (!_state.State.Relationships.ContainsKey(npc2Id))
            {
                _state.State.Relationships[npc2Id] = new Dictionary<string, float>();
            }

            _state.State.Relationships[npc2Id][npc1Id] = relationshipValue;

            await _state.WriteStateAsync();
        }

        public Task<float?> GetRelationshipAsync(string npc1Id, string npc2Id)
        {
            if (_state.State == null)
                return Task.FromResult<float?>(null);

            if (_state.State.Relationships.TryGetValue(npc1Id, out var relationships))
            {
                if (relationships.TryGetValue(npc2Id, out var value))
                {
                    return Task.FromResult<float?>(value);
                }
            }

            return Task.FromResult<float?>(null);
        }

        /// <summary>
        /// Extracts narrative ID from grain key.
        /// Format: "narrativeId" or "worldId:narrativeId"
        /// </summary>
        private static string ExtractNarrativeId(string grainKey)
        {
            var parts = grainKey.Split(':', 2);
            return parts.Length == 2 ? parts[1] : grainKey;
        }

        /// <summary>
        /// Extracts world ID from grain key if present.
        /// Format: "narrativeId" or "worldId:narrativeId"
        /// </summary>
        private static string? ExtractWorldId(string grainKey)
        {
            var parts = grainKey.Split(':', 2);
            return parts.Length == 2 ? parts[0] : null;
        }
    }
}

