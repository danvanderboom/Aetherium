using System.Collections.Generic;

namespace Aetherium.Topology
{
    /// <summary>
    /// A world's tiling: how cells connect, measure, and face (docs/grid-topologies.md).
    /// Implementations are stateless singletons resolved once at grain init from the
    /// per-world <c>topology</c> config field onto <see cref="Aetherium.Core.World.Topology"/>.
    ///
    /// Two structural rules keep this general enough for triangles (3 parity-dependent
    /// edges) and H3 (6 edges, except 12 pentagons with 5):
    /// <list type="number">
    /// <item><b>Every direction query takes the cell</b> — uniform grids ignore it;
    /// non-uniform grids depend on it.</item>
    /// <item><b>All relative movement resolves angularly against degree headings</b> —
    /// see <see cref="ResolveRelative"/>; the 4-value <see cref="RelativeDirection"/>
    /// wire enum is unchanged on every topology.</item>
    /// </list>
    ///
    /// The topology governs the XY plane only; the Z axis (levels, stairs, lifts) is
    /// orthogonal and engine-level (invariant 8).
    /// </summary>
    public interface IGridTopology
    {
        /// <summary>Registry name: "square" | "hex" | "tri" | (later) "h3".</summary>
        string Name { get; }

        /// <summary>True when every cell has the same direction set (square, hex) —
        /// enables table fast paths. Callers must not assume it (invariant 5).</summary>
        bool HasUniformDirections { get; }

        /// <summary>Upper bound on <see cref="DirectionCount"/> — buffer sizing only.</summary>
        int MaxDirectionCount { get; }

        // ---- per-cell direction machinery (the triangle/pentagon-proof core) ----

        int DirectionCount(GridCoord cell);

        /// <summary>The cell's outgoing edge at <paramref name="directionIndex"/>
        /// (0 ≤ index &lt; <see cref="DirectionCount"/>). Indices are ephemeral —
        /// never persist or wire them.</summary>
        EdgeStep GetStep(GridCoord cell, int directionIndex);

        IEnumerable<EdgeStep> Steps(GridCoord cell);

        IEnumerable<GridCoord> Neighbors(GridCoord cell);

        // ---- metric & geometry (same-Z; the Z axis stays engine-level) ----

        /// <summary>Graph metric on the XY plane: minimum number of edge crossings.
        /// A true metric with Distance == 1 ⇔ adjacent (invariant 2). Z is ignored —
        /// callers add vertical terms themselves.</summary>
        int Distance(GridCoord a, GridCoord b);

        /// <summary>Cells from <paramref name="a"/> to <paramref name="b"/> inclusive,
        /// connected under <see cref="Neighbors"/> (invariant 4). Used by FOV/lighting
        /// raycasts, which skip the origin cell themselves.</summary>
        IEnumerable<GridCoord> Line(GridCoord a, GridCoord b);

        /// <summary>Exactly the ball { x : Distance(center, x) ≤ radius } (invariant 3),
        /// including the center. Same Z as the center.</summary>
        IEnumerable<GridCoord> Range(GridCoord center, int radius);

        // ---- heading machinery — degrees remain the engine-wide source of truth ----

        /// <summary>The nearest legal facing at this cell (ties clockwise). Uniform
        /// topologies snap to their edge headings; square: nearest multiple of 90°.</summary>
        int SnapHeading(GridCoord cell, int degrees);

        /// <summary>Rotate-preset granularity at this cell: 90 (square) / 60 (hex) /
        /// 120 (triangle — cycling the cell's own edges).</summary>
        int TurnStepDegrees(GridCoord cell);

        /// <summary>Index of the outgoing edge nearest <paramref name="degrees"/>
        /// (ties clockwise), or null when the cell has no edges.</summary>
        int? HeadingToDirectionIndex(GridCoord cell, int degrees);

        /// <summary>
        /// Resolves a relative move against the actor's heading: target angle =
        /// heading + {F:0°, R:90°, B:180°, L:270°}; the outgoing edge nearest that
        /// angle wins, with deterministic tie-breaks (a) toward forward, then
        /// (b) clockwise. On square this is byte-identical to the legacy
        /// cardinalize-then-rotate pair (pinned by golden tests).
        /// </summary>
        RelativeMoveResolution ResolveRelative(GridCoord cell, int headingDegrees,
                                               Aetherium.Model.RelativeDirection move);

        // ---- local planar embedding for cones/falloff — the H3-proofing method ----

        /// <summary>
        /// Vector from → to in the local tangent plane, in cell-size units, in the
        /// engine's screen-style axes (+X east, +Y south; north = -Y). Planar
        /// topologies: cell-center difference. H3 later: azimuthal projection of the
        /// great-circle displacement. Only ever called at perception range — exactly
        /// where a tangent plane is valid on a sphere. Runtime systems (vision cones,
        /// light falloff) must use this, never absolute cell centers.
        /// </summary>
        (double X, double Y) Delta(GridCoord from, GridCoord to);
    }

    /// <summary>
    /// Planar-only extras (square/hex/tri — NOT implemented by H3, which has no global
    /// plane). Worldgen and debug tooling may use absolute centers; runtime systems
    /// must use <see cref="IGridTopology.Delta"/>.
    /// </summary>
    public interface IPlanarGridTopology : IGridTopology
    {
        (double X, double Y) CellCenter(GridCoord cell);
    }
}
