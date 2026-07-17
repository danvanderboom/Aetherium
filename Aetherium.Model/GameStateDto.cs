using System;
using System.Collections.Generic;

namespace Aetherium.Model
{
    public class GameStateDto
    {
        public string PlayerId { get; set; } = string.Empty;
        // PlayerLocation removed - client should not know absolute world coordinates
        public WorldDirection PlayerHeading { get; set; } = WorldDirection.North;

        /// <summary>
        /// Secret the client stores and presents to GameHub.ResumeSession after a
        /// reconnect to rebind to this session (PlayerId is publicly visible to other
        /// players, so it can't authenticate a resume by itself).
        /// </summary>
        public string ResumeToken { get; set; } = string.Empty;
        public Dictionary<string, TileTypeDto> TileTypes { get; set; } = new Dictionary<string, TileTypeDto>();

        public GameStateDto()
        {
        }
    }
}


