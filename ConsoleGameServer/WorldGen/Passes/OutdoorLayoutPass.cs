using ConsoleGame.WorldGen.Generators.Outdoor;

namespace ConsoleGame.WorldGen.Passes
{
    public sealed class OutdoorLayoutPass : IWorldGenerationPass
    {
        public string Name => "outdoor-layout";
        public GenerationPhase Phase => GenerationPhase.Layout;

        public bool SupportsTemplate(WorldGenerationTemplate template) => template == WorldGenerationTemplate.Outdoor;

        public void Execute(WorldGenerationContext context)
        {
            var generator = new AdvancedOutdoorGenerator();
            var world = generator.Generate(context.GeneratorContext);
            context.World = world;
        }
    }
}


