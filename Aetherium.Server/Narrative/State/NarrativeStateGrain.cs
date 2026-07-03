using Orleans;
using Orleans.Runtime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Aetherium.Server.Narrative;
using Aetherium.Server.Narrative.CrossWorld;
using Aetherium.Server.MultiWorld;
using Aetherium.Server.MetaProgression;

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
            // Initialize with defaults if needed
            if (_state.State == null)
            {
                _state.State = new NarrativeState
                {
                    StateId = this.GetPrimaryKeyString(),
                    NarrativeId = ExtractNarrativeId(this.GetPrimaryKeyString()),
                    WorldId = ExtractWorldId(this.GetPrimaryKeyString())
                };
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

            // Handle player_arrived events for travel_to objectives
            if (eventType == "player_arrived" && eventData != null)
            {
                await HandlePlayerArrivedEventAsync(eventData);
            }

            await _state.WriteStateAsync();
        }

        /// <summary>
        /// Handles player_arrived events by checking if any travel_to objectives are complete.
        /// </summary>
        private async Task HandlePlayerArrivedEventAsync(Dictionary<string, object> eventData)
        {
            if (_state.State == null)
                return;

            // Extract arrival location from event
            if (!eventData.TryGetValue("worldId", out var worldIdObj) ||
                !eventData.TryGetValue("mapId", out var mapIdObj))
                return;

            var arrivedWorldId = worldIdObj?.ToString();
            var arrivedMapId = mapIdObj?.ToString();

            if (string.IsNullOrEmpty(arrivedWorldId) || string.IsNullOrEmpty(arrivedMapId))
                return;

            // Get all active quests and check for travel_to objectives
            var activeQuestIds = _state.State.ActiveQuestIds.ToList();
            
            foreach (var questId in activeQuestIds)
            {
                // Get quest definition
                var narrativeGrain = GrainFactory.GetGrain<INarrativeGrain>(_state.State.NarrativeId);
                var narrative = await narrativeGrain.GetNarrativeAsync();
                
                QuestDefinition? quest = null;
                if (narrative != null)
                {
                    quest = narrative.Quests.FirstOrDefault(q => q.QuestId == questId);
                }
                
                if (quest == null)
                {
                    quest = _state.State.GeneratedQuests.FirstOrDefault(q => q.QuestId == questId);
                }

                if (quest == null || quest.Objectives == null)
                    continue;

                // Check each objective for travel_to type
                foreach (var objective in quest.Objectives)
                {
                    if (objective.Type != "travel_to")
                        continue;

                    // Check if this objective is already completed
                    if (_state.State.CompletedObjectives.TryGetValue(questId, out var completed) &&
                        completed.Contains(objective.ObjectiveId))
                        continue;

                    // Check if arrival matches this objective's target
                    if (await IsTravelToObjectiveCompleteAsync(objective, arrivedWorldId, arrivedMapId))
                    {
                        // Mark objective as complete
                        if (!_state.State.CompletedObjectives.ContainsKey(questId))
                        {
                            _state.State.CompletedObjectives[questId] = new HashSet<string>();
                        }
                        
                        _state.State.CompletedObjectives[questId].Add(objective.ObjectiveId);

                        // Check if all objectives are complete
                        if (AreAllObjectivesComplete(quest))
                        {
                            await MarkQuestCompletedAsync(questId);
                        }
                    }
                }
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

