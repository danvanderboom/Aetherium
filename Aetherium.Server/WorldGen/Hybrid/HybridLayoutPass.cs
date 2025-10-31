using Aetherium.Core;
using Aetherium.Components;
using Aetherium.WorldGen;

namespace Aetherium.WorldGen.Hybrid
{
    /// <summary>
    /// Pass that processes hybrid anchors and makes them available to layout generators.
    /// Must run before layout pass.
    /// </summary>
    public sealed class HybridLayoutPass : IWorldGenerationPass
    {
        public string Name => "hybrid-layout";
        public GenerationPhase Phase => GenerationPhase.PreLayout;

        public bool SupportsTemplate(WorldGenerationTemplate template) => true; // Supports all templates

        public void Execute(WorldGenerationContext context)
        {
            // Store anchors in generator context for access by layout generators
            if (context.Request.HybridAnchors != null)
            {
                context.GeneratorContext.PhaseArtifacts["HybridLayout"] = context.Request.HybridAnchors;

                // Extract anchor information for generators to use
                var blockedLocations = new System.Collections.Generic.HashSet<Aetherium.Components.WorldLocation>();
                var requiredLocations = new System.Collections.Generic.HashSet<Aetherium.Components.WorldLocation>();

                for (int z = 0; z < context.Request.Levels; z++)
                {
                    var blocked = context.Request.HybridAnchors.GetBlockedLocations(z);
                    foreach (var loc in blocked)
                    {
                        blockedLocations.Add(loc);
                    }

                    // Collect required (non-blocking) anchors
                    foreach (var anchor in context.Request.HybridAnchors.Anchors)
                    {
                        if (anchor.ZLevel == z && !anchor.IsBlocking)
                        {
                            foreach (var loc in anchor.GetLocations(z))
                            {
                                requiredLocations.Add(loc);
                            }
                        }
                    }
                }

                context.GeneratorContext.PhaseArtifacts["BlockedLocations"] = blockedLocations;
                context.GeneratorContext.PhaseArtifacts["RequiredLocations"] = requiredLocations;

                // Extract tagged anchors for semantic use
                var taggedAnchors = new System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<HybridAnchor>>();
                foreach (var anchor in context.Request.HybridAnchors.Anchors)
                {
                    foreach (var tag in anchor.Tags)
                    {
                        if (!taggedAnchors.TryGetValue(tag, out var list))
                        {
                            list = new System.Collections.Generic.List<HybridAnchor>();
                            taggedAnchors[tag] = list;
                        }
                        list.Add(anchor);
                    }
                }

                context.GeneratorContext.PhaseArtifacts["TaggedAnchors"] = taggedAnchors;

                // If world exists, respect anchors by storing blocked information
                // Generators can check PhaseArtifacts["BlockedLocations"] to avoid placing content there
                // The actual clearing of content is handled by generators that respect the anchors
            }
        }
    }
}

