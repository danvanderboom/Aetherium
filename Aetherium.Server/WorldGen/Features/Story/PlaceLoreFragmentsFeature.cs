using System;
using System.Collections.Generic;
using System.Linq;
using Aetherium.Core;
using Aetherium.Components;
using Aetherium.Entities;
using Aetherium.Server.Narrative.Procedural;
using Aetherium.WorldGen;

namespace Aetherium.WorldGen.Features.Story
{
    /// <summary>
    /// Feature that places lore fragments (books, inscriptions) throughout the world.
    /// </summary>
    public class PlaceLoreFragmentsFeature : IGenerationFeature
    {
        private readonly List<string> _topics;
        private const int FragmentsPerTopic = 2;

        public PlaceLoreFragmentsFeature(List<string> topics)
        {
            _topics = topics ?? new List<string>();
        }

        public void Apply(World world, GeneratorContext context)
        {
            if (_topics.Count == 0)
                return;

            var rng = context.GetRandom("lore-fragments");
            var passableLocations = world.EntitiesByLocation.Keys
                .Where(loc => world.PassableTerrain(loc))
                .ToList();

            if (passableLocations.Count == 0)
                return;

            var placedLocations = new HashSet<WorldLocation>();
            var existingLore = new Dictionary<string, List<string>>();

            // Place fragments for each topic
            foreach (var topic in _topics)
            {
                for (int i = 0; i < FragmentsPerTopic; i++)
                {
                    int attempts = 0;
                    int maxAttempts = 50;

                    while (attempts < maxAttempts)
                    {
                        attempts++;

                        var location = passableLocations[rng.Next(passableLocations.Count)];

                        // Avoid placing multiple fragments at same location
                        if (placedLocations.Contains(location))
                            continue;

                        // Generate lore fragment with consistent cross-references
                        var region = GetRegionName(location, context);
                        var fragment = LoreGenerator.GenerateLoreFragment(topic, region, existingLore, rng);

                        fragment.Set(location);
                        world.AddEntity(fragment);

                        placedLocations.Add(location);

                        // Track generated lore for cross-referencing
                        var inscription = fragment.Get<Components.Inscription>();
                        if (inscription != null)
                        {
                            if (!existingLore.ContainsKey(topic))
                            {
                                existingLore[topic] = new List<string>();
                            }

                            existingLore[topic].Add(inscription.Text);
                        }

                        break;
                    }
                }
            }
        }

        /// <summary>
        /// Gets a region name based on location.
        /// </summary>
        private string GetRegionName(WorldLocation location, GeneratorContext context)
        {
            var rng = context.GetRandom($"region-{location.Z}");
            var regions = new[] { "Elderwood", "Whispering Peaks", "Forgotten Vale", "Ancient Hollows", "The Wilds" };
            return regions[rng.Next(regions.Length)];
        }
    }
}

