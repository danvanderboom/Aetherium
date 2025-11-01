using System.Collections.Generic;
using System.Threading.Tasks;
using Orleans;

namespace Aetherium.Server.MetaProgression
{
    /// <summary>
    /// Orleans grain interface for tracking player meta-progression across multiple worlds.
    /// Tracks discoveries, evaluates unlock criteria, and filters allowed generators.
    /// </summary>
    public interface IMetaProgressionGrain : IGrainWithStringKey
    {
        /// <summary>
        /// Records a world discovery (visit to a world/map).
        /// </summary>
        Task RecordDiscoveryAsync(string worldId, string mapId, string? worldTemplate = null, List<string>? tags = null);

        /// <summary>
        /// Records completion of a cross-world quest.
        /// </summary>
        Task RecordQuestCompletionAsync(string questId, bool isCrossWorld);

        /// <summary>
        /// Evaluates unlock criteria and unlocks new generators if conditions are met.
        /// </summary>
        Task EvaluateUnlocksAsync();

        /// <summary>
        /// Gets all unlocked generator templates/names.
        /// </summary>
        Task<List<string>> GetAllowedGeneratorsAsync();

        /// <summary>
        /// Checks if a specific generator is unlocked.
        /// </summary>
        Task<bool> IsGeneratorUnlockedAsync(string generatorName);

        /// <summary>
        /// Gets the current meta-progression state.
        /// </summary>
        Task<MetaProgressionState?> GetStateAsync();

        /// <summary>
        /// Adds an unlock criteria definition.
        /// </summary>
        Task AddUnlockCriteriaAsync(UnlockCriteria criteria);
    }
}

