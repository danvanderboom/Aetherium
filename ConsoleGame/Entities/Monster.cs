using System;
using System.Linq;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using ConsoleGame.Core;
using ConsoleGame.Components;

namespace ConsoleGame
{
    public class Monster : Character
    {
        GameWorld world;

        Goal goal;

        Random rand;

        public Monster(GameWorld world) : base()
        {
            this.world = world;

            rand = new Random();

            Components.Add(new Mind());
        }

        public void Heartbeat()
        {
            if (goal == null)
                SetGoal();

            var rest = rand.NextDouble() < 0.1; // 10% chance of resting
            if (!rest)
            {
                var direction = SelectRandomDirection();

                if (world.TryMove(this, direction))
                {
                    PerceiveEnvironment();
                    //PreviousDirection = direction;
                }
            }
        }

        public void SetGoal()
        {
            // find the strongest recent memories of forest terrain
            var memories = Get<Mind>().AllSpaceTimeMemories
                .Where(m => m.ContentType == "Terrain"
                    && m.Content == "Forest"
                    && m.Impressions > 30
                    && m.TimeSinceLastSeen.TotalMinutes < 120)
                .OrderByDescending(m => m.Impressions)
                .Take(3)
                .ToList();

            Location destination;

            // if forest memories exist, there's a strong chance they'll be chosen (90%)
            var useMemoryForSettingGoal = memories.Any() && rand.NextDouble() < 0.90;

            if (useMemoryForSettingGoal)
            {
                var memory = memories[rand.Next(0, memories.Count)];
                destination = memory.Location;
            }
            else
            {
                destination = world.SelectRandomPassableLocation();
            }
            
            goal = new Goal
            {
                Created = DateTime.Now,
                Location = destination
            };
        }
        
        private void PerceiveEnvironment()
        {
            RememberThisTerrain();
        }

        public void RememberThisTerrain()
        {
            var location = Get<Location>();

            var bias = world.GetTerrain(location) switch
            {
                TerrainType.Forest => 1,
                TerrainType.Cave => 0.8,
                TerrainType.Indoors => 0.1,
                TerrainType.Road => 0.3,
                TerrainType.Plains => 0.3,
                _ => 0.5
            };

            Get<Mind>().Remember(location, "Terrain", world.Terrain[location.Z, location.Y, location.X].ToString());
        }

        public Direction SelectRandomDirection()
        {
            return (Direction)rand.Next(0, Enum.GetNames(typeof(Direction)).Length);
        }
    }
}
