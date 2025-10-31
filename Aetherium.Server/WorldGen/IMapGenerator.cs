using Aetherium.Core;

namespace Aetherium.WorldGen
{
    /// <summary>
    /// Interface for procedural map generators that create or modify worlds.
    /// </summary>
    public interface IMapGenerator
    {
        /// <summary>
        /// Generates a world according to the generator's algorithm and the provided context.
        /// </summary>
        World Generate(GeneratorContext context);
    }
}


