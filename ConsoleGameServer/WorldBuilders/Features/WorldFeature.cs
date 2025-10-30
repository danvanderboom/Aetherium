using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using ConsoleGame.WorldBuilders;

namespace ConsoleGame.Core
{
    public class WorldFeature
    {
        public WorldChunk Chunk { get; set; }

        public Func<World, WorldFeature, WorldFeatureBuilder>? FeatureBuilder { get; set; }

        public Dictionary<string, string> Settings { get; set; }

        public WorldFeature()
        {
            Chunk = WorldChunk.Nowhere;
            Settings = new Dictionary<string, string>();
        }
    }
}
