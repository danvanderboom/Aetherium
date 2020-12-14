using System;
using System.Linq;
using System.Collections.Generic;
using ConsoleGame.Core;
using ConsoleGame.Components;
using ConsoleGame.Entities;

namespace ConsoleGame.WorldBuilders
{
    public class SpaceHackWorldBuilder : WorldBuilder
    {
        Random rand = new Random();

        public SpaceHackWorldBuilder(World world) : base(world)
        {
        }

        int ForceInRange(int value, int min, int max) => Math.Min(max, Math.Max(min, value));

        int RandomSign() => rand.Next(0, 2) == 0 ? 1 : -1;

        public void Build()
        {
            // river
            var riverStartY = -50;
            var riverCenter = 0;
            var riverCenterMaxChange = 2;
            var riverCenterChangeTurns = 3;
            var riverMinWidth = 4;
            var riverMaxWidth = 12;
            var riverMaxWidthChange = 2;
            var riverLength = 100;
            var riverMinBorderWidth = 3;
            var riverMaxBorderWidth = 9;
            var riverBorderTerrainName = "Forest";

            var riverWidth = rand.Next(riverMinWidth, riverMaxWidth + 1);

            for (int line = 0; line < riverLength; line++)
            {
                riverWidth = ForceInRange(
                    riverWidth + (RandomSign() * rand.Next(0, riverMaxWidthChange + 1)), 
                    riverMinWidth, 
                    riverMaxWidth);

                if (line % riverCenterChangeTurns == 0)
                    riverCenter += RandomSign() * rand.Next(0, riverCenterMaxChange + 1);

                var x = riverCenter - ((riverWidth + 1) / 2);
                var y = riverStartY + line;

                // left border of river
                var leftBorderWidth = rand.Next(riverMinBorderWidth, riverMaxBorderWidth + 1);
                AddTerrain(riverBorderTerrainName, 
                    new WorldLocation(x - leftBorderWidth, y, 0), 
                    new Size3d(1, leftBorderWidth, 1));

                // river
                AddTerrain("Water",
                    new WorldLocation(x, y, 0),
                    new Size3d(1, riverWidth, 1));

                // right border of river
                var rightBorderWidth = rand.Next(riverMinBorderWidth, riverMaxBorderWidth + 1);
                AddTerrain(riverBorderTerrainName, 
                    new WorldLocation(x + riverWidth, y, 0), 
                    new Size3d(1, rightBorderWidth, 1));

                //var expectedLocationCount = leftBorderWidth + riverWidth + rightBorderWidth;
                //var test = GetTerrain(new Location(x - leftBorderWidth, y, 0), new Size3d(1, expectedLocationCount, 1));
                //if (test.Count != expectedLocationCount)
                //    break;
            }
        }

        public override World Build(WorldBuilderOptions options = null)
        {
            throw new NotImplementedException();
        }

        public override World Expand(WorldBuilderOptions options = null)
        {
            throw new NotImplementedException();
        }
    }
}
