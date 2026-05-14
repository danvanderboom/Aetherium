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
        // One monster per this many passable, off-path tiles (clamped to a minimum of 2).
        private const int MonsterTileRatio = 50;

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

            var primaryPath = new HashSet<WorldLocation>(context.PrimaryPath);
            // Sort before any RNG draws so dictionary-key enumeration order doesn't affect outcomes.
            var candidateLocations = world.EntitiesByLocation.Keys
                .Where(loc => world.PassableTerrain(loc) && !primaryPath.Contains(loc))
                .OrderBy(loc => loc.Z).ThenBy(loc => loc.Y).ThenBy(loc => loc.X)
                .ToList();

            if (candidateLocations.Count == 0)
                return;

            PlaceMonsters(world, candidateLocations, rng);
            PlaceTreasure(world, candidateLocations, rng);
        }

        private static WorldLocation TakeRandom(List<WorldLocation> candidates, Random rng)
        {
            int idx = rng.Next(candidates.Count);
            var loc = candidates[idx];
            candidates.RemoveAt(idx);
            return loc;
        }

        private static void PlaceMonsters(World world, List<WorldLocation> candidates, Random rng)
        {
            int monstersToPlace = Math.Max(2, candidates.Count / MonsterTileRatio);
            for (int i = 0; i < monstersToPlace && candidates.Count > 0; i++)
            {
                var location = TakeRandom(candidates, rng);
                var monster = new Monster(world);
                monster.Set(location);
                monster.Set(new Goal { Location = location });
                world.AddEntity(monster);
            }
        }

        private static void PlaceTreasure(World world, List<WorldLocation> candidates, Random rng)
        {
            if (candidates.Count == 0)
                return;

            var chestLocation = TakeRandom(candidates, rng);
            var restorative = new HealthRestorativeItem();
            restorative.Set(chestLocation);
            world.AddEntity(restorative);

            // Lantern sits adjacent to the chest. Validate the neighbor is in-bounds and passable
            // before placing — otherwise pick a nearby passable cell from the remaining candidates.
            var lanternLocation = chestLocation.FromDelta(1, 0, 0);
            if (!world.EntitiesByLocation.ContainsKey(lanternLocation) || !world.PassableTerrain(lanternLocation))
            {
                if (candidates.Count == 0)
                    return;
                lanternLocation = TakeRandom(candidates, rng);
            }

            var lantern = new LanternItem();
            lantern.Set(lanternLocation);
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



