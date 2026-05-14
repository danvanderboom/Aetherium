using Aetherium.Core;

namespace Aetherium.WorldGen
{
    /// <summary>
    /// Interface for procedural map generators that create or modify worlds.
    ///
    /// <para><b>Threading contract:</b> a generator is invoked from a single thread and must not
    /// be called concurrently. The orchestrator uses <c>Task.Run</c> when enforcing
    /// <c>PhaseTimeout</c> but each generator instance is still single-threaded; do not cache
    /// mutable state across <see cref="Generate"/> calls unless you take a lock.
    /// For determinism, draw all randomness through <see cref="GeneratorContext.GetRandom"/>
    /// with a stable scope name rather than directly from <see cref="GeneratorContext.Random"/>
    /// (which is the shared global stream and can produce non-deterministic results when other
    /// generators or features draw from it concurrently or in a different order).</para>
    /// </summary>
    public interface IMapGenerator
    {
        /// <summary>
        /// Generates a world according to the generator's algorithm and the provided context.
        /// Implementations must treat <paramref name="context"/> as the sole source of randomness
        /// and must not share mutable state with other generators.
        /// </summary>
        World Generate(GeneratorContext context);
    }
}


