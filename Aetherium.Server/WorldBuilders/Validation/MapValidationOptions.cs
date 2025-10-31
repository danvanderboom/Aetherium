using Aetherium.Components;

namespace Aetherium.WorldBuilders.Validation
{
    /// <summary>
    /// Options for map validation, allowing customization of which checks are performed
    /// and what criteria must be met.
    /// </summary>
    public sealed class MapValidationOptions
    {
        /// <summary>
        /// The Z-level (height/depth) to validate. Defaults to 0.
        /// </summary>
        public int ZLevel { get; init; } = 0;

        /// <summary>
        /// If true, requires explicit impassable terrain (walls, mountains, etc.) at map boundaries.
        /// If false, allows implicit boundaries (locations simply don't exist in the world).
        /// Defaults to true.
        /// </summary>
        public bool RequireExplicitBoundary { get; init; } = true;

        /// <summary>
        /// If true, requires at least one enabled LightSource at the specified Z-level.
        /// Defaults to true.
        /// </summary>
        public bool RequireLightSource { get; init; } = true;

        /// <summary>
        /// Optional start location to validate. If provided, checks that it exists,
        /// is passable, and optionally that enough terrain is reachable from it.
        /// </summary>
        public WorldLocation? StartLocation { get; init; }

        /// <summary>
        /// Minimum number of reachable locations from the start location (if StartLocation is provided).
        /// If null, only checks that start location exists and is passable.
        /// </summary>
        public int? MinReachableLocations { get; init; }
    }
}


