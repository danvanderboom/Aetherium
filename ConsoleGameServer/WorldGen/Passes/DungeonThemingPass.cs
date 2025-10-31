using System;
using System.Collections.Generic;
using System.Linq;
using ConsoleGame.Components;
using ConsoleGame.Core;
using ConsoleGame.Entities;

namespace ConsoleGame.WorldGen.Passes
{
    public sealed class DungeonThemingPass : IWorldGenerationPass
    {
        public string Name => "dungeon-theming";
        public GenerationPhase Phase => GenerationPhase.Theming;

        public bool SupportsTemplate(WorldGenerationTemplate template) => template == WorldGenerationTemplate.Dungeon;

        public void Execute(WorldGenerationContext context)
        {
            if (context.World == null)
            {
                context.AddError("Theming pass requires a generated world.");
                return;
            }

            var theme = ResolveTheme(context);
            ApplyThemeFeature(context.World, theme);
            EnsureAmbientLighting(context);
        }

        private static string ResolveTheme(WorldGenerationContext context)
        {
            if (context.Request.Parameters.TryGetValue("theme", out var explicitTheme) && !string.IsNullOrWhiteSpace(explicitTheme))
            {
                return explicitTheme;
            }

            var tokenTheme = context.Request.Narrative.Tokens.FirstOrDefault()?.TokenType;
            if (!string.IsNullOrWhiteSpace(tokenTheme))
            {
                return tokenTheme;
            }

            return "ancient-catacombs";
        }

        private static void ApplyThemeFeature(World world, string theme)
        {
            var feature = new WorldFeature
            {
                Settings = new Dictionary<string, string>
                {
                    ["type"] = "theme",
                    ["value"] = theme
                }
            };
            world.Features.Add(feature);
        }

        private static void EnsureAmbientLighting(WorldGenerationContext context)
        {
            if (context.StartLocation is not WorldLocation start || start.IsNone || context.World == null)
                return;

            var existingLights = context.World.Entities.Values
                .Where(e => e.Has<LightSource>())
                .Select(e => e.Get<WorldLocation>())
                .Where(loc => loc != null && !loc.IsNone)
                .ToList();

            if (existingLights.Any(loc => loc!.Z == start.Z))
                return;

            var anchor = start;
            var light = new LightEntity();
            light.Set(anchor);
            light.Set(new LightSource(1.0, 30));
            context.World.AddEntity(light);
        }
    }
}


