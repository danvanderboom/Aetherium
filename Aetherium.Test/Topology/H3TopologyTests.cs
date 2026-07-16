using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Aetherium.Core;
using Aetherium.Components;
using Aetherium.Topology;
using H3;
using H3.Algorithms;
using H3.Extensions;
using H3.Model;
using ModelRel = Aetherium.Model.RelativeDirection;

namespace Aetherium.Test.Topology
{
    /// <summary>
    /// H3 tiling (docs/h3-topology.md) — the "implementation, not a redesign" the seam was shaped
    /// for. Covers lossless index packing, the 8-invariant harness over a real hex disc, the
    /// pentagon-specific 5-neighbor properties (the thing PentagonishTopology has been guarding
    /// for), the hierarchy interface, non-planarity, and an end-to-end run of the unchanged engine
    /// (World.TryMoveSteps) over a world whose Topology is H3.
    /// </summary>
    [TestFixture]
    public class H3TopologyTests
    {
        private static readonly H3Topology H = H3Topology.Instance;

        // A land point (≈40°N, 74°W) well away from all 12 pentagons, at resolution 9.
        private static H3Index HexOrigin => H3Index.FromLatLng(new LatLng(0.71, -1.29), 9);

        private static GridCoord Coord(H3Index index) => H3Topology.FromH3((ulong)index);

        private static List<GridCoord> Disk(H3Index center, int k)
            => center.GridDiskDistances(k).Select(r => Coord(r.Index)).ToList();

        [Test]
        public void PacksLosslesslyIntoGridCoord()
        {
            var index = HexOrigin;
            var coord = Coord(index);
            Assert.That(coord.X, Is.GreaterThanOrEqualTo(0), "H3's reserved top bit keeps X non-negative");
            Assert.That(H3Topology.ToH3(coord), Is.EqualTo((ulong)index), "index must round-trip through the coord");
            Assert.That(coord.Z, Is.EqualTo(0));
        }

        [Test]
        public void RegistryResolvesH3()
        {
            Assert.That(GridTopologyRegistry.Get("h3"), Is.SameAs(H));
            Assert.That(GridTopologyRegistry.Names, Does.Contain("h3"));
        }

        [Test]
        public void IsNotPlanar()
        {
            // The seam quarantines absolute CellCenter onto IPlanarGridTopology; H3 has no global
            // plane and must not implement it (runtime systems use Delta instead).
            Assert.That(H, Is.Not.InstanceOf<IPlanarGridTopology>());
            Assert.That(H, Is.InstanceOf<IHierarchicalGridTopology>());
        }

        [Test]
        public void HexDisc_SatisfiesAllInvariants()
        {
            // A radius-3 disc around a pentagon-free hexagon: every cell here has six neighbors,
            // so the full pairwise harness (including H3's own gridDistance/gridPathCells, which
            // are documented as unreliable *across* pentagons) runs cleanly.
            GridTopologyInvariants.AssertAll(H, Disk(HexOrigin, 3), rangeRadius: 2);
        }

        [Test]
        public void Pentagon_HasFiveNeighborsWithSymmetry()
        {
            var pentagon = H3Index.GetPentagons(9).First();
            Assume.That(pentagon.IsPentagon, Is.True);
            var cell = Coord(pentagon);

            Assert.That(H.HasUniformDirections, Is.False, "H3 mixes 5- and 6-neighbor cells");
            Assert.That(H.DirectionCount(cell), Is.EqualTo(5), "a pentagon has five edges");
            Assert.That(H.Steps(cell).Count(), Is.EqualTo(5));

            var neighbors = H.Neighbors(cell).ToList();
            Assert.That(neighbors, Has.Count.EqualTo(5));
            foreach (var n in neighbors)
            {
                Assert.That(H.Distance(cell, n), Is.EqualTo(1), "each pentagon neighbor is one hop away");
                Assert.That(H.Neighbors(n), Does.Contain(cell), "neighbor symmetry holds at the pentagon");
            }

            // Edge headings stay consistent with Delta even at a pentagon (invariant 7).
            foreach (var step in H.Steps(cell))
            {
                var (dx, dy) = H.Delta(cell, step.Target);
                double headingDeg = System.Math.Atan2(dx, -dy) * 180.0 / System.Math.PI;
                if (headingDeg < 0) headingDeg += 360;
                int diff = ((int)System.Math.Round(headingDeg) - step.HeadingDegrees) % 360;
                diff = ((diff + 360) % 360);
                if (diff > 180) diff = 360 - diff;
                Assert.That(diff, Is.LessThanOrEqualTo(1));
            }
        }

