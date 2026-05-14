using Aetherium.Core;

namespace Aetherium.WorldGen
{
    /// <summary>
    /// A feature that can be applied to modify a world during generation.
    /// Features are composable and can be chained in a pipeline.
    /// Examples: lakes, forests, cities, roads, etc.
    ///
    /// <para><b>Threading contract:</b> a feature's <see cref="Apply"/> method is invoked from a
    /// single thread. Features must not assume thread-affinity (the orchestrator may dispatch to
    /// a background thread for timeout enforcement). Draw all randomness through
    /// <see cref="GeneratorContext.GetRandom"/> with a unique, stable scope name so that the
    /// feature's random stream is isolated from generators and other features.</para>
    /// </summary>
    public interface IGenerationFeature
    {
        /// <summary>
        /// Applies this feature to the world within the given context.
        /// Use <see cref="GeneratorContext.GetRandom"/> for all randomness to maintain
        /// seed-reproducibility.
        /// </summary>
        void Apply(World world, GeneratorContext context);
    }
}


