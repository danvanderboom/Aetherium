using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Aetherium.Topology;
using Aetherium.WorldGen;
using Aetherium.WorldGen.Generators.Outdoor;
using H3.Extensions;
using WorldLocation = Aetherium.Components.WorldLocation;

namespace Aetherium.Test.Perception
{
    /// <summary>
    /// Verifies perceiver-anchored relative coordinates on the sphere (docs/design/h3-sphere-worldgen.md
    /// §7 P0): H3 returns local i/j (via cellToLocalIj) instead of a meaningless packed-index difference,
    /// the perceiver is the origin, and the mapping is injective over a viewport disk — so no two visible
    /// cells collide onto the same perception key.
    /// </summary>
    [TestFixture]
    public class H3RelativeCoordsTests
    {
        private static GridCoord SampleCell(int resolution = 4)
            => H3Topology.FromH3((ulong)H3.H3Index.GetRes0Cells().First()
                .GetChildrenForResolution(resolution).First());

        [Test]
        public void PerceiverMapsToTheOrigin()
        {
            var cell = SampleCell();
            Assert.That(H3Topology.Instance.RelativeCoords(cell, cell), Is.EqualTo((0, 0)));
        }

        [Test]
        public void NeighboursAreDistinctSmallOffsets()
        {
            var cell = SampleCell();
            var offsets = H3Topology.Instance.Neighbors(cell)
                .Select(n => H3Topology.Instance.RelativeCoords(cell, n))
                .ToList();

            Assert.That(offsets, Has.None.Null, "a neighbour always has a stable local coordinate");
            Assert.That(offsets.Distinct().Count(), Is.EqualTo(offsets.Count), "neighbours must not collide");
            foreach (var o in offsets)
                Assert.That(Math.Max(Math.Abs(o!.Value.RelX), Math.Abs(o.Value.RelY)), Is.LessThanOrEqualTo(2));
        }

        [Test]
        public void IsInjectiveOverAViewportDisk()
        {
            // The crucial contract: distinct cells within a viewport disk get distinct relative keys, so
            // the perception dictionary never silently drops a cell to a collision.
            var origin = SampleCell();
            var disk = H3Topology.Instance.Range(origin, 12).ToList();

            var keys = disk
                .Select(c => H3Topology.Instance.RelativeCoords(origin, c))
                .Where(r => r is not null)
                .ToList();

            Assert.That(keys.Count, Is.GreaterThan(disk.Count - 5), "at most a few pentagon-edge omissions");
            Assert.That(keys.Distinct().Count(), Is.EqualTo(keys.Count), "no two disk cells may share a relative key");
        }

        [Test]
        public void PlanarTopologiesKeepRawDifference()
        {
            // The default (square/hex/tri) is the raw lattice difference — unchanged behaviour. It's a
            // default interface method, so it's reached through IGridTopology, exactly as the perception
            // service reaches it via world.Topology.
            var origin = new GridCoord(10, 20, 0);
            var cell = new GridCoord(13, 18, 0);
            Assert.That(((IGridTopology)SquareTopology.Instance).RelativeCoords(origin, cell), Is.EqualTo((3, -2)));
            Assert.That(((IGridTopology)HexTopology.Instance).RelativeCoords(origin, cell), Is.EqualTo((3, -2)));
        }
    }

    /// <summary>
    /// Verifies that a player can perceive an H3 planet — the gate to walking it. The frame enumerates a
    /// gridDisk around the perceiver, keys every visible cell by its relative offset (player at 0,0,0),
    /// and never leaks absolute coordinates.
    /// </summary>
    [TestFixture]
    public class H3PerceptionFrameTests
    {
        private Aetherium.Core.World _world = null!;
        private WorldLocation _start = null!;

        [SetUp]
        public void SetUp()
        {
            var ctx = new GeneratorContext(256, 256, 20260718)
            {
                GeneratorParams = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["h3Resolution"] = "2"
                }
            };
            _world = new H3TerrainGenerator().Generate(ctx);
            _start = ctx.StartLocation!;
        }

        private Aetherium.Model.PerceptionDto Compute() => ComputeAt(_start);

        private Aetherium.Model.PerceptionDto ComputeAt(WorldLocation loc)
            => new Aetherium.Server.PerceptionService().ComputePerception(
                _world, loc, Aetherium.WorldDirection.North, new System.Drawing.Size(20, 20), null);

        [Test]
        public void FrameIsAnH3FrameCentredOnThePlayer()
        {
            var p = Compute();
            Assert.That(p.Topology, Is.EqualTo("h3"));
            Assert.That((p.PlayerLocation.X, p.PlayerLocation.Y, p.PlayerLocation.Z), Is.EqualTo((0, 0, 0)),
                "the player is always the relative origin — clients never learn absolute coordinates");
        }

        [Test]
        public void PlayerCellAndADiskOfNeighboursAreVisible()
        {
            var p = Compute();
            Assert.That(p.Visuals, Is.Not.Empty);
            Assert.That(p.Visuals.ContainsKey("0,0,0"), Is.True, "the perceiver's own cell must be in view");
            // Size(20,20) => radius 10 => a gridDisk of ~331 cells. Real terrain occludes part of it
            // (mountains/forests cast shadows — see H3FovLightingTests), so this asserts a substantial
            // lit disc remains visible rather than the full count.
            Assert.That(p.Visuals.Count, Is.GreaterThan(100), "expected a substantial view disk, got " + p.Visuals.Count);
        }

        [Test]
        public void EveryVisibleCellCarriesTerrain()
        {
            var p = Compute();
            foreach (var kv in p.Visuals)
                Assert.That(kv.Value.TileTypeId, Is.Not.Null, $"visible cell {kv.Key} has no terrain");
        }

        [Test]
        public void FrameRecentresWhenThePlayerWalks()
        {
            // Walking = stepping to an adjacent cell and re-perceiving. The new cell becomes the origin,
            // and the cell just left is still in view at a nonzero offset — the moving-window a client
            // scrolls as the player crosses the planet.
            var startCell = GridCoord.From(_start);
            var neighbour = H3Topology.Instance.Neighbors(startCell).First().ToWorldLocation();

            var afterStep = ComputeAt(neighbour);
            Assert.That(afterStep.Visuals.ContainsKey("0,0,0"), Is.True, "the player's new cell is the new origin");

            var rel = H3Topology.Instance.RelativeCoords(GridCoord.From(neighbour), startCell);
            Assert.That(rel, Is.Not.Null);
            Assert.That(rel!.Value, Is.Not.EqualTo((0, 0)), "the cell just left is no longer the origin");
            Assert.That(afterStep.Visuals.ContainsKey($"{rel.Value.RelX},{rel.Value.RelY},0"), Is.True,
                "the cell the player stepped off of is still visible, now at a relative offset");
        }

        [Test]
        public void VisibleCellsAreWithinTheViewRadius()
        {
            // Relative keys must be bounded — a client places them on a local grid, so a stray huge
            // offset would mean a mis-anchored cell.
            var p = Compute();
            foreach (var key in p.Visuals.Keys)
            {
                var parts = key.Split(',').Select(int.Parse).ToArray();
                Assert.That(Math.Max(Math.Abs(parts[0]), Math.Abs(parts[1])), Is.LessThanOrEqualTo(40),
                    $"relative key {key} is outside any sane view radius");
            }
        }
    }
}
