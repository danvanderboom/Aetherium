using System.Collections.Generic;
using Aetherium.Core;

namespace Aetherium.WorldGen
{
    /// <summary>
    /// Fluent builder for composing a sequence of <see cref="IGenerationFeature"/> objects and
    /// applying them to a world in a single call.
    ///
    /// <para>This type is a <em>standalone utility</em>, not a registered
    /// <see cref="IWorldGenerationPass"/>. It is intentionally excluded from the
    /// <see cref="WorldGenerationOrchestrator"/> pipeline so that individual passes and generators
    /// can use it internally to compose sub-steps without coupling the orchestrator to a
    /// feature-list abstraction. To consume it within the orchestration pipeline, create a pass
    /// that holds a <see cref="GeneratorPipeline"/> and calls <see cref="Apply"/> from its
    /// <c>Execute</c> method.</para>
    /// </summary>
    public sealed class GeneratorPipeline
    {
        private readonly List<IGenerationFeature> _features = new List<IGenerationFeature>();

        /// <summary>
        /// Adds a feature to the pipeline. Features are applied in the order they are added.
        /// </summary>
        public GeneratorPipeline AddFeature(IGenerationFeature feature)
        {
            _features.Add(feature);
            return this;
        }

        /// <summary>
        /// Applies all features in the pipeline to the world.
        /// </summary>
        public void Apply(World world, GeneratorContext context)
        {
            foreach (var feature in _features)
            {
                feature.Apply(world, context);
            }
        }

        /// <summary>
        /// Creates a new pipeline with no features.
        /// </summary>
        public static GeneratorPipeline Create() => new GeneratorPipeline();
    }
}


