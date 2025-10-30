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
        WorldDirection? PreviousDirection;

        World world;

        Random rand;

        public Monster(World world) : base()
        {
            this.world = world;

            rand = new Random();

            Set(new Memory());
            Set(new Tile { Type = world.TileTypes["Monster"] });
            
            // Monsters emit slightly lower heat than characters but still high
            Set(new HeatSignature(0.8, TimeSpan.FromSeconds(8)));
        }

        public virtual void Heartbeat()
        {
            //if (goal == null)
            //    SetGoal();

            var validDirections = GetValidDirections();

            var r = rand.NextDouble();
            if (PreviousDirection.HasValue && validDirections.Contains(PreviousDirection.Value) && r < 0.5)
            {
                world.TryMove(this, PreviousDirection.Value);
                return;
            }

            var direction = validDirections[rand.Next(0, validDirections.Count)];

            if (world.TryMove(this, direction))
            {
                //PerceiveEnvironment();
                PreviousDirection = direction;
            }
        }

        private IList<WorldDirection> GetValidDirections()
        {
            var location = Get<WorldLocation>();
            if (location == null)
                throw new InvalidOperationException("WorldLocation is missing from Monster");

            var directions = Enum.GetValues(typeof(WorldDirection)).Cast<WorldDirection>().ToList();

            if (!location.Has<CanAscend>())
                directions.Remove(WorldDirection.Up);

            if (!location.Has<CanDescend>())
                directions.Remove(WorldDirection.Down);

            var invalidDirections = new List<WorldDirection>();

            foreach (var direction in directions)
            {
                var target = direction switch
                {
                    WorldDirection.North => location.FromDelta(0, +1, 0),
                    WorldDirection.South => location.FromDelta(0, -1, 0),
                    WorldDirection.East => location.FromDelta(+1, 0, 0),
                    WorldDirection.West => location.FromDelta(-1, 0, 0),
                    WorldDirection.Up => location.FromDelta(0, 0, +1),
                    WorldDirection.Down => location.FromDelta(0, 0, -1),
                    _ => throw new NotImplementedException()
                };

                if (!world.PassableTerrain(target))
                    invalidDirections.Add(direction);
            }

            foreach (var item in invalidDirections)
                directions.Remove(item);

            return directions;
        }

        //public void SetGoal()
        //{
        //    // find the strongest recent memories of forest terrain
        //    var memories = Get<Memory>().AllSpaceTimeMemories
        //        .Where(m => m.ContentType == "Terrain"
        //            && m.Content == "Forest"
        //            && m.Impressions > 30
        //            && m.TimeSinceLastSeen.TotalMinutes < 120)
        //        .OrderByDescending(m => m.Impressions)
        //        .Take(3)
        //        .ToList();

        //    Location destination;

        //    // if forest memories exist, there's a strong chance they'll be chosen (90%)
        //    var useMemoryForSettingGoal = memories.Any() && rand.NextDouble() < 0.90;

        //    if (useMemoryForSettingGoal)
        //    {
        //        var memory = memories[rand.Next(0, memories.Count)];
        //        destination = memory.Location;
        //    }
        //    else
        //    {
        //        destination = world.SelectRandomPassableLocation();
        //    }

        //    goal = new Goal
        //    {
        //        Created = DateTime.Now,
        //        Location = destination
        //    };
        //}

        //private void PerceiveEnvironment()
        //{
        //    RememberThisTerrain();
        //}

        //public void RememberThisTerrain()
        //{
        //    var location = Get<Location>();

        //    var bias = world.GetTerrain(location) switch
        //    {
        //        TerrainType.Forest => 1,
        //        TerrainType.Cave => 0.8,
        //        TerrainType.Indoors => 0.1,
        //        TerrainType.Road => 0.3,
        //        TerrainType.Plains => 0.3,
        //        _ => 0.5
        //    };

        //    Get<Memory>().Remember(location, "Terrain", world.GetTerrain(location).ToString());
        //}

        public WorldDirection SelectRandomDirection()
        {
            return (WorldDirection)rand.Next(0, Enum.GetNames(typeof(WorldDirection)).Length);
        }
    }
}
