using Orleans;
using Orleans.Runtime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Aetherium.Server.Agents.Analysis;
using Aetherium.Model.Analysis;
using Aetherium.Server.WorldGen.Adaptation;

using Aetherium.Model.Narrative;
namespace Aetherium.Server.Narrative
{
    /// <summary>
    /// Orleans grain implementation for managing game narratives with persistence.
    /// </summary>
    public class NarrativeGrain : Grain, INarrativeGrain
    {
        private readonly IPersistentState<NarrativeDefinition> _narrativeState;

        public NarrativeGrain(
            [PersistentState("narrative", "narrativeStore")] IPersistentState<NarrativeDefinition> narrativeState)
        {
            _narrativeState = narrativeState;
        }

        public override Task OnActivateAsync(CancellationToken cancellationToken)
        {
            // Initialize with defaults if needed
            if (_narrativeState.State == null)
            {
                _narrativeState.State = new NarrativeDefinition
                {
                    NarrativeId = this.GetPrimaryKeyString(),
                    Name = "Unnamed Narrative",
                    Description = string.Empty,
                    Quests = new List<QuestDefinition>(),
                    LootTables = new Dictionary<string, LootTable>(),
                    MonsterDensity = new Dictionary<string, MonsterDensityRule>(),
                    NPCGoals = new List<NPCGoalDefinition>()
                };
            }

            return base.OnActivateAsync(cancellationToken);
        }

        public Task<NarrativeDefinition?> GetNarrativeAsync()
        {
            return Task.FromResult<NarrativeDefinition?>(_narrativeState.State);
        }

        public async Task SetNarrativeAsync(NarrativeDefinition narrative)
        {
            // Ensure the ID matches the grain key
            narrative.NarrativeId = this.GetPrimaryKeyString();
            _narrativeState.State = narrative;
            await _narrativeState.WriteStateAsync();
        }

        public async Task AddOrUpdateQuestAsync(QuestDefinition quest)
        {
            var existing = _narrativeState.State.Quests.FirstOrDefault(q => q.QuestId == quest.QuestId);
            if (existing != null)
            {
                _narrativeState.State.Quests.Remove(existing);
            }

            _narrativeState.State.Quests.Add(quest);
            await _narrativeState.WriteStateAsync();
        }

        public async Task RemoveQuestAsync(string questId)
        {
            var quest = _narrativeState.State.Quests.FirstOrDefault(q => q.QuestId == questId);
            if (quest != null)
            {
                _narrativeState.State.Quests.Remove(quest);
                await _narrativeState.WriteStateAsync();
            }
        }

        public async Task AddOrUpdateLootTableAsync(string tableId, LootTable lootTable)
        {
            lootTable.TableId = tableId;
            _narrativeState.State.LootTables[tableId] = lootTable;
            await _narrativeState.WriteStateAsync();
        }

        public Task<LootTable?> GetLootTableAsync(string tableId)
        {
            _narrativeState.State.LootTables.TryGetValue(tableId, out var lootTable);
            return Task.FromResult<LootTable?>(lootTable);
        }

        public async Task AddOrUpdateMonsterDensityAsync(string zonePattern, MonsterDensityRule rule)
        {
            rule.ZonePattern = zonePattern;
            _narrativeState.State.MonsterDensity[zonePattern] = rule;
            await _narrativeState.WriteStateAsync();
        }

        public Task<MonsterDensityRule?> GetMonsterDensityAsync(string zonePattern)
        {
            _narrativeState.State.MonsterDensity.TryGetValue(zonePattern, out var rule);
            return Task.FromResult<MonsterDensityRule?>(rule);
        }

        public async Task AddOrUpdateNPCGoalAsync(NPCGoalDefinition goal)
        {
            var existing = _narrativeState.State.NPCGoals.FirstOrDefault(g => g.GoalId == goal.GoalId);
            if (existing != null)
            {
                _narrativeState.State.NPCGoals.Remove(existing);
            }

            _narrativeState.State.NPCGoals.Add(goal);
            await _narrativeState.WriteStateAsync();
        }

        public Task<List<NPCGoalDefinition>> GetNPCGoalsAsync()
        {
            return Task.FromResult(_narrativeState.State.NPCGoals);
        }

        public async Task DeleteAsync()
        {
            await _narrativeState.ClearStateAsync();
            _narrativeState.State = null!;
        }

        public async Task<List<QuestDefinition>> GetAdaptiveQuestsAsync(string agentId, int maxQuests = 5)
        {
            // Get adaptive narrative grain for this narrative
            var adaptiveGrain = GrainFactory.GetGrain<IAdaptiveNarrativeGrain>(this.GetPrimaryKeyString());
            return await adaptiveGrain.GenerateAdaptiveQuestsAsync(agentId, maxQuests);
        }

        public async Task<QuestDefinition?> AdaptQuestForAgentAsync(string questId, string agentId)
        {
            // Get existing quest
            var quest = _narrativeState.State.Quests.FirstOrDefault(q => q.QuestId == questId);
            if (quest == null)
            {
                return null;
            }

            // Get adaptive narrative grain for this narrative
            var adaptiveGrain = GrainFactory.GetGrain<IAdaptiveNarrativeGrain>(this.GetPrimaryKeyString());
            return await adaptiveGrain.AdaptQuestAsync(questId, agentId);
        }
    }
}