        [Test]
        public void Pentagon_TurnStepIs72_HexIs60()
        {
            var pentagon = Coord(H3Index.GetPentagons(9).First());
            Assert.That(H.TurnStepDegrees(pentagon), Is.EqualTo(72));
            Assert.That(H.TurnStepDegrees(Coord(HexOrigin)), Is.EqualTo(60));
        }

        [Test]
        public void ResolveRelative_MovesToAnActualNeighbor()
        {
            var cell = Coord(HexOrigin);
            var neighbors = H.Neighbors(cell).ToHashSet();

            foreach (var move in new[] { ModelRel.Forward, ModelRel.Backward, ModelRel.Left, ModelRel.Right })
            {
                var result = H.ResolveRelative(cell, 90, move);
                Assert.That(result.Success, Is.True, $"{move} must resolve to an edge");
                Assert.That(neighbors, Does.Contain(result.Step.Target), $"{move} must step to a real neighbor");
            }
        }

        [Test]
        public void Hierarchy_ParentAndChildrenNest()
        {
            var index = HexOrigin;
            var cell = Coord(index);

            Assert.That(H.Resolution(cell), Is.EqualTo(9));

            var parent = H.Parent(cell);
            Assert.That(H.Resolution(parent), Is.EqualTo(8));

            var children = H.Children(cell).ToList();
            Assert.That(children, Has.Count.EqualTo(7), "a hexagon has seven finer children");
            Assert.That(children.All(c => H.Resolution(c) == 10), Is.True);

            // The cell is the parent of its own children.
            foreach (var child in children)
                Assert.That(H.Parent(child), Is.EqualTo(cell), "each child's parent is the original cell");
        }

        [Test]
        public void Delta_MagnitudeIsAboutOneCellPerHop()
        {
            var cell = Coord(HexOrigin);
            foreach (var neighbor in H.Neighbors(cell))
            {
                var (dx, dy) = H.Delta(cell, neighbor);
                double mag = System.Math.Sqrt(dx * dx + dy * dy);
                // Normalized to cell-size units, an adjacent cell sits ≈1 unit away — keeping FOV
                // cone/light-falloff math numerically comparable to square/hex.
                Assert.That(mag, Is.EqualTo(1.0).Within(0.15), "adjacent Delta magnitude ≈ 1 cell");
            }
        }

        // ---- end-to-end: the unchanged engine over an H3 world ----

        [Test]
        public void Engine_TryMoveSteps_WalksAnH3Edge()
        {
            var origin = HexOrigin;
            var world = new World();
            var builder = new WorldBuilders.TorusWorldBuilder();
            world.AddTileTypes(builder.TileTypes);
            world.AddTerrainTypes(builder.CreateTerrainTypes(builder.TileTypes));
            world.Topology = H;

            // Passable floor over a radius-2 H3 disc (packed index coords); outside is void.
            foreach (var coord in Disk(origin, 2))
                world.SetTerrain("Indoors", new WorldLocation(coord.X, coord.Y, 0));

            var start = Coord(origin);
            var character = new Character();
            character.Set(new WorldLocation(start.X, start.Y, 0));
            character.Set(new HasHeading { Heading = 90 });
            world.AddEntity(character);

            var neighbors = H.Neighbors(start).ToHashSet();
            var outcome = world.TryMoveSteps(character, 90, ModelRel.Forward, 1);

            Assert.That(outcome.Success, Is.True, "a forward step onto an adjacent H3 cell must succeed");
            var landed = character.Get<WorldLocation>();
            var landedCoord = new GridCoord(landed.X, landed.Y, landed.Z);
            Assert.That(landedCoord, Is.Not.EqualTo(start), "the character actually moved");
            Assert.That(neighbors, Does.Contain(landedCoord), "it landed on a true H3 neighbor of the start cell");
        }
    }
}
