using System;
using System.Linq;
using ConsoleGame.Components;
using ConsoleGame.Core;

namespace ConsoleGame.WorldGen.Passes
{
    public sealed class OutdoorInteractionsPass : IWorldGenerationPass
    {
        public string Name => "outdoor-interactions";
        public GenerationPhase Phase => GenerationPhase.Interactions;

        public bool SupportsTemplate(WorldGenerationTemplate template) => template == WorldGenerationTemplate.Outdoor;

        public void Execute(WorldGenerationContext context)
        {
            if (context.World == null)
            {
                context.AddError("Outdoor interactions requires world instance");
                return;
            }

            foreach (var token in context.Request.Narrative.Tokens)
            {
                if (token.TokenType.Equals("ruined-tower", StringComparison.OrdinalIgnoreCase))
                {
                    var location = FindNearTerrain(context, "Water") ?? context.ObjectiveLocation ?? context.StartLocation;
                    if (location != null && !location.IsNone)
                    {
                        context.World.SetTerrain("Indoors", location);
                        context.SharedData[$"narrative-token:{token.TokenId}"] = location;
                    }
                }
                else
                {
                    if (context.StartLocation is WorldLocation start && !start.IsNone)
                    {
                        context.SharedData[$"narrative-token:{token.TokenId}"] = start;
                    }
                }
            }
        }

        private static WorldLocation? FindNearTerrain(WorldGenerationContext context, string terrain)
        {
            if (context.World == null)
                return null;

            var world = context.World;
            var matches = world.EntitiesByLocation.Keys
                .Where(loc => string.Equals(world.GetTerrainType(loc)?.Name, terrain, StringComparison.OrdinalIgnoreCase))
                .Take(50)
                .ToList();

            return matches.FirstOrDefault();
        }
    }
}


