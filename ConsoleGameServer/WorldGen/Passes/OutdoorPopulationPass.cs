using System;
using System.Collections.Generic;
using System.Linq;
using ConsoleGame;
using ConsoleGame.Components;
using ConsoleGame.Core;
using ConsoleGame.Entities;

namespace ConsoleGame.WorldGen.Passes
{
    public sealed class OutdoorPopulationPass : IWorldGenerationPass
    {
        public string Name => "outdoor-population";
        public GenerationPhase Phase => GenerationPhase.Population;

        public bool SupportsTemplate(WorldGenerationTemplate template) => template == WorldGenerationTemplate.Outdoor;

        public void Execute(WorldGenerationContext context)
        {
            if (context.World == null)
            {
                context.AddError("Outdoor population requires world instance");
                return;
            }

            var world = context.World;
            EnsureMonsterTileType(world);
            var rng = context.GeneratorContext.GetRandom("outdoor:population");

            var plainsTiles = world.EntitiesByLocation.Keys
                .Where(loc => world.PassableTerrain(loc) && world.GetTerrainType(loc)?.Name == "Plains")
                .Take(200)
                .ToList();

            if (plainsTiles.Count == 0)
                return;

            var traderLoc = plainsTiles[rng.Next(plainsTiles.Count)];
            var npc = new Monster(world);
            npc.Set(traderLoc);
            npc.Set(new Goal { Created = DateTime.UtcNow, Location = traderLoc });
            world.AddEntity(npc);

            var snakeLoc = plainsTiles[rng.Next(plainsTiles.Count)];
            var snake = new Snake();
            snake.Set(snakeLoc);
            world.AddEntity(snake);
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


