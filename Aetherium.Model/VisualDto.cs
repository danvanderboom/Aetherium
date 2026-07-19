using System;
using System.Collections.Generic;

namespace Aetherium.Model
{
    public class VisualDto
    {
        public WorldLocationDto Location { get; set; } = new WorldLocationDto();

        /// <summary>
        /// The terrain tile type at this cell, by <em>reference</em>: the key into the frame's
        /// <see cref="PerceptionDto.TileTypes"/> palette (which carries the full definition — name +
        /// settings). Null when the cell has no terrain (e.g. open air in the vertical slab).
        ///
        /// This replaced a full embedded <c>TileTypeDto Terrain</c> per cell. Measured, that per-cell
        /// copy was ~43% of a frame's bytes even though a viewport typically shows only a couple of
        /// distinct terrains — the same definition was serialized once per visible cell. Sending the id
        /// and resolving against the palette collapses that duplication. (perception efficiency)
        /// </summary>
        public string? TileTypeId { get; set; }

        public List<TileTypeDto> Entities { get; set; } = new List<TileTypeDto>();
        public double LightLevel { get; set; } = 1.0;

        // Things seen at this location (character, objects, etc.)
        public Dictionary<VisualType, int> ThingsSeen { get; set; } = new Dictionary<VisualType, int>();

        public VisualDto()
        {
        }

        public VisualDto(WorldLocationDto location, string? tileTypeId)
        {
            Location = location;
            TileTypeId = tileTypeId;
        }
    }
}




