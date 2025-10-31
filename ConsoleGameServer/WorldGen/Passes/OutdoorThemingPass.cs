using System.Collections.Generic;
using ConsoleGame.Core;

namespace ConsoleGame.WorldGen.Passes
{
    public sealed class OutdoorThemingPass : IWorldGenerationPass
    {
        public string Name => "outdoor-theming";
        public GenerationPhase Phase => GenerationPhase.Theming;

        public bool SupportsTemplate(WorldGenerationTemplate template) => template == WorldGenerationTemplate.Outdoor;

        public void Execute(WorldGenerationContext context)
        {
            if (context.World == null)
            {
                context.AddError("Outdoor theming requires world instance");
                return;
            }

            var theme = context.Request.Parameters.TryGetValue("biome-theme", out var value) && !string.IsNullOrWhiteSpace(value)
                ? value
                : "temperate-valley";

            context.World.Features.Add(new WorldFeature
            {
                Settings = new Dictionary<string, string>
                {
                    ["type"] = "biome-theme",
                    ["value"] = theme
                }
            });
        }
    }
}


