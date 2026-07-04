using System;
using System.Collections.Generic;
using System.Linq;
using Aetherium;
using Aetherium.Components;
using Aetherium.Core;
using Aetherium.Entities;

namespace Aetherium.WorldGen.Passes
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

            // enemyCount, when supplied, sets the number of wandering monsters; absent => one
            // (the historical single trader). The snake is always placed for flavor. Default draws
            // one monster location then the snake location — identical to the prior behavior.
            var gc = context.GeneratorContext;
            int monsterCount = gc.HasParam("enemyCount")
                ? Math.Min(gc.GetIntParam("enemyCount", 1, min: 0, max: 100000), plainsTiles.Count)
                : 1;

            for (int i = 0; i < monsterCount; i++)
            {
                var monsterLoc = plainsTiles[rng.Next(plainsTiles.Count)];
                var npc = new Monster(world);
                npc.Set(monsterLoc);
                npc.Set(new Goal { Created = DateTime.UtcNow, Location = monsterLoc });
                world.AddEntity(npc);
            }

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



