using System;
using System.Collections.Generic;

namespace ConsoleGameModel
{
    public class VisualDto
    {
        public WorldLocationDto Location { get; set; } = new WorldLocationDto();
        public TileTypeDto? Terrain { get; set; }
        public List<TileTypeDto> Entities { get; set; } = new List<TileTypeDto>();
        public double LightLevel { get; set; } = 1.0;

        // Things seen at this location (character, objects, etc.)
        public Dictionary<VisualType, int> ThingsSeen { get; set; } = new Dictionary<VisualType, int>();

        public VisualDto()
        {
        }

        public VisualDto(WorldLocationDto location, TileTypeDto? terrain)
        {
            Location = location;
            Terrain = terrain;
        }
    }
}



