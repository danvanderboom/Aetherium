namespace Aetherium.Audio
{
    /// <summary>
    /// 3D position for spatial audio
    /// </summary>
    public struct AudioVector3
    {
        public float X { get; set; }
        public float Y { get; set; }
        public float Z { get; set; }

        public AudioVector3(float x, float y, float z)
        {
            X = x;
            Y = y;
            Z = z;
        }
    }

    /// <summary>
    /// Listener state for 3D audio
    /// </summary>
    public class AudioListenerState
    {
        /// <summary>
        /// Listener position
        /// </summary>
        public AudioVector3 Position { get; set; } = new AudioVector3(0, 0, 0);

        /// <summary>
        /// Forward direction (normalized)
        /// </summary>
        public AudioVector3 Forward { get; set; } = new AudioVector3(0, -1, 0);

        /// <summary>
        /// Up direction (normalized)
        /// </summary>
        public AudioVector3 Up { get; set; } = new AudioVector3(0, 0, 1);
    }

    /// <summary>
    /// Options for audio playback
    /// </summary>
    public class AudioPlaybackOptions
    {
        /// <summary>
        /// Volume (0.0 to 1.0), defaults to effects volume
        /// </summary>
        public float? Volume { get; set; }

        /// <summary>
        /// Whether to loop (defaults to false for effects)
        /// </summary>
        public bool Loop { get; set; } = false;

        /// <summary>
        /// Position for spatial audio (null = non-spatial)
        /// </summary>
        public AudioVector3? Position { get; set; }

        /// <summary>
        /// Maximum distance for attenuation (0 = no distance falloff)
        /// </summary>
        public float MaxDistance { get; set; } = 50.0f;

        /// <summary>
        /// Minimum distance for full volume
        /// </summary>
        public float MinDistance { get; set; } = 1.0f;
    }
}

