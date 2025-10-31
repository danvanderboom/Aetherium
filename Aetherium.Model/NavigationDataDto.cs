using Aetherium.Model;

namespace Aetherium.Model
{
    /// <summary>
    /// Navigation information provided by compass or similar items
    /// </summary>
    public class NavigationDataDto
    {
        /// <summary>
        /// Whether the player has a compass or navigation tool
        /// </summary>
        public bool HasCompass { get; set; }

        /// <summary>
        /// Current heading in degrees (0-359, where 0 is North)
        /// </summary>
        public int HeadingDegrees { get; set; }

        /// <summary>
        /// Cardinal direction the player is facing
        /// </summary>
        public WorldDirection CardinalDirection { get; set; }
    }
}


