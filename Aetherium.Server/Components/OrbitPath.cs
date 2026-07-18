using System.Collections.Generic;
using Aetherium.Core;

namespace Aetherium.Components
{
    /// <summary>
    /// A precomputed closed orbit: a ring of cells the bearer steps around, one per <see cref="TicksPerStep"/>
    /// ticks, looping forever. On the sphere the ring is an H3 gridRing at a fixed band, so the orbit is a
    /// loop over the surface at altitude; because each orbiter rides its own band, orbits can criss-cross in
    /// projection and never collide (they never share a cell — the Z differs). The ring is built once at
    /// spawn (worldgen) and just replayed at runtime, so the movement system needs no topology knowledge.
    /// </summary>
    public class OrbitPath : Component
    {
        /// <summary>The ordered ring of cells (adjacent, same band). Stepped modulo its length.</summary>
        public List<WorldLocation> Ring { get; set; } = new();

        /// <summary>Position along the ring.</summary>
        public int Cursor { get; set; }

        /// <summary>Ticks between one-cell advances — bigger is slower, so orbiters drift at different rates.</summary>
        public int TicksPerStep { get; set; } = 1;

        /// <summary>Tick accumulator toward the next advance.</summary>
        public int TickAccum { get; set; }

        public OrbitPath() : base() { }
    }
}
