using System;
using System.Collections.Generic;

namespace ConsoleGameModel
{
    public class GameStateDto
    {
        public string PlayerId { get; set; } = string.Empty;
        // PlayerLocation removed - client should not know absolute world coordinates
        public WorldDirection PlayerHeading { get; set; } = WorldDirection.North;
        public Dictionary<string, TileTypeDto> TileTypes { get; set; } = new Dictionary<string, TileTypeDto>();

        public GameStateDto()
        {
        }
    }
}

