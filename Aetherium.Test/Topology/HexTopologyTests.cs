using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using NUnit.Framework;
using Aetherium;
using Aetherium.Core;
using Aetherium.Components;
using Aetherium.Systems;
using Aetherium.Topology;
using ModelRel = Aetherium.Model.RelativeDirection;

namespace Aetherium.Test.Topology
{
    /// <summary>
    /// Hexagon tiling (docs/grid-topologies.md P1). Covers the 8-invariant harness, Red Blob
    /// axial/cube reference values, the relative-move/facing semantics, and — the real proof —
    /// an end-to-end run of the unchanged engine (World.TryMoveSteps, FovCalculator) over a
    /// world whose Topology is hex.
    /// </summary>
    [TestFixture]
    public class HexTopologyTests
    {
        private static readonly HexTopology H = HexTopology.Instance;

        [Test]
        public void Hex_SatisfiesAllInvariants() => GridTopologyInvariants.AssertAll(H);

        [Test]
        public void Neighbors_Are_The_Six_Axial_Steps()
        {
            var origin = new GridCoord(0, 0, 0);
            var expected = new HashSet<GridCoord>
            {
                new(+1, -1, 0), new(+1, 0, 0), new(0, +1, 0),
                new(-1, +1, 0), new(-1, 0, 0), new(0, -1, 0),
            };
            Assert.That(H.Neighbors(origin).ToHashSet(), Is.EquivalentTo(expected));
        }

        [Test]
        public void Distance_Is_Cube_Distance()
        {
            var o = new GridCoord(0, 0, 0);
            Assert.That(H.Distance(o, new GridCoord(1, -1, 0)), Is.EqualTo(1)); // neighbor
            Assert.That(H.Distance(o, new GridCoord(2, 0, 0)), Is.EqualTo(2));
            Assert.That(H.Distance(o, new GridCoord(2, -1, 0)), Is.EqualTo(2));
            Assert.That(H.Distance(o, new GridCoord(-3, 1, 0)), Is.EqualTo(3));
        }

        [Test]
        public void Range_Disc_Has_3r2_Plus_3r_Plus_1_Cells([Range(0, 4)] int r)
        {
            var count = H.Range(new GridCoord(0, 0, 0), r).Count();
            Assert.That(count, Is.EqualTo(3 * r * r + 3 * r + 1));
        }

        [Test]
        public void TurnStep_Is_60() => Assert.That(H.TurnStepDegrees(new GridCoord(0, 0, 0)), Is.EqualTo(60));

        // Facing east (heading 90, which is a hex edge): Forward/Backward are exact along
        // east–west; Left/Right resolve to the +/-60-degree forward-side edges (design Rule 2).
        [Test]
        public void ResolveRelative_FacingEast_ResolvesToForwardSideEdges()
        {
            var cell = new GridCoord(5, 5, 0);
            const int east = 90;

            var fwd = H.ResolveRelative(cell, east, ModelRel.Forward);
            Assert.That(fwd.Step.Target, Is.EqualTo(new GridCoord(6, 5, 0)));   // (+1, 0)

            var back = H.ResolveRelative(cell, east, ModelRel.Backward);
            Assert.That(back.Step.Target, Is.EqualTo(new GridCoord(4, 5, 0)));  // (-1, 0)

            var right = H.ResolveRelative(cell, east, ModelRel.Right);
            Assert.That(right.Step.Target, Is.EqualTo(new GridCoord(5, 6, 0)),  // (0, +1), heading 150
                "Right of east should take the +60-degree (south-east) edge.");
            Assert.That(right.NewHeadingDegrees, Is.EqualTo(150));

            var left = H.ResolveRelative(cell, east, ModelRel.Left);
            Assert.That(left.Step.Target, Is.EqualTo(new GridCoord(6, 4, 0)),   // (+1, -1), heading 30
                "Left of east should take the -60-degree (north-east) edge.");
            Assert.That(left.NewHeadingDegrees, Is.EqualTo(30));
        }

        [Test]
        public void SnapHeading_SnapsToNearestEdge()
        {
            var cell = new GridCoord(0, 0, 0);
            Assert.That(H.SnapHeading(cell, 0), Is.EqualTo(30).Or.EqualTo(330));  // equidistant; tie is legal
            Assert.That(H.SnapHeading(cell, 88), Is.EqualTo(90));
            Assert.That(H.SnapHeading(cell, 200), Is.EqualTo(210));
        }

