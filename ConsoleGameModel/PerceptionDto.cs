using System;
using System.Collections.Generic;

namespace ConsoleGameModel
{
    public class PerceptionDto
    {
        public WorldLocationDto PlayerLocation { get; set; } = new WorldLocationDto();
        public WorldDirection PlayerHeading { get; set; } = WorldDirection.North;
        public Dictionary<string, VisualDto> Visuals { get; set; } = new Dictionary<string, VisualDto>();
        public RectangleDto VisibleBounds { get; set; } = new RectangleDto();
        public Guid UpdateTimestamp { get; set; } = Guid.NewGuid();
        public Dictionary<string, TileTypeDto> TileTypes { get; set; } = new Dictionary<string, TileTypeDto>();

        // Inventory and interactions (AI-friendly)
        public InventoryDto? Inventory { get; set; }
        public List<ItemDto> VisibleItems { get; set; } = new List<ItemDto>();
        public List<AffordanceDto> Affordances { get; set; } = new List<AffordanceDto>();

        public PerceptionDto()
        {
        }
    }
}

