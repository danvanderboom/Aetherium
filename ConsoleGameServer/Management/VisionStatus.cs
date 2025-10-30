using ConsoleGameModel;

namespace ConsoleGameServer.Management
{
    /// <summary>
    /// Contains vision configuration for a game session.
    /// </summary>
    public class VisionStatus
    {
        public bool DirectionalVisionMode { get; set; }
        public int HeadingDegrees { get; set; }
        public int FieldOfViewDegrees { get; set; }
        public LightingMode LightingMode { get; set; }
        public VisionMode VisionMode { get; set; }
    }
}

