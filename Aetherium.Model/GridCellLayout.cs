using System;

namespace Aetherium.Model
{
    /// <summary>
    /// Client-side layout math for the grid topologies (docs/grid-topologies.md). The server
    /// streams relative cell coordinates ("relX,relY,relZ" keys) plus <see cref="PerceptionDto.Topology"/>
    /// and <see cref="PerceptionDto.SelfCellParity"/>; how those cells land on a screen is purely a
    /// client concern, and this is the one shared implementation of it. All methods are pure.
    ///
    /// Character-grid layout (the console renderer, monitoring captures): a cell is
    /// <see cref="CellCharWidth"/> characters wide, and <see cref="CharColumnOffset"/> gives each
    /// relative cell's column offset (in characters) from the perceiver's own column. Square cells
    /// sit on a plain 2-char grid; hex rows shift half a cell (one character) per row — the
    /// column formula <c>2·relX + relY</c> is exactly the pointy-top axial embedding
    /// <c>x = q + r/2</c> in characters, which renders as the classic honeycomb stagger; triangle
    /// cells are half-width (one character), matching the lattice's half-unit x pitch. H3 local-IJ
    /// coordinates are hex axes, so "h3" lays out like hex.
    ///
    /// Continuous layout (Unity, any pixel renderer): <see cref="CellLayoutPosition"/> returns the
    /// cell center in cell units, mirroring the server's planar embeddings
    /// (<c>SquareTopology</c>/<c>HexTopology</c>/<c>TriangleTopology.CellCenter</c>) so adjacent
    /// centers are one unit apart on every tiling.
    /// </summary>
    public static class GridCellLayout
    {
        private static readonly double Sqrt3Over2 = Math.Sqrt(3.0) / 2.0;
        private static readonly double Sqrt3Over6 = Math.Sqrt(3.0) / 6.0;

        /// <summary>Cell width in characters for character-grid renderers.</summary>
        public static int CellCharWidth(string? topology) =>
            string.Equals(topology, "tri", StringComparison.OrdinalIgnoreCase) ? 1 : 2;

        /// <summary>
        /// Column offset, in characters, of the relative cell (relX, relY) from the perceiver's
        /// own column. Rows map 1:1 to relY on every topology; only columns differ.
        /// </summary>
        public static int CharColumnOffset(string? topology, int relX, int relY) => Normalize(topology) switch
        {
            "hex" or "h3" => 2 * relX + relY,
            "tri" => relX,
            _ => 2 * relX,
        };

        /// <summary>
        /// Characters of leading stagger for a screen row: hex rows with odd relY shift right by
        /// one character (half a cell), producing the honeycomb; every other topology has none.
        /// Equivalent to decomposing <see cref="CharColumnOffset"/>'s <c>2·relX + relY</c> into a
        /// whole-cell shift (see <see cref="RelXForCellIndex"/>) plus this sub-cell remainder.
        /// </summary>
        public static int RowStaggerChars(string? topology, int relY) => Normalize(topology) switch
        {
            "hex" or "h3" => relY & 1,
            _ => 0,
        };

        /// <summary>
        /// For cell-driven render loops (a row of fixed-width cells, plus the row's stagger): the
        /// relative X of the <paramref name="cellIndex"/>-th cell on the row at
        /// <paramref name="relY"/>, where <paramref name="xoffset"/> is the cell index of the
        /// perceiver's column. On hex the visible axial window slides left by one cell every two
        /// rows (arithmetic shift = floor, so the honeycomb stays aligned across relY = 0).
        /// </summary>
        public static int RelXForCellIndex(string? topology, int cellIndex, int relY, int xoffset) => Normalize(topology) switch
        {
            "hex" or "h3" => cellIndex - xoffset - (relY >> 1),
            _ => cellIndex - xoffset,
        };

        /// <summary>
        /// Triangle only: the (x+y)&amp;1 parity of the cell at (relX, relY), derived from the
        /// perceiver's own parity — 0 means up-pointing, 1 means down-pointing. Null on every
        /// other topology (or when the server sent no parity). Parity flips with every unit of
        /// relX or relY, so it is (self + relX + relY) mod 2.
        /// </summary>
        public static int? CellParity(string? topology, int? selfCellParity, int relX, int relY) =>
            Normalize(topology) == "tri" && selfCellParity is int parity
                ? ((parity + relX + relY) % 2 + 2) % 2
                : (int?)null;

        /// <summary>
        /// The relative cell's center position in cell units for continuous (pixel) renderers,
        /// +X right and +Y down, mirroring the server's normalized planar embeddings: adjacent
        /// cell centers are exactly one unit apart on every tiling.
        /// </summary>
        public static (double X, double Y) CellLayoutPosition(string? topology, int relX, int relY, int? selfCellParity = null)
            => Normalize(topology) switch
            {
                "hex" or "h3" => (relX + relY / 2.0, Sqrt3Over2 * relY),
                // Triangle centers dip by √3/6 on down-cells; positions are relative to the
                // perceiver's own center, so its parity dip subtracts out.
                "tri" => (0.5 * relX,
                    Sqrt3Over2 * relY
                    - Sqrt3Over6 * ((CellParity("tri", selfCellParity ?? 0, relX, relY) ?? 0) - (selfCellParity ?? 0))),
                _ => (relX, relY),
            };

        private static string Normalize(string? topology) =>
            string.IsNullOrEmpty(topology) ? "square" : topology.ToLowerInvariant();
    }
}
