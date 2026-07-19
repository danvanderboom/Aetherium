using Aetherium.WorldGen.Generators.Outdoor;

namespace Aetherium.WorldGen.Passes
{
    public sealed class OutdoorLayoutPass : IWorldGenerationPass
    {
        public string Name => "outdoor-layout";
        public GenerationPhase Phase => GenerationPhase.Layout;

        public bool SupportsTemplate(WorldGenerationTemplate template) => template == WorldGenerationTemplate.Outdoor;

        public void Execute(WorldGenerationContext context)
        {
            var gc = context.GeneratorContext;
            // On a sphere (H3) the world is a shell of cells enumerated by resolution, not a
            // Width×Height rectangle, and terrain is classified from 3-D noise over each cell's
            // centre unit vector — a dedicated generator handles that. Every other tiling
            // (square/hex/tri) uses the planar outdoor generator unchanged.
            IMapGenerator generator = gc.Topology.Name == "h3"
                ? new H3TerrainGenerator()
                : new AdvancedOutdoorGenerator();
            context.World = generator.Generate(gc);
        }
    }
}



