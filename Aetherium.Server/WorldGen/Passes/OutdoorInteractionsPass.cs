using System;
using System.Linq;
using Aetherium.Components;
using Aetherium.Core;

namespace Aetherium.WorldGen.Passes
{
    public sealed class OutdoorInteractionsPass : IWorldGenerationPass
    {
        public string Name => "outdoor-interactions";
        public GenerationPhase Phase => GenerationPhase.Interactions;

        public bool SupportsTemplate(WorldGenerationTemplate template) => template == WorldGenerationTemplate.Outdoor;

        // Places buildings/doors/keys with rectangular footprints and square offsets; a sphere-native
        // settlement pass (footprints over topology.Range) is the phased follow-up.
        public bool SupportsTopology(string? topology)
            => !string.Equals(topology, "h3", System.StringComparison.OrdinalIgnoreCase);

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



