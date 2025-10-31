using System;
using Aetherium.WorldGen.Generators;

namespace Aetherium.WorldGen.Passes
{
    public sealed class DungeonLayoutPass : IWorldGenerationPass
    {
        public string Name => "dungeon-layout";
        public GenerationPhase Phase => GenerationPhase.Layout;

        public bool SupportsTemplate(WorldGenerationTemplate template) => template == WorldGenerationTemplate.Dungeon;

        public void Execute(WorldGenerationContext context)
        {
            var registry = context.GeneratorContext.FeatureRegistry;
            IMapGenerator? generator = null;

            if (registry != null)
            {
                generator = registry.GetGenerator(context.Request.LayoutGenerator);
            }

            generator ??= new AdvancedDungeonGenerator();

            try
            {
                var world = generator.Generate(context.GeneratorContext);
                context.World = world;
            }
            catch (Exception ex)
            {
                context.AddError($"Layout generation failed: {ex.Message}");
            }
        }
    }
}



