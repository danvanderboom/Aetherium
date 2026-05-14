using System;
using Aetherium.Components;
using Aetherium.Core;

namespace Aetherium.WorldGen.Features.Population
{
    /// <summary>
    /// Spawns NPCs based on density rules from narrative configuration.
    /// </summary>
    public class SpawnNPCsFeature : IGenerationFeature
    {
        private readonly double _density; // NPCs per 100 tiles
        private readonly string _npcType;

        public SpawnNPCsFeature(double density = 1.0, string npcType = "Monster")
        {
            _density = density;
            _npcType = npcType;
        }

        public void Apply(World world, GeneratorContext context)
        {
            // Calculate total passable tiles
            int passableTiles = 0;
            for (int y = 0; y < context.Height; y++)
            {
                for (int x = 0; x < context.Width; x++)
                {
                    var loc = new WorldLocation(x, y, context.ZLevel);
                    if (world.EntitiesByLocation.ContainsKey(loc) && world.PassableTerrain(loc))
                    {
                        passableTiles++;
                    }
                }
            }

            // Calculate number of NPCs to spawn
            int npcCount = (int)Math.Ceiling(passableTiles * _density / 100.0);

            // Spawn NPCs at random passable locations
            int spawned = 0;
            int attempts = 0;
            int maxAttempts = npcCount * 10;

            while (spawned < npcCount && attempts < maxAttempts)
            {
                attempts++;

                int x = context.GetRandom("feature:spawn-npcs").Next(context.Width);
                int y = context.GetRandom("feature:spawn-npcs").Next(context.Height);
                var loc = new WorldLocation(x, y, context.ZLevel);

                if (world.EntitiesByLocation.ContainsKey(loc) && world.PassableTerrain(loc))
                {
                    // Check if location is already occupied by an NPC
                    bool hasNPC = false;
                    if (world.EntitiesByLocation.TryGetValue(loc, out var entitiesAtLoc))
                    {
                        foreach (var kvp in entitiesAtLoc)
                        {
                            var entity = kvp.Value;
                            // Simple check - in a full implementation, check for NPC components
                            if (entity.EntityId != null && !entity.EntityId.StartsWith("Terrain"))
                            {
                                hasNPC = true;
                                break;
                            }
                        }
                    }

                    if (!hasNPC)
                    {
                        // Placeholder for actual NPC spawning
                        // In full implementation, create NPC entity here
                        Console.WriteLine($"[SpawnNPCsFeature] Would spawn {_npcType} at {loc}");
                        spawned++;
                    }
                }
            }

            Console.WriteLine($"[SpawnNPCsFeature] Spawned {spawned} NPCs (target: {npcCount})");
        }
    }
}


