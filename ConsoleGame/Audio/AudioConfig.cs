namespace ConsoleGame.Audio
{
    /// <summary>
    /// Configuration for audio system
    /// </summary>
    public class AudioConfig
    {
        /// <summary>
        /// Whether audio is enabled
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Music volume (0.0 to 1.0)
        /// </summary>
        public float MusicVolume { get; set; } = 0.5f;

        /// <summary>
        /// Sound effects volume (0.0 to 1.0)
        /// </summary>
        public float EffectsVolume { get; set; } = 0.7f;

        /// <summary>
        /// Default music track to play on startup
        /// </summary>
        public string DefaultMusicTrack { get; set; } = "mellow-guitar-loop";

        /// <summary>
        /// Path to audio assets directory
        /// </summary>
        public string AssetPath { get; set; } = "Assets/Audio";

        /// <summary>
        /// Music file extensions to try (in order of preference)
        /// </summary>
        public string[] MusicExtensions { get; set; } = new[] { ".mp3", ".wav", ".ogg" };

        /// <summary>
        /// Sound effect file extensions to try
        /// </summary>
        public string[] EffectExtensions { get; set; } = new[] { ".wav", ".mp3" };
    }
}

