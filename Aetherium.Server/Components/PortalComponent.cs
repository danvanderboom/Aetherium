using Aetherium.Core;

namespace Aetherium.Components
{
    /// <summary>
    /// Component that marks an entity as a portal, enabling travel between worlds/maps.
    /// </summary>
    public class PortalComponent : Component
    {
        /// <summary>
        /// Unique identifier for this portal.
        /// </summary>
        public string PortalId { get; set; } = string.Empty;

        /// <summary>
        /// Target world ID (resolved at runtime via cluster grain).
        /// </summary>
        public string? TargetWorldId { get; set; }

        /// <summary>
        /// Target map ID within target world.
        /// </summary>
        public string? TargetMapId { get; set; }

        /// <summary>
        /// Target tag (used for link resolution, e.g., "hub", "city", "dungeon").
        /// </summary>
        public string? TargetTag { get; set; }

        /// <summary>
        /// Activation requirement (e.g., "unlocked", "quest_complete:quest-id").
        /// </summary>
        public string? Activation { get; set; }

        /// <summary>
        /// Display name for the portal.
        /// </summary>
        public string DisplayName { get; set; } = "Portal";

        /// <summary>
        /// Whether the portal is active and usable.
        /// </summary>
        public bool IsActive { get; set; } = true;

        public PortalComponent() : base() { }
    }
}

