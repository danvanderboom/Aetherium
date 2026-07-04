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

            var gc = context.GeneratorContext;
            // enemyCount, when supplied, sets the monster count exactly (clamped to available tiles);
            // absent, we keep the historical one-per-MonsterTileRatio density (min 2).
            int monsterCount = gc.HasParam("enemyCount")
                ? Math.Min(gc.GetIntParam("enemyCount", 0, min: 0, max: 100000), candidateLocations.Count)
                : Math.Max(2, candidateLocations.Count / MonsterTileRatio);
            // resourceAvailability scales treasure around the historical baseline of 2 items
            // (restorative + lantern); absent => 2. Values >1 add restoratives, <1 thin them out.
            int treasureCount = gc.HasParam("resourceAvailability")
                ? Math.Max(1, (int)Math.Round(2 * gc.GetDoubleParam("resourceAvailability", 1.0, min: 0, max: 10)))
                : 2;

            PlaceMonsters(world, candidateLocations, rng, monsterCount);
            PlaceTreasure(world, candidateLocations, rng, treasureCount);
        }

        private static WorldLocation TakeRandom(List<WorldLocation> candidates, Random rng)
        {
            int idx = rng.Next(candidates.Count);
            var loc = candidates[idx];
            candidates.RemoveAt(idx);
            return loc;
        }

        private static void PlaceMonsters(World world, List<WorldLocation> candidates, Random rng, int monstersToPlace)
        {
            for (int i = 0; i < monstersToPlace && candidates.Count > 0; i++)
            {
                var location = TakeRandom(candidates, rng);
                var monster = new Monster(world);
                monster.Set(location);
                monster.Set(new Goal { Location = location });
                world.AddEntity(monster);
            }
        }

        private static void PlaceTreasure(World world, List<WorldLocation> candidates, Random rng, int treasureCount)
        {
            if (candidates.Count == 0 || treasureCount <= 0)
                return;

            // First treasure: the restorative "chest".
            var chestLocation = TakeRandom(candidates, rng);
            var restorative = new HealthRestorativeItem();
            restorative.Set(chestLocation);
            world.AddEntity(restorative);

            // Second treasure (the historical pair): a lantern adjacent to the chest. Validate the
            // neighbor is in-bounds and passable before placing — otherwise pick a nearby passable
            // cell from the remaining candidates.
            if (treasureCount >= 2)
            {
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

            // Any treasure beyond the default pair is additional restoratives.
            for (int i = 2; i < treasureCount && candidates.Count > 0; i++)
            {
                var extraLocation = TakeRandom(candidates, rng);
                var extra = new HealthRestorativeItem();
                extra.Set(extraLocation);
                world.AddEntity(extra);
            }
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



