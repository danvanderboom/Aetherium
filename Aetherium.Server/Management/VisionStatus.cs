using Aetherium.Model;
using Orleans;

namespace Aetherium.Server.Management
{
    /// <summary>
    /// Contains vision configuration for a game session.
    /// </summary>
    [GenerateSerializer]
    public class VisionStatus
    {
        [Id(0)]
        public bool DirectionalVisionMode { get; set; }
        
        [Id(1)]
        public int HeadingDegrees { get; set; }
        
        [Id(2)]
        public int FieldOfViewDegrees { get; set; }
        
        [Id(3)]
        public LightingMode LightingMode { get; set; }
        
        [Id(4)]
        public VisionMode VisionMode { get; set; }
    }
}

