using Orleans;
using Orleans.Runtime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Aetherium.Server.Narrative;

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

            await _state.WriteStateAsync();
        }

        public async Task AddGeneratedQuestAsync(QuestDefinition quest)
        {
            if (_state.State == null)
                return;

            // Remove existing quest with same ID if present
            _state.State.GeneratedQuests.RemoveAll(q => q.QuestId == quest.QuestId);
            _state.State.GeneratedQuests.Add(quest);

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

