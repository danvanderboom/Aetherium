using Aetherium.Components;

namespace Aetherium.Topology
{
    /// <summary>
    /// Immutable cell coordinate for topology math — the hot-path mirror of
    /// <see cref="WorldLocation"/> (a mutable class whose hash allocates a string).
    /// Convert at the seam boundary; <see cref="WorldLocation"/>'s type, wire shape,
    /// and hashing are unchanged.
    ///
    /// X and Y are <b>topology-interpreted opaque integers</b>: square reads them as
    /// column/row, hex as axial q/r, triangle as lattice coordinates whose parity
    /// <c>(X+Y)&amp;1</c> picks the cell's orientation. Z is always the vertical level
    /// and is never topology-interpreted (see docs/grid-topologies.md, invariant 8).
    /// </summary>
    public readonly record struct GridCoord(int X, int Y, int Z)
    {
        public static GridCoord From(WorldLocation location) => new(location.X, location.Y, location.Z);

        public WorldLocation ToWorldLocation() => new(X, Y, Z);
    }
}
