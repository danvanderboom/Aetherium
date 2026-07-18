using System.Threading;

namespace Aetherium.WorldGen
{
    /// <summary>
    /// Represents a single pipeline phase that can mutate the world, collect metrics,
    /// or enforce validation rules during procedural content generation.
    ///
    /// <para>Threading contract: a pass is invoked from a single thread, but may run on any
    /// thread (the orchestrator may use <c>Task.Run</c> to enforce <c>PhaseTimeout</c>). Passes
    /// must not assume thread-affinity and must not share state across passes outside
    /// <see cref="WorldGenerationContext.SharedData"/> / <see cref="GeneratorContext"/>.</para>
    /// </summary>
    public interface IWorldGenerationPass
    {
        string Name { get; }
        GenerationPhase Phase { get; }
        bool SupportsTemplate(WorldGenerationTemplate template);

        /// <summary>
        /// Whether this pass runs for a given world tiling (docs/h3-topology.md). Defaults to
        /// <c>true</c> — most passes read the terrain dictionary and are topology-agnostic. Passes
        /// built on square-grid geometry (rectangular bounding boxes, 4/6-neighbour deltas, or a
        /// single start→objective route) override this to opt out of tilings they can't yet handle
        /// — notably <c>"h3"</c>, whose coordinates pack a spherical cell index rather than a
        /// scannable rectangle. Sphere-native replacements for those passes are the phased follow-up.
        /// </summary>
        bool SupportsTopology(string? topology) => true;

        /// <summary>
        /// Executes the pass. Existing implementations should override the
        /// parameterless variant; new code is encouraged to override
        /// <see cref="Execute(WorldGenerationContext, CancellationToken)"/> to support
        /// cooperative cancellation.
        /// </summary>
        void Execute(WorldGenerationContext context);

        /// <summary>
        /// Cancellable variant; defaults to the synchronous <see cref="Execute(WorldGenerationContext)"/>.
        /// Passes that perform long-running work should override and observe the token.
        /// </summary>
        void Execute(WorldGenerationContext context, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Execute(context);
        }
    }
}



