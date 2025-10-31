using System.Collections.Generic;

namespace Aetherium.Server.Audio
{
    /// <summary>
    /// Audio profile for a biome or room type
    /// </summary>
    public class BiomeAudioProfile
    {
        /// <summary>
        /// Unique identifier for this profile (e.g., "forest", "dungeon", "plains")
        /// </summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// Display name for this profile
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Primary ambient soundscape loop track name (without extension)
        /// </summary>
        public string? AmbientLoop { get; set; }

        /// <summary>
        /// Music track for exploration state (without extension)
        /// </summary>
        public string? ExplorationMusic { get; set; }

        /// <summary>
        /// Music track for danger/combat state (without extension)
        /// </summary>
        public string? DangerMusic { get; set; }

        /// <summary>
        /// Footstep material type (e.g., "grass", "stone", "water", "dirt")
        /// </summary>
        public string FootstepMaterial { get; set; } = "stone";

        /// <summary>
        /// Reverb preset identifier (e.g., "hall", "cave", "room", "outdoor")
        /// </summary>
        public string ReverbPreset { get; set; } = "outdoor";

        /// <summary>
        /// Base occlusion value (0.0 = no occlusion, 1.0 = fully occluded)
        /// </summary>
        public float BaseOcclusion { get; set; } = 0.0f;

        /// <summary>
        /// Additional ambient emitters at fixed positions (relative to zone)
        /// Key: emitter ID, Value: (x, y, z, trackName)
        /// </summary>
        public Dictionary<string, AmbientEmitter> AmbientEmitters { get; set; } = new Dictionary<string, AmbientEmitter>();
    }

    /// <summary>
    /// Ambient audio emitter at a fixed position
    /// </summary>
    public class AmbientEmitter
    {
        /// <summary>
        /// Relative X position within the zone
        /// </summary>
        public int X { get; set; }

        /// <summary>
        /// Relative Y position within the zone
        /// </summary>
        public int Y { get; set; }

        /// <summary>
        /// Relative Z position within the zone
        /// </summary>
        public int Z { get; set; }

        /// <summary>
        /// Audio track name (without extension)
        /// </summary>
        public string TrackName { get; set; } = string.Empty;

        /// <summary>
        /// Volume multiplier (0.0 to 1.0)
        /// </summary>
        public float Volume { get; set; } = 0.5f;

        /// <summary>
        /// Whether to loop this emitter
        /// </summary>
        public bool Loop { get; set; } = true;
    }
}

