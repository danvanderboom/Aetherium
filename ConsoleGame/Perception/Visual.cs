using System;
using System.Linq;
using System.Collections.Generic;
using ConsoleGame.Core;

namespace ConsoleGame.Components
{
    public class Visual
    {
        public Location Location { get; set; }

        public TileType? Terrain { get; set; }

        public Dictionary<VisualType, Dictionary<string, double>> ThingsSeen { get; set; }

        public Visual() { }

        public Visual(Location location, TileType? terrain)
        {
            Location = location;
            Terrain = terrain;
            ThingsSeen = new Dictionary<VisualType, Dictionary<string, double>>();
        }
    }
}
