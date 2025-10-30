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

        // Navigation data (compass, maps, etc.)
        public NavigationDataDto? NavigationData { get; set; }

        // Lighting and vision modes
        public LightingMode CurrentLightingMode { get; set; } = LightingMode.Torch;
        public VisionMode CurrentVisionMode { get; set; } = VisionMode.Normal;
        public double GameTimeOfDay { get; set; } = 12.0; // 0-24 hours
        public (double r, double g, double b) AmbientTint { get; set; } = (1.0, 1.0, 1.0);

        public PerceptionDto()
        {
        }
    }
}

