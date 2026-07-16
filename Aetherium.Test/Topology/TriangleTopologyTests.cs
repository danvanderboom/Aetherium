using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Aetherium;
using Aetherium.Core;
using Aetherium.Components;
using Aetherium.Topology;
using ModelRel = Aetherium.Model.RelativeDirection;

namespace Aetherium.Test.Topology
{
    /// <summary>
    /// Triangular tiling (docs/grid-topologies.md P2) — the generalization proof. Verifies the
    /// per-cell (parity-dependent) direction sets, the 120° turn preset, the deterministic
    /// Backward tie-break where heading+180° lands exactly between two edges, BFS distance/range
    /// against an independent reference, and an end-to-end move through the unchanged engine.
    /// </summary>
    [TestFixture]
    public class TriangleTopologyTests
    {
        private static readonly TriangleTopology T = TriangleTopology.Instance;

        [Test]
        public void Triangle_SatisfiesAllInvariants() => GridTopologyInvariants.AssertAll(T);

        [Test]
        public void DirectionSets_Depend_On_Cell_Parity()
        {
            var up = new GridCoord(0, 0, 0);   // (0+0) even → up
            var down = new GridCoord(1, 0, 0); // (1+0) odd  → down

            var upHeadings = T.Steps(up).Select(s => s.HeadingDegrees).OrderBy(h => h).ToArray();
            var downHeadings = T.Steps(down).Select(s => s.HeadingDegrees).OrderBy(h => h).ToArray();

            Assert.That(upHeadings, Is.EqualTo(new[] { 60, 180, 300 }), "up-cell edges");
            Assert.That(downHeadings, Is.EqualTo(new[] { 0, 120, 240 }), "down-cell edges");
        }

        [Test]
        public void Every_Edge_Crossing_Flips_Parity()
        {
            foreach (var cell in AllCells(3))
                foreach (var n in T.Neighbors(cell))
                    Assert.That((n.X + n.Y) & 1, Is.Not.EqualTo((cell.X + cell.Y) & 1),
                        $"edge {cell}->{n} did not flip parity");
        }

        [Test]
        public void TurnStep_Is_120() => Assert.That(T.TurnStepDegrees(new GridCoord(0, 0, 0)), Is.EqualTo(120));

        // The signature triangle case: an up-cell has no edge opposite its 60° edge. Facing 60°
        // (Forward is exact), Backward targets 240°, which is equidistant from the 180° and 300°
        // edges AND equidistant from forward — so tie-break (a) ties too, and (b) clockwise
        // decides: 300° is clockwise of 240°, so Backward deterministically takes the 300° edge.
        [Test]
        public void Backward_From_UpCell_UsesClockwiseTieBreak()
        {
            var up = new GridCoord(0, 0, 0);
            const int facing60 = 60;

            var fwd = T.ResolveRelative(up, facing60, ModelRel.Forward);
            Assert.That(fwd.Step.HeadingDegrees, Is.EqualTo(60));
            Assert.That(fwd.Step.Target, Is.EqualTo(new GridCoord(1, 0, 0)));

            var back = T.ResolveRelative(up, facing60, ModelRel.Backward);
            Assert.That(back.Success, Is.True);
            Assert.That(back.Step.HeadingDegrees, Is.EqualTo(300),
                "Backward with no opposite edge must resolve clockwise to the 300° edge.");
            Assert.That(back.Step.Target, Is.EqualTo(new GridCoord(-1, 0, 0)));
        }

        [Test]
        public void SnapHeading_Snaps_To_The_Cells_Own_Edges()
        {
            var up = new GridCoord(0, 0, 0);
            var down = new GridCoord(1, 0, 0);
            // 90° is nearer the up-cell's 60° edge, but nearer the down-cell's 120° edge.
            Assert.That(T.SnapHeading(up, 90), Is.EqualTo(60));
            Assert.That(T.SnapHeading(down, 90), Is.EqualTo(120));
        }

        [Test]
        public void Distance_Matches_Independent_Bfs([Values(2, 3)] int extent)
        {
            var origin = new GridCoord(0, 0, 0);
            var reference = BfsDistances(origin, 6);
            foreach (var (cell, dist) in reference.Where(kv => System.Math.Abs(kv.Key.X) <= extent && System.Math.Abs(kv.Key.Y) <= extent))
                Assert.That(T.Distance(origin, cell), Is.EqualTo(dist), $"distance to {cell}");
        }

        [Test]
        public void Range_Is_The_Bfs_Ball([Range(0, 4)] int r)
        {
            var origin = new GridCoord(0, 0, 0);
            var expected = BfsDistances(origin, r).Where(kv => kv.Value <= r).Select(kv => kv.Key).ToHashSet();
            var actual = T.Range(origin, r).ToHashSet();
            Assert.That(actual, Is.EquivalentTo(expected));
        }

        // ---- end-to-end: the real engine over a triangle world ----

        [Test]
        public void Engine_TryMoveSteps_Crosses_A_Triangle_Edge()
        {
            var world = new World();
            var builder = new WorldBuilders.TorusWorldBuilder();
            world.AddTileTypes(builder.TileTypes);
            world.AddTerrainTypes(builder.CreateTerrainTypes(builder.TileTypes));
            world.Topology = TriangleTopology.Instance;

            foreach (var cell in TriangleTopology.Instance.Range(new GridCoord(0, 0, 0), 5))
                world.SetTerrain("Indoors", new WorldLocation(cell.X, cell.Y, 0));

            var character = new Character();
            character.Set(new WorldLocation(0, 0, 0));        // up-cell
            character.Set(new HasHeading { Heading = 60 });   // snaps to the up-cell's 60° edge
            world.AddEntity(character);

            var outcome = world.TryMoveSteps(character, 60, ModelRel.Forward, 1);
            Assert.That(outcome.Success, Is.True);
            var landed = character.Get<WorldLocation>();
            Assert.That(landed, Is.EqualTo(new WorldLocation(1, 0, 0)));
            Assert.That((landed.X + landed.Y) & 1, Is.EqualTo(1), "crossing an edge lands in a down-cell");
        }

        // ---- helpers ----

        private static IEnumerable<GridCoord> AllCells(int extent)
        {
            for (int y = -extent; y <= extent; y++)
                for (int x = -extent; x <= extent; x++)
                    yield return new GridCoord(x, y, 0);
        }

        private static Dictionary<GridCoord, int> BfsDistances(GridCoord origin, int maxDepth)
        {
            var dist = new Dictionary<GridCoord, int> { [origin] = 0 };
            var frontier = new Queue<GridCoord>();
            frontier.Enqueue(origin);
            while (frontier.Count > 0)
            {
                var cell = frontier.Dequeue();
                int d = dist[cell];
                if (d == maxDepth) continue;
                foreach (var n in T.Neighbors(cell))
                    if (!dist.ContainsKey(n))
                    {
                        dist[n] = d + 1;
                        frontier.Enqueue(n);
                    }
            }
            return dist;
        }
    }
}
