using System;
using ConsoleGameModel;
using Orleans;

namespace ConsoleGameServer.Management
{
    /// <summary>
    /// Contains information about an active game session.
    /// </summary>
    [GenerateSerializer]
    public class SessionInfo
    {
        [Id(0)]
        public string SessionId { get; set; } = string.Empty;
        
        [Id(1)]
        public string ConnectionId { get; set; } = string.Empty;
        
        [Id(2)]
        public bool DirectionalVisionMode { get; set; }
        
        [Id(3)]
        public int HeadingDegrees { get; set; }
        
        [Id(4)]
        public int FieldOfViewDegrees { get; set; }
        
        [Id(5)]
        public LightingMode LightingMode { get; set; }
        
        [Id(6)]
        public VisionMode VisionMode { get; set; }
        
        [Id(7)]
        public double TimeScale { get; set; }
        
        [Id(8)]
        public DateTime ConnectedAt { get; set; }
    }
}

