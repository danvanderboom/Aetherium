using System;
using System.Collections.Generic;
using System.Linq;
using Aetherium.Core;
using Aetherium.Server.Narrative.Procedural;
using Aetherium.WorldGen.Features.Story;

namespace Aetherium.WorldGen.Passes
{
    /// <summary>
    /// Generation pass that adds environmental storytelling elements:
    /// ruins with coherent history, abandoned camps with clues, and lore fragments.
    /// </summary>
    public sealed class EnvironmentalStoryPass : IWorldGenerationPass
    {
        public string Name => "environmental-story";
        public GenerationPhase Phase => GenerationPhase.Population;

        public bool SupportsTemplate(WorldGenerationTemplate template) => true; // Works with all templates

        public void Execute(WorldGenerationContext context)
        {
            if (context.World == null)
            {
                context.AddError("Environmental story pass requires world instance");
                return;
            }

            var world = context.World;
            var constraints = context.GeneratorContext.NarrativeConstraints;
            var rng = context.GeneratorContext.GetRandom("environmental-story");

            // Place ruins if requested in constraints
            if (constraints.StoryPOIs.Any(poi => poi.Name.Contains("ruin", StringComparison.OrdinalIgnoreCase)))
            {
                var ruinsFeature = new RuinsFeature();
                ruinsFeature.Apply(world, context.GeneratorContext);
            }

            // Place abandoned camps if requested
            if (constraints.StoryPOIs.Any(poi => poi.Name.Contains("camp", StringComparison.OrdinalIgnoreCase)))
            {
                var campFeature = new AbandonedCampFeature();
                campFeature.Apply(world, context.GeneratorContext);
            }

            // Place lore fragments if topics are specified
            if (constraints.LoreTopics.Count > 0)
            {
                var loreFeature = new PlaceLoreFragmentsFeature(constraints.LoreTopics);
                loreFeature.Apply(world, context.GeneratorContext);
            }
        }
    }
}

