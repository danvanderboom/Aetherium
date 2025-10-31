using Aetherium.Core;

namespace Aetherium.WorldGen
{
    /// <summary>
    /// A feature that can be applied to modify a world during generation.
    /// Features are composable and can be chained in a pipeline.
    /// Examples: lakes, forests, cities, roads, etc.
    /// </summary>
    public interface IGenerationFeature
    {
        /// <summary>
        /// Applies this feature to the world within the given context.
        /// </summary>
        void Apply(World world, GeneratorContext context);
    }
}


