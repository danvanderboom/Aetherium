using System;
using System.Collections.Generic;
using System.Linq;
using Aetherium;
using Aetherium.Components;
using Aetherium.Core;
using Aetherium.Entities;

namespace Aetherium.WorldGen.Passes
{
    public sealed class DungeonPopulationPass : IWorldGenerationPass
    {
        public string Name => "dungeon-population";
        public GenerationPhase Phase => GenerationPhase.Population;

        public bool SupportsTemplate(WorldGenerationTemplate template) => template == WorldGenerationTemplate.Dungeon;

        public void Execute(WorldGenerationContext context)
        {
            if (context.World == null)
            {
                context.AddError("Population pass requires a generated world.");
                return;
            }

            var world = context.World;
            EnsureMonsterTileType(world);
            var rng = context.GeneratorContext.GetRandom("population:dungeon");

            var candidateLocations = world.EntitiesByLocation.Keys
                .Where(loc => world.PassableTerrain(loc) && !context.PrimaryPath.Contains(loc))
                .ToList();

            if (candidateLocations.Count == 0)
                return;

            PlaceMonsters(world, candidateLocations, rng);
            PlaceTreasure(world, candidateLocations, rng);
        }

        private static void PlaceMonsters(World world, List<WorldLocation> candidates, Random rng)
        {
            int monstersToPlace = Math.Max(2, candidates.Count / 50);
            for (int i = 0; i < monstersToPlace; i++)
            {
                var location = candidates[rng.Next(candidates.Count)];
                var monster = new Monster(world);
                monster.Set(location);
                monster.Set(new Goal { Created = DateTime.UtcNow, Location = location });
                world.AddEntity(monster);
            }
        }

        private static void PlaceTreasure(World world, List<WorldLocation> candidates, Random rng)
        {
            var chestLocation = candidates[rng.Next(candidates.Count)];
            var restorative = new HealthRestorativeItem();
            restorative.Set(chestLocation);
            world.AddEntity(restorative);

            var lantern = new LanternItem();
            lantern.Set(chestLocation.FromDelta(1, 0, 0));
            world.AddEntity(lantern);
        }

        private static void EnsureMonsterTileType(World world)
        {
            if (!world.TileTypes.ContainsKey("Monster"))
            {
                world.TileTypes["Monster"] = new TileType
                {
                    Name = "Monster",
                    Settings = new Dictionary<string, string>
                    {
                        { "MapCharacter", "M" },
                        { "BackgroundColor", ConsoleColor.Black.ToString() },
                        { "ForegroundColor", ConsoleColor.DarkRed.ToString() }
                    }
                };
            }
        }
    }
}



