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

            Set(new Mind());
        }

        public void Heartbeat()
        {
            if (goal == null)
                SetGoal();

            var rest = rand.NextDouble() < 0.1; // 10% chance of resting
            if (!rest)
            {
                var validDirections = GetValidDirections();
                var direction = validDirections[rand.Next(0, validDirections.Count)];

                if (world.TryMove(this, direction))
                {
                    PerceiveEnvironment();
                    //PreviousDirection = direction;
                }
            }
        }

        private IList<Direction> GetValidDirections()
        {
            var directions = Enum.GetValues(typeof(Direction)).Cast<Direction>().ToList();

            foreach (var direction in Enum.GetValues(typeof(Direction)).Cast<Direction>())
            {
                var target = direction switch
                {
                    Direction.North => Get<Location>().FromDelta(0, +1, 0),
                    Direction.South => Get<Location>().FromDelta(0, -1, 0),
                    Direction.East => Get<Location>().FromDelta(+1, 0, 0),
                    Direction.West => Get<Location>().FromDelta(-1, 0, 0),
                    Direction.Up => Get<Location>().FromDelta(0, 0, +1),
                    Direction.Down => Get<Location>().FromDelta(0, 0, -1),
                    _ => throw new NotImplementedException()
                };

                if (!world.PassableTerrain(target))
                    directions.Remove(direction);
            }

            return directions;
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

            Get<Mind>().Remember(location, "Terrain", world.GetTerrain(location).ToString());
        }

        public Direction SelectRandomDirection()
        {
            return (Direction)rand.Next(0, Enum.GetNames(typeof(Direction)).Length);
        }
    }
}
