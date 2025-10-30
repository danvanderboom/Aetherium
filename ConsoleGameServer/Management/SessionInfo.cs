using System;
using ConsoleGameModel;

namespace ConsoleGameServer.Management
{
    /// <summary>
    /// Contains information about an active game session.
    /// </summary>
    public class SessionInfo
    {
        public string SessionId { get; set; } = string.Empty;
        public string ConnectionId { get; set; } = string.Empty;
        public bool DirectionalVisionMode { get; set; }
        public int HeadingDegrees { get; set; }
        public int FieldOfViewDegrees { get; set; }
        public LightingMode LightingMode { get; set; }
        public VisionMode VisionMode { get; set; }
        public double TimeScale { get; set; }
        public DateTime ConnectedAt { get; set; }
    }
}

