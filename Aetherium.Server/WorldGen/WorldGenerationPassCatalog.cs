using Aetherium.Server.Audio;

namespace Aetherium.WorldGen
{
    /// <summary>
    /// Single source of truth for the standard world-generation pass list.
    ///
    /// <para>
    /// Every world built from a <see cref="WorldGenerationRequest"/> — the game server
    /// (<c>GameMapGrain</c>), snapshot rehydration (<c>SnapshotWorldBuilder</c>), the
    /// WorldGenCLI preview API, and aetherctl — must run the same passes, or the worlds
    /// they produce diverge (the server used to run a 4-pass subset of the CLI's list,
    /// which is why in-game worlds were unthemed and unpopulated while previews were rich).
    /// Snapshot rehydration in particular must match the grain exactly: it regenerates
    /// terrain from the recipe and overlays the grain's entities, so a differing pass
    /// list corrupts joins.
    /// </para>
    ///
    /// <para>
    /// The orchestrator orders passes by (Phase, Name), so only membership matters here.
    /// <c>HybridLayoutPass</c> no-ops when the request carries no anchors, and each
    /// template-specific pass gates itself via <c>SupportsTemplate</c>.
    /// </para>
    /// </summary>
    public static class WorldGenerationPassCatalog
    {
        public static IWorldGenerationPass[] BuildPasses(
            WorldGenerationTemplate template,
            IAudioProfileRepository? audioProfileRepository = null)
        {
            return template switch
            {
                WorldGenerationTemplate.Outdoor => new IWorldGenerationPass[]
                {
                    new Hybrid.HybridLayoutPass(),
                    new Passes.OutdoorLayoutPass(),
                    new Passes.OutdoorThemingPass(),
                    new Passes.OutdoorPopulationPass(),
                    new Passes.EnvironmentalStoryPass(),
                    new Passes.AudioGenerationPass(audioProfileRepository),
                    new Passes.OutdoorInteractionsPass(),
                    new Passes.PortalNetworkPass(),
                    new Passes.OutdoorValidationPass()
                },
                _ => new IWorldGenerationPass[]
                {
                    new Hybrid.HybridLayoutPass(),
                    new Passes.DungeonLayoutPass(),
                    new Passes.DungeonThemingPass(),
                    new Passes.DungeonPopulationPass(),
                    new Passes.EnvironmentalStoryPass(),
                    new Passes.AudioGenerationPass(audioProfileRepository),
                    new Passes.DungeonInteractionsPass(),
                    new Passes.PortalNetworkPass(),
                    new Passes.DungeonValidationPass()
                }
            };
        }
    }
}
