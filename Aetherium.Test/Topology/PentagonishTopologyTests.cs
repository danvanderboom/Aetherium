using System.Linq;
using NUnit.Framework;
using Aetherium.Topology;

namespace Aetherium.Test.Topology
{
    /// <summary>
    /// The permanent regression guard (docs/grid-topologies.md P3): an intentionally irregular
    /// topology with a mix of 5- and 6-neighbor cells is run through the same 8-invariant harness
    /// every real topology passes. If any seam code ever reintroduces a "cells all have N edges"
    /// or "there is a global plane" assumption, this fails long before H3 depends on the machinery.
    /// </summary>
    [TestFixture]
    public class PentagonishTopologyTests
    {
        private static readonly PentagonishTopology P = PentagonishTopology.Instance;

        [Test]
        public void Pentagonish_SatisfiesAllInvariants() => GridTopologyInvariants.AssertAll(P);

        [Test]
        public void It_Really_Has_A_Mix_Of_5_And_6_Neighbor_Cells()
        {
            // y-even rows are pentagons (5), y-odd rows are hexagons (6) — the whole point.
            Assert.That(P.DirectionCount(new GridCoord(0, 0, 0)), Is.EqualTo(5), "even/even is a pentagon");
            Assert.That(P.DirectionCount(new GridCoord(1, 0, 0)), Is.EqualTo(5), "odd/even is a pentagon");
            Assert.That(P.DirectionCount(new GridCoord(0, 1, 0)), Is.EqualTo(6), "any/odd is a hexagon");
            Assert.That(P.HasUniformDirections, Is.False);

            var counts = Enumerable.Range(-3, 7)
                .SelectMany(y => Enumerable.Range(-3, 7).Select(x => P.DirectionCount(new GridCoord(x, y, 0))))
                .Distinct().OrderBy(n => n).ToArray();
            Assert.That(counts, Is.EqualTo(new[] { 5, 6 }), "the mock must exercise both direction counts");
        }
    }
}
