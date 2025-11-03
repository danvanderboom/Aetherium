using System;

namespace Aetherium.Unity.Model
{
    /// <summary>
    /// Unity-friendly representation of VisualDto (simplified for tilemap rendering).
    /// </summary>
    [Serializable]
    public class VisualLite
    {
        public WorldLocationLite Location { get; set; } = new WorldLocationLite();
        public string TileTypeId { get; set; } = string.Empty;
        public double LightLevel { get; set; } = 1.0;

        public VisualLite()
        {
        }

        public VisualLite(WorldLocationLite location, string tileTypeId, double lightLevel)
        {
            Location = location;
            TileTypeId = tileTypeId;
            LightLevel = lightLevel;
        }
    }
}

