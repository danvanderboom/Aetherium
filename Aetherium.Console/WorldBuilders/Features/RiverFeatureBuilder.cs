using System;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using Aetherium.Components;
using Aetherium.Core;
using Aetherium.WorldBuilders;

namespace Aetherium.WorldBuilders.Features
{
    public class RiverFeatureBuilder : WorldFeatureBuilder
    {
        Random rand = new Random();

        public RiverFeatureBuilder(World world, WorldFeature feature) : base(world, feature)
        {
        }

        public override void Build() //WorldBuilderOptions? options = null)
        {
            var riverStartY = Feature.Chunk.Location.Y;
            var riverCenter = Feature.Chunk.Location.X + (Feature.Chunk.Size.Width + 1) / 2;
            var riverCenterMaxChange = 2;
            var riverCenterChangeTurns = 3;
            var riverCenterMin = (int)(Feature.Chunk.Location.X - (Feature.Chunk.Size.Width + 1) / 2 * 0.8);
            var riverCenterMax = (int)(Feature.Chunk.Location.X + (Feature.Chunk.Size.Width + 1) / 2 * 0.8);
            var riverMinWidth = 4;
            var riverMaxWidth = Feature.Chunk.Size.Width;
            var riverMaxWidthChange = 2;
            var riverLength = Feature.Chunk.Size.Length;
            var riverMinBorderWidth = Feature.Chunk.Size.Width / 8;
            var riverMaxBorderWidth = Feature.Chunk.Size.Width / 4;
            var riverBorderTerrainName = "Forest";

            var riverWidth = rand.Next(riverMinWidth, riverMaxWidth + 1);

            for (int line = 0; line < riverLength; line++)
            {
                riverWidth = (riverWidth + (RandomSign() * rand.Next(0, riverMaxWidthChange + 1)))
                    .ForceInRange(riverMinWidth, riverMaxWidth);

                if (line % riverCenterChangeTurns == 0)
                    riverCenter = (riverCenter + RandomSign() * rand.Next(0, riverCenterMaxChange + 1))
                        .ForceInRange(riverCenterMin, riverCenterMax);

                var x = riverCenter - ((riverWidth + 1) / 2);
                var y = riverStartY + line;

                // left border of river
                var leftBorderWidth = rand.Next(riverMinBorderWidth, riverMaxBorderWidth + 1);
                World.SetTerrain(riverBorderTerrainName,
                    new WorldLocation(x - leftBorderWidth, y, 0),
                    new Size3d(1, leftBorderWidth, 1));

                // river
                World.SetTerrain("Water",
                    new WorldLocation(x, y, 0),
                    new Size3d(1, riverWidth, 1));

                // right border of river
                var rightBorderWidth = rand.Next(riverMinBorderWidth, riverMaxBorderWidth + 1);
                World.SetTerrain(riverBorderTerrainName,
                    new WorldLocation(x + riverWidth, y, 0),
                    new Size3d(1, rightBorderWidth, 1));

                //var expectedLocationCount = leftBorderWidth + riverWidth + rightBorderWidth;
                //var test = GetTerrain(new Location(x - leftBorderWidth, y, 0), new Size3d(1, expectedLocationCount, 1));
                //if (test.Count != expectedLocationCount)
                //    break;
            }
        }
    }
}

