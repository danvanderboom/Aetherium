using Orleans;
using Orleans.Runtime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Aetherium.Server.MetaProgression
{
    /// <summary>
    /// Orleans grain implementation for tracking player meta-progression across multiple worlds.
    /// </summary>
    public class MetaProgressionGrain : Grain, IMetaProgressionGrain
    {
        private readonly IPersistentState<MetaProgressionState> _state;

        public MetaProgressionGrain(
            [PersistentState("metaProgression", "metaStore")] IPersistentState<MetaProgressionState> state)
        {
            _state = state;
        }

        public override Task OnActivateAsync(CancellationToken cancellationToken)
        {
            if (_state.State == null)
            {
                _state.State = new MetaProgressionState
                {
                    PlayerId = this.GetPrimaryKeyString(),
                    UnlockedGenerators = new HashSet<string> { "PerlinTerrain", "BasicDungeon" } // Start with basic generators unlocked
                };
            }
            else if (_state.State.UnlockedGenerators.Count == 0)
            {
                // Backfill defaults if state exists but has no unlocks (for legacy tests/state)
                _state.State.UnlockedGenerators.Add("PerlinTerrain");
                _state.State.UnlockedGenerators.Add("BasicDungeon");
            }

            return base.OnActivateAsync(cancellationToken);
        }

        public async Task RecordDiscoveryAsync(string worldId, string mapId, string? worldTemplate = null, List<string>? tags = null)
        {
            if (_state.State == null)
                return;

            _state.State.VisitedWorldIds.Add(worldId);
            _state.State.VisitedMapIds.Add(mapId);

            if (!string.IsNullOrEmpty(worldTemplate))
            {
                _state.State.DiscoveredWorldTemplates.Add(worldTemplate);
            }

            if (tags != null)
            {
                foreach (var tag in tags)
                {
                    _state.State.DiscoveredTags.Add(tag);

                    // Track tag visit counts
                    if (!_state.State.TagVisitCounts.ContainsKey(tag))
                    {
                        _state.State.TagVisitCounts[tag] = 0;
                    }
                    _state.State.TagVisitCounts[tag]++;
                }
            }

            _state.State.LastUpdatedAt = DateTime.UtcNow;
            await _state.WriteStateAsync();

            // Evaluate unlocks after recording discovery
            await EvaluateUnlocksAsync();
        }

        public async Task RecordQuestCompletionAsync(string questId, bool isCrossWorld)
        {
            if (_state.State == null)
                return;

            _state.State.CompletedQuestIds.Add(questId);

            if (isCrossWorld)
            {
                _state.State.CompletedCrossWorldQuestIds.Add(questId);
            }

            _state.State.LastUpdatedAt = DateTime.UtcNow;
            await _state.WriteStateAsync();

            // Evaluate unlocks after recording quest completion
            await EvaluateUnlocksAsync();
        }

        public async Task EvaluateUnlocksAsync()
        {
            if (_state.State == null)
                return;

            var newlyUnlocked = new HashSet<string>();

            foreach (var criteria in _state.State.UnlockCriteriaDefinitions.Values)
            {
                // Skip if already unlocked
                if (_state.State.UnlockedGenerators.Contains(criteria.UnlocksGenerator))
                    continue;

                // Check if criteria is met
                if (IsCriteriaMet(criteria))
                {
                    newlyUnlocked.Add(criteria.UnlocksGenerator);
                }
            }

            // Add newly unlocked generators
            foreach (var generator in newlyUnlocked)
            {
                _state.State.UnlockedGenerators.Add(generator);
            }

            if (newlyUnlocked.Count > 0)
            {
                _state.State.LastUpdatedAt = DateTime.UtcNow;
                await _state.WriteStateAsync();
            }
        }

        private bool IsCriteriaMet(UnlockCriteria criteria)
        {
            if (_state.State == null)
                return false;

            // Check minimum world visits
            if (criteria.MinWorldVisits.HasValue)
            {
                if (_state.State.VisitedWorldIds.Count < criteria.MinWorldVisits.Value)
                    return false;
            }

            // Check minimum worlds of specific tag
            if (criteria.MinWorldsOfTag.HasValue && !string.IsNullOrEmpty(criteria.RequiredTag))
            {
                var tagVisitCount = _state.State.TagVisitCounts.TryGetValue(criteria.RequiredTag, out var count) ? count : 0;
                if (tagVisitCount < criteria.MinWorldsOfTag.Value)
                    return false;
            }

            // Check required tag discovery
            if (!string.IsNullOrEmpty(criteria.RequiredTag))
            {
                if (!_state.State.DiscoveredTags.Contains(criteria.RequiredTag))
                    return false;
            }

            // Check minimum cross-world quests
            if (criteria.MinCrossWorldQuests.HasValue)
            {
                if (_state.State.CompletedCrossWorldQuestIds.Count < criteria.MinCrossWorldQuests.Value)
                    return false;
            }

            // Check required quest IDs
            if (criteria.RequiredQuestIds != null && criteria.RequiredQuestIds.Count > 0)
            {
                foreach (var questId in criteria.RequiredQuestIds)
                {
                    if (!_state.State.CompletedQuestIds.Contains(questId))
                        return false;
                }
            }

            // Check required world templates
            if (criteria.RequiredWorldTemplates != null && criteria.RequiredWorldTemplates.Count > 0)
            {
                foreach (var template in criteria.RequiredWorldTemplates)
                {
                    if (!_state.State.DiscoveredWorldTemplates.Contains(template))
                        return false;
                }
            }

            // Check tag visit requirements
            if (criteria.TagVisitRequirements != null && criteria.TagVisitRequirements.Count > 0)
            {
                foreach (var kvp in criteria.TagVisitRequirements)
                {
                    var tag = kvp.Key;
                    var requiredCount = kvp.Value;
                    var actualCount = _state.State.TagVisitCounts.TryGetValue(tag, out var count) ? count : 0;
                    if (actualCount < requiredCount)
                        return false;
                }
            }

            return true;
        }

        public Task<List<string>> GetAllowedGeneratorsAsync()
        {
            if (_state.State == null)
                return Task.FromResult(new List<string> { "PerlinTerrain", "BasicDungeon" }); // Default unlocked

            if (_state.State.UnlockedGenerators.Count == 0)
            {
                // Return defaults if none recorded
                return Task.FromResult(new List<string> { "PerlinTerrain", "BasicDungeon" });
            }

            return Task.FromResult(_state.State.UnlockedGenerators.ToList());
        }

        public Task<bool> IsGeneratorUnlockedAsync(string generatorName)
        {
            if (_state.State == null)
            {
                // Default unlocked generators
                return Task.FromResult(generatorName == "PerlinTerrain" || generatorName == "BasicDungeon");
            }

            return Task.FromResult(
                _state.State.UnlockedGenerators.Count == 0
                    ? (generatorName == "PerlinTerrain" || generatorName == "BasicDungeon")
                    : _state.State.UnlockedGenerators.Contains(generatorName));
        }

        public Task<MetaProgressionState?> GetStateAsync()
        {
            return Task.FromResult<MetaProgressionState?>(_state.State);
        }

        public async Task AddUnlockCriteriaAsync(UnlockCriteria criteria)
        {
            if (_state.State == null)
                return;

            if (string.IsNullOrEmpty(criteria.CriteriaId))
            {
                criteria.CriteriaId = $"criteria-{Guid.NewGuid():N}";
            }

            _state.State.UnlockCriteriaDefinitions[criteria.CriteriaId] = criteria;
            _state.State.LastUpdatedAt = DateTime.UtcNow;
            await _state.WriteStateAsync();

            // Evaluate unlocks after adding criteria
            await EvaluateUnlocksAsync();
        }
    }
}

