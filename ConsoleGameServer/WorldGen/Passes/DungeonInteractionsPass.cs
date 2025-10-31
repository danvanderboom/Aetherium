using System;
using System.Linq;
using ConsoleGame.Components;
using ConsoleGame.Core;

namespace ConsoleGame.WorldGen.Passes
{
    public sealed class DungeonInteractionsPass : IWorldGenerationPass
    {
        public string Name => "dungeon-interactions";
        public GenerationPhase Phase => GenerationPhase.Interactions;

        public bool SupportsTemplate(WorldGenerationTemplate template) => template == WorldGenerationTemplate.Dungeon;

        public void Execute(WorldGenerationContext context)
        {
            if (context.World == null)
            {
                context.AddError("Interaction pass requires a generated world.");
                return;
            }

            foreach (var token in context.Request.Narrative.Tokens)
            {
                switch (token.TokenType.ToLowerInvariant())
                {
                    case "locked-shrine":
                        EnsureLockedShrine(context, token.TokenId);
                        break;
                    case "boss-chamber":
                        MarkObjective(context, token.TokenId);
                        break;
                    default:
                        MarkGenericToken(context, token.TokenId);
                        break;
                }
            }
        }

        private static void EnsureLockedShrine(WorldGenerationContext context, string tokenId)
        {
            if (context.ObjectiveLocation is not WorldLocation objective || context.World == null)
                return;

            var shrineLocation = objective;
            context.World.SetTerrain("Indoors", shrineLocation);
            context.SharedData[$"narrative-token:{tokenId}"] = shrineLocation;
        }

        private static void MarkObjective(WorldGenerationContext context, string tokenId)
        {
            if (context.ObjectiveLocation is WorldLocation objective && !objective.IsNone)
            {
                context.SharedData[$"narrative-token:{tokenId}"] = objective;
            }
        }

        private static void MarkGenericToken(WorldGenerationContext context, string tokenId)
        {
            if (context.StartLocation is WorldLocation start && !start.IsNone)
            {
                context.SharedData[$"narrative-token:{tokenId}"] = start;
            }
        }

    }
}


