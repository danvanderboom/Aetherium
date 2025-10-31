using System.Collections.Generic;
using Aetherium.Core;

namespace Aetherium.WorldGen
{
    /// <summary>
    /// A pipeline of generation features that are applied in sequence to build a world.
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


