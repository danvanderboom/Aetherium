using System.Collections.Generic;

namespace Aetherium.Model
{
    /// <summary>
    /// Audio perception data for a location
    /// </summary>
    public class AudioPerceptionDto
    {
        /// <summary>
        /// Current biome ID (e.g., "forest", "dungeon", "plains")
        /// </summary>
        public string? Biome { get; set; }

        /// <summary>
        /// Danger level (0.0 = safe, 1.0 = extreme danger)
        /// Used for adaptive music selection
        /// </summary>
        public float DangerLevel { get; set; } = 0.0f;

        /// <summary>
        /// Reverb preset identifier (e.g., "hall", "cave", "room", "outdoor")
        /// </summary>
        public string ReverbPreset { get; set; } = "outdoor";

        /// <summary>
        /// Occlusion value (0.0 = no occlusion, 1.0 = fully occluded)
        /// </summary>
        public float Occlusion { get; set; } = 0.0f;

        /// <summary>
        /// Ambient emitters with positions and track names
        /// Key: emitter ID, Value: (x, y, z, trackName)
        /// </summary>
        public Dictionary<string, AmbientEmitterDto> AmbientEmitters { get; set; } = new Dictionary<string, AmbientEmitterDto>();

        /// <summary>
        /// Suggested music track for current state (without extension)
        /// </summary>
        public string? SuggestedMusicTrack { get; set; }

        /// <summary>
        /// Footstep material type (e.g., "grass", "stone", "water", "dirt")
        /// </summary>
        public string FootstepMaterial { get; set; } = "stone";
    }

    /// <summary>
    /// Ambient audio emitter at a fixed position
    /// </summary>
    public class AmbientEmitterDto
    {
        /// <summary>
        /// Relative X position from player
        /// </summary>
        public int X { get; set; }

        /// <summary>
        /// Relative Y position from player
        /// </summary>
        public int Y { get; set; }

        /// <summary>
        /// Relative Z position from player
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

