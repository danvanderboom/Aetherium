using System.Collections.Generic;

namespace Aetherium.Topology
{
    /// <summary>
    /// A topology whose cells nest across resolutions (docs/h3-topology.md). Implemented only
    /// by <see cref="H3Topology"/>; square/hex/triangle never expose hierarchy. A single map
    /// still uses exactly one resolution — this interface exists so cross-resolution travel
    /// (survey-from-orbit → land-at-a-base) can be wired through the existing, topology-agnostic
    /// portal system, which links a coarse-resolution map to a fine-resolution one. There is no
    /// hierarchy traversal in the per-step movement path.
    /// </summary>
    public interface IHierarchicalGridTopology : IGridTopology
    {
        /// <summary>The cell's resolution (H3: 0 coarsest … 15 finest).</summary>
        int Resolution(GridCoord cell);

        /// <summary>The single parent cell one resolution coarser.</summary>
        GridCoord Parent(GridCoord cell);

        /// <summary>The cells one resolution finer that tile this cell (7 for a hexagon,
        /// 6 for a pentagon at the center-child boundary).</summary>
        IEnumerable<GridCoord> Children(GridCoord cell);
    }
}
