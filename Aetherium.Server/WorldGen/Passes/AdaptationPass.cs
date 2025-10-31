using System;
using System.Collections.Generic;
using System.Linq;
using Aetherium.Components;
using Aetherium.Core;
using Aetherium.Server.Agents.Analysis;
using Aetherium.Server.WorldGen.Adaptation;

namespace Aetherium.WorldGen.Passes
{
    /// <summary>
    /// Pass that adapts world content based on agent behavior after initial generation.
    /// </summary>
    public sealed class AdaptationPass : IWorldGenerationPass
    {
        public string Name => "adaptation";
        public GenerationPhase Phase => GenerationPhase.Adaptation; // Runs after interactions, before validation

        public bool SupportsTemplate(WorldGenerationTemplate template) => true; // Supports all templates

        public void Execute(WorldGenerationContext context)
        {
            if (context.World == null || context.Request.AdaptiveAgentId == null)
            {
                return; // Skip if no world or no adaptive agent specified
            }

            var agentId = context.Request.AdaptiveAgentId;

            try
            {
                // Get behavior analysis for agent (via grain factory - requires async, so this is placeholder)
                // In production, would need to make Execute async or use a different pattern
                // For now, this is a placeholder that shows the intent
                Console.WriteLine($"[AdaptationPass] Would adapt content for agent: {agentId}");
                
                // TODO: In production, would:
                // 1. Get behavior analysis grain
                // 2. Get behavior analysis and content needs
                // 3. Adapt narrative constraints
                // 4. Inject contextual content
                // 5. Adjust loot tables
                // 6. Modify difficulty
                
                // For now, we'll add this as a framework that can be extended when we have async support
                // or use a post-generation hook pattern
            }
            catch (Exception ex)
            {
                context.AddError($"Adaptation pass error: {ex.Message}");
            }
        }

        /// <summary>
        /// Helper method to adapt narrative constraints based on agent behavior.
        /// This would be called from a post-generation hook.
        /// </summary>
        public static void AdaptNarrativeConstraintsForAgent(
            WorldGenerationContext context,
            BehaviorAnalysis behaviorAnalysis,
            List<ContentNeed> contentNeeds)
        {
            if (context.Request.AdaptiveAgentId == null)
                return;

            // Adapt narrative constraints
            var baseConstraints = context.Request.Narrative;
            var adaptedConstraints = AdaptiveNarrativeGenerator.AdaptNarrativeConstraints(
                baseConstraints,
                behaviorAnalysis,
                contentNeeds);

            // Update context with adapted constraints
            context.Request.Narrative = adaptedConstraints;
            context.GeneratorContext.NarrativeConstraints = adaptedConstraints;
        }

        /// <summary>
        /// Helper method to inject contextual content into the world.
        /// This would be called from a post-generation hook.
        /// </summary>
        public static void InjectContextualContentForAgent(
            World world,
            BehaviorAnalysis behaviorAnalysis,
            List<ContentNeed> contentNeeds,
            WorldLocation? agentLocation = null)
        {
            if (world == null || agentLocation == null)
                return;

            // Inject helpful items
            DynamicContentInjector.InjectHelpfulItems(world, contentNeeds, agentLocation);

            // Adjust monster density
            DynamicContentInjector.AdjustMonsterDensity(world, behaviorAnalysis, agentLocation);

            // Inject hints
            DynamicContentInjector.InjectHints(world, behaviorAnalysis.StrugglePatterns, agentLocation);
        }
    }
}

