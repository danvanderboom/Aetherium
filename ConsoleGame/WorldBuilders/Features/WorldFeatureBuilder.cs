using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using ConsoleGame.Core;

namespace ConsoleGame.WorldBuilders
{
    public abstract class WorldFeatureBuilder
    {
        protected World World { get; set; }

        protected WorldFeature Feature { get; set; }

        public WorldFeatureBuilder(World world, WorldFeature feature)
        {
            World = world;
            Feature = feature;
        }

        public abstract void Build(); // WorldBuilderOptions options = null);

        Random rand = new Random();

        // TODO: move to dedicated Randomizer class?
        protected int RandomSign() => rand.Next(0, 2) == 0 ? 1 : -1;
    }
}
