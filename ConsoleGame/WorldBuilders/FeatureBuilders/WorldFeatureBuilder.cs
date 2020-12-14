using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using ConsoleGame.Core;

namespace ConsoleGame.WorldBuilders
{
    public abstract class WorldFeatureBuilder
    {
        World world;
        WorldFeature feature;

        public WorldFeatureBuilder(World world, WorldFeature feature)
        {
            this.world = world;
            this.feature = feature;
        }

        public abstract World Build(WorldBuilderOptions options = null);
    }
}
