using System;

namespace Aetherium.Client.Contracts
{
    /// <summary>
    /// Client-side layout math for the grid topologies — the MIRROR of
    /// <c>Aetherium.Model.GridCellLayout</c> (which Unity builds can't reference; Orleans).
    /// Kept in lockstep by <c>GridCellLayoutDriftTests</c>, which compares every method
    /// against the server-side original across a coordinate window. See the original for
    /// the full doc; briefly: <see cref="CellLayoutPosition"/> returns a relative cell's
    /// center in cell units (+X right, +Y down), mirroring the server topologies' planar
    /// embeddings so client visuals and server geometry agree about where a cell is.
    /// </summary>
    public static class GridCellLayout
    {
        private static readonly double Sqrt3Over2 = Math.Sqrt(3.0) / 2.0;
        private static readonly double Sqrt3Over6 = Math.Sqrt(3.0) / 6.0;

        /// <summary>Cell width in characters for character-grid renderers.</summary>
        public static int CellCharWidth(string? topology) =>
            string.Equals(topology, "tri", StringComparison.OrdinalIgnoreCase) ? 1 : 2;

        /// <summary>Column offset, in characters, of the relative cell from the perceiver's column.</summary>
        public static int CharColumnOffset(string? topology, int relX, int relY) => Normalize(topology) switch
        {
            "hex" => 2 * relX + relY,
            "h3" => 2 * relX + relY,
            "tri" => relX,
            _ => 2 * relX,
        };

        /// <summary>Characters of leading stagger for a screen row (hex honeycomb).</summary>
        public static int RowStaggerChars(string? topology, int relY) => Normalize(topology) switch
        {
            "hex" => relY & 1,
            "h3" => relY & 1,
            _ => 0,
        };

        /// <summary>Relative X of the cellIndex-th cell on a row, for cell-driven render loops.</summary>
        public static int RelXForCellIndex(string? topology, int cellIndex, int relY, int xoffset) => Normalize(topology) switch
        {
            "hex" => cellIndex - xoffset - (relY >> 1),
            "h3" => cellIndex - xoffset - (relY >> 1),
            _ => cellIndex - xoffset,
        };

        /// <summary>Triangle only: the up(0)/down(1) parity of the cell, derived from the
        /// perceiver's own parity. Null on other topologies or without a server parity.</summary>
        public static int? CellParity(string? topology, int? selfCellParity, int relX, int relY) =>
            Normalize(topology) == "tri" && selfCellParity is int parity
                ? ((parity + relX + relY) % 2 + 2) % 2
                : (int?)null;

        /// <summary>The relative cell's center in cell units for continuous (pixel) renderers,
        /// relative to the perceiver's own center; adjacent centers are one unit apart.</summary>
        public static (double X, double Y) CellLayoutPosition(string? topology, int relX, int relY, int? selfCellParity = null)
            => Normalize(topology) switch
            {
                "hex" => (relX + relY / 2.0, Sqrt3Over2 * relY),
                "h3" => (relX + relY / 2.0, Sqrt3Over2 * relY),
                "tri" => (0.5 * relX,
                    Sqrt3Over2 * relY
                    - Sqrt3Over6 * ((CellParity("tri", selfCellParity ?? 0, relX, relY) ?? 0) - (selfCellParity ?? 0))),
                _ => (relX, relY),
            };

        private static string Normalize(string? topology) =>
            string.IsNullOrEmpty(topology) ? "square" : topology!.ToLowerInvariant();
    }
}
