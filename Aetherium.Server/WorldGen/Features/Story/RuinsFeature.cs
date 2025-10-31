using System;
using System.Collections.Generic;
using System.Linq;
using Aetherium.Components;
using Aetherium.Core;
using Aetherium.WorldGen;

namespace Aetherium.WorldGen.Features.Story
{
    /// <summary>
    /// Feature that places ruins with coherent history in the world.
    /// </summary>
    public class RuinsFeature : IGenerationFeature
    {
        private const int MaxRuins = 3;
        private const int MinRuinsDistance = 20; // Minimum distance between ruins

        public void Apply(World world, GeneratorContext context)
        {
            var rng = context.GetRandom("ruins");
            var passableLocations = world.EntitiesByLocation.Keys
                .Where(loc => world.PassableTerrain(loc))
                .ToList();

            if (passableLocations.Count == 0)
                return;

            var placedRuins = new List<WorldLocation>();
            int attempts = 0;
            int maxAttempts = MaxRuins * 20;

            while (placedRuins.Count < MaxRuins && attempts < maxAttempts)
            {
                attempts++;

                var location = passableLocations[rng.Next(passableLocations.Count)];

                // Check minimum distance from other ruins
                bool tooClose = placedRuins.Any(ruin =>
                {
                    var dx = location.X - ruin.X;
                    var dy = location.Y - ruin.Y;
                    var distance = Math.Sqrt(dx * dx + dy * dy);
                    return distance < MinRuinsDistance;
                });

                if (tooClose)
                    continue;

                // Create ruin entity
                var ruin = new Ruin();
                ruin.Set(location);

                // Add inscription component with historical text
                var inscription = new Inscription
                {
                    Title = $"Ancient Ruins of {GetRegionName(location, context)}",
                    Text = GenerateRuinHistory(location, context),
                    Topic = "history",
                    Author = "Ancient Builders",
                    Era = GenerateEra(context.GetRandom("ruin-era"))
                };

                ruin.Set(inscription);
                world.AddEntity(ruin);

                placedRuins.Add(location);
            }
        }

        /// <summary>
        /// Generates historical text about a ruin.
        /// </summary>
        private string GenerateRuinHistory(WorldLocation location, GeneratorContext context)
        {
            var rng = context.GetRandom($"ruin-{location.X}-{location.Y}");
            var eras = new[] { "First Age", "Golden Age", "Dark Age", "Age of Legends" };
            var era = eras[rng.Next(eras.Length)];

            return $"These ruins date back to the {era}. " +
                   $"Once a thriving settlement, it now stands as a testament to time. " +
                   $"Few remember the stories of those who built these walls, " +
                   $"but the stones still whisper of their past glory.";
        }

        /// <summary>
        /// Gets a region name based on location.
        /// </summary>
        private string GetRegionName(WorldLocation location, GeneratorContext context)
        {
            var rng = context.GetRandom($"region-{location.Z}");
            var regions = new[] { "Elderwood", "Whispering Peaks", "Forgotten Vale", "Ancient Hollows" };
            return regions[rng.Next(regions.Length)];
        }

        /// <summary>
        /// Generates an era name.
        /// </summary>
        private string GenerateEra(Random random)
        {
            var eras = new[] { "First Age", "Golden Age", "Dark Age", "Age of Legends", "Ancient Times" };
            return eras[random.Next(eras.Length)];
        }
    }

    /// <summary>
    /// Entity representing ancient ruins.
    /// </summary>
    public class Ruin : Entity
    {
        public Ruin() : base()
        {
            var tile = Get<Tile>();
            if (tile != null)
            {
                tile.Character = 'R'; // Ruins symbol
            }

            // Ruins are not passable but can be examined
            Set(new ObstructsView { Opacity = 0.3f }); // Partially visible
        }
    }
}

