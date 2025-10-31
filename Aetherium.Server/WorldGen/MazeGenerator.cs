using Aetherium.Core;

namespace Aetherium.WorldGen
{
    /// <summary>
    /// Back-compat wrapper generator which exposes the name "Maze" for discovery tests
    /// and delegates generation to the default rooms-and-corridors implementation.
    /// </summary>
    public class MazeGenerator : IMapGenerator
    {
        private readonly Generators.RoomsAndCorridorsGenerator _delegate = new Generators.RoomsAndCorridorsGenerator();

        public World Generate(GeneratorContext context)
        {
            return _delegate.Generate(context);
        }
    }
}