        [Test]
        public void Line_Is_Connected_And_Hits_Both_Endpoints()
        {
            var a = new GridCoord(0, 0, 0);
            var b = new GridCoord(3, -1, 0);
            var line = H.Line(a, b).ToList();
            Assert.That(line.First(), Is.EqualTo(a));
            Assert.That(line.Last(), Is.EqualTo(b));
            Assert.That(line.Count, Is.EqualTo(H.Distance(a, b) + 1));
            for (int i = 1; i < line.Count; i++)
                Assert.That(H.Distance(line[i - 1], line[i]), Is.EqualTo(1), "hex line steps to true neighbors");
        }

        // ---- end-to-end: the real engine, unchanged, over a hex world ----

        private static World BuildHexDiscWorld(int radius)
        {
            var world = new World();
            var builder = new WorldBuilders.TorusWorldBuilder();
            world.AddTileTypes(builder.TileTypes);
            world.AddTerrainTypes(builder.CreateTerrainTypes(builder.TileTypes));
            world.Topology = HexTopology.Instance;

            // Fill a hex disc with passable floor; cells outside stay empty (impassable void).
            foreach (var cell in HexTopology.Instance.Range(new GridCoord(0, 0, 0), radius))
                world.SetTerrain("Indoors", new WorldLocation(cell.X, cell.Y, 0));

            return world;
        }

        [Test]
        public void Engine_TryMoveSteps_Walks_Hex_Edges()
        {
            var world = BuildHexDiscWorld(radius: 4);
            var character = new Character();
            character.Set(new WorldLocation(0, 0, 0));
            character.Set(new HasHeading { Heading = 90 }); // facing east
            world.AddEntity(character);

            // Forward (east) → the (+1, 0) hex neighbor.
            var fwd = world.TryMoveSteps(character, 90, ModelRel.Forward, 1);
            Assert.That(fwd.Success, Is.True);
            Assert.That(character.Get<WorldLocation>(), Is.EqualTo(new WorldLocation(1, 0, 0)));

            // Right of east → the (0, +1) hex neighbor — a diagonal that no single square
            // cardinal move could reach. This is the seam doing real hex work in the live path.
            var right = world.TryMoveSteps(character, 90, ModelRel.Right, 1);
            Assert.That(right.Success, Is.True);
            Assert.That(character.Get<WorldLocation>(), Is.EqualTo(new WorldLocation(1, 1, 0)));
        }

        [Test]
        public void Engine_TryMoveSteps_Stops_At_The_Disc_Edge()
        {
            var world = BuildHexDiscWorld(radius: 2);
            var character = new Character();
            character.Set(new WorldLocation(0, 0, 0));
            character.Set(new HasHeading { Heading = 90 });
            world.AddEntity(character);

            // Ask for 5 forward steps on a radius-2 disc: it walks (1,0), (2,0), then the next
            // east cell (3,0) is outside the disc → stops with 2 steps taken.
            var outcome = world.TryMoveSteps(character, 90, ModelRel.Forward, 5);
            Assert.That(outcome.StepsTaken, Is.EqualTo(2));
            Assert.That(character.Get<WorldLocation>(), Is.EqualTo(new WorldLocation(2, 0, 0)));
        }

        [Test]
        public void Engine_FovCalculator_Runs_On_Hex_World()
        {
            var world = BuildHexDiscWorld(radius: 5);
            var origin = new WorldLocation(0, 0, 0);
            var bounds = new Rectangle(-6, -6, 13, 13);

            var visible = new FovCalculator().ComputeVisible(world, origin, bounds, maxRange: 3);

            // Origin is visible to itself.
            Assert.That(visible[origin.Y - bounds.Y, origin.X - bounds.X], Is.True);

            // A hex neighbor within range is visible...
            var near = new WorldLocation(1, 0, 0);
            Assert.That(visible[near.Y - bounds.Y, near.X - bounds.X], Is.True);

            // ...and a cell well beyond maxRange (hex-distance 5) is not.
            var far = new WorldLocation(5, 0, 0);
            Assert.That(visible[far.Y - bounds.Y, far.X - bounds.X], Is.False);
        }
    }
}
