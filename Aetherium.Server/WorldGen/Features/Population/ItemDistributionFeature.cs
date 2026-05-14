using System;
using System.Collections.Generic;
using Aetherium.Components;
using Aetherium.Core;

namespace Aetherium.WorldGen.Features.Population
{
    /// <summary>
    /// Distributes items/loot based on weighted tables.
    /// </summary>
    public class ItemDistributionFeature : IGenerationFeature
    {
        private readonly Dictionary<string, int> _itemWeights;
        private readonly int _totalItems;
        private readonly string _placementTerrain;

        public ItemDistributionFeature(
            Dictionary<string, int> itemWeights,
            int totalItems = 10,
            string placementTerrain = "Indoors")
        {
            _itemWeights = itemWeights ?? new Dictionary<string, int>();
            _totalItems = totalItems;
            _placementTerrain = placementTerrain;
        }

        public void Apply(World world, GeneratorContext context)
        {
            if (_itemWeights.Count == 0)
                return;

            // Calculate total weight
            int totalWeight = 0;
            foreach (var weight in _itemWeights.Values)
            {
                totalWeight += weight;
            }

            if (totalWeight == 0)
                return;

            // Find candidate locations
            var candidates = new List<WorldLocation>();
            for (int y = 0; y < context.Height; y++)
            {
                for (int x = 0; x < context.Width; x++)
                {
                    var loc = new WorldLocation(x, y, context.ZLevel);
                    if (world.EntitiesByLocation.ContainsKey(loc))
                    {
                        var terrainType = world.GetTerrainType(loc);
                        if (terrainType?.Name == _placementTerrain)
                        {
                            candidates.Add(loc);
                        }
                    }
                }
            }

            if (candidates.Count == 0)
                return;

            // Place items
            int placed = 0;
            while (placed < _totalItems && candidates.Count > 0)
            {
                // Select random location
                var loc = candidates[context.GetRandom("feature:item-distribution").Next(candidates.Count)];
                candidates.Remove(loc);

                // Select item type based on weights
                string itemType = SelectWeightedItem(context.GetRandom("feature:item-distribution"), _itemWeights, totalWeight);

                // Placeholder for actual item spawning
                Console.WriteLine($"[ItemDistributionFeature] Would place {itemType} at {loc}");
                placed++;
            }

            Console.WriteLine($"[ItemDistributionFeature] Placed {placed} items (target: {_totalItems})");
        }

        private string SelectWeightedItem(Random random, Dictionary<string, int> weights, int totalWeight)
        {
            int roll = random.Next(totalWeight);
            int cumulative = 0;

            foreach (var kvp in weights)
            {
                cumulative += kvp.Value;
                if (roll < cumulative)
                {
                    return kvp.Key;
                }
            }

            // Fallback to first item
            foreach (var kvp in weights)
            {
                return kvp.Key;
            }

            return "Unknown";
        }
    }
}


