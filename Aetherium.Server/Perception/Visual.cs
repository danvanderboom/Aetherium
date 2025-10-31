using System;
using System.Linq;
using System.Collections.Generic;
using Aetherium.Core;

namespace Aetherium.Components
{
    public class Visual
    {
        public WorldLocation Location { get; set; } = WorldLocation.None;

        public TileType? Terrain { get; set; }

        public Dictionary<VisualType, Dictionary<string, double>> ThingsSeen { get; set; }

        public Visual() 
        {
            ThingsSeen = new Dictionary<VisualType, Dictionary<string, double>>();
        }

        public Visual(WorldLocation location, TileType? terrain) : this()
        {
            Location = location;
            Terrain = terrain;
        }
    }
}

