using Aetherium.Components;

namespace Aetherium.Core
{
    /// <summary>
    /// The surface a descending flyer comes to rest on: the top band of the next obstruction below it.
    /// A flyer lands co-located with this band (rendered on top). The surface is either a terrain top
    /// (ground floor or a tall terrain peak, gated by terrain type) or a structure top (a monorail, bridge,
    /// or building, on which any landing-capable flyer may rest).
    /// </summary>
    public readonly struct LandingSurface
    {
        /// <summary>The cell (including the resting band) the flyer would occupy once landed.</summary>
        public WorldLocation Cell { get; }

        /// <summary>The terrain type of the surface when it is a terrain top; null when the surface is a structure.</summary>
        public TerrainType? Terrain { get; }

        public LandingSurface(WorldLocation cell, TerrainType? terrain)
        {
            Cell = cell;
            Terrain = terrain;
        }

        /// <summary>True when the resting surface is terrain (ground floor or terrain peak) rather than a structure.</summary>
        public bool IsTerrain => Terrain != null;
    }
}
