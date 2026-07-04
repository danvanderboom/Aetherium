using System.Threading.Tasks;
using Orleans;
using Aetherium.Server.MultiWorld;
using Aetherium.Server.Narrative.State;

using Aetherium.Model.Narrative;
namespace Aetherium.Server.Narrative
{
    /// <summary>
    /// Resolves the <see cref="INarrativeStateGrain"/> that governs a world's quest progression,
    /// centralizing the worldInfo → narrativeId → scope → grain-key derivation that the
    /// consequence engine, the game hub, and the quest tools all need. The key convention matches
    /// <c>NarrativeConsequenceEngine.GetStateGrainKey</c>: per-world scope keys on
    /// <c>"{worldId}:{narrativeId}"</c>, otherwise the shared <c>narrativeId</c>.
    /// </summary>
    public static class NarrativeStateResolver
    {
        /// <summary>World metadata key selecting shared vs per-world narrative state.</summary>
        public const string ScopeMetadataKey = "NarrativeStateScope";

        /// <summary>
        /// Returns the narrative-state grain for the world, or null when the world can't be
        /// resolved or has no associated narrative (nothing to track).
        /// </summary>
        public static async Task<INarrativeStateGrain?> ResolveForWorldAsync(
            IGrainFactory grainFactory, string? worldId)
        {
            if (grainFactory == null || string.IsNullOrEmpty(worldId))
                return null;

            var worldGrain = grainFactory.GetGrain<IWorldGrain>(worldId);
            var worldInfo = await worldGrain.GetInfoAsync();
            if (worldInfo == null || string.IsNullOrEmpty(worldInfo.NarrativeId))
                return null;

            var scope = worldInfo.Metadata != null &&
                        worldInfo.Metadata.TryGetValue(ScopeMetadataKey, out var scopeObj)
                ? scopeObj?.ToString()
                : "shared";

            var key = scope == "per-world"
                ? $"{worldId}:{worldInfo.NarrativeId}"
                : worldInfo.NarrativeId!;

            return grainFactory.GetGrain<INarrativeStateGrain>(key);
        }
    }
}
