using System;
using System.Collections.Generic;
using System.Linq;
using System.Drawing;
using NUnit.Framework;
using Aetherium.Model;
using Aetherium.Topology;
using Aetherium.WorldBuilders;
using H3.Extensions;
using World = Aetherium.Core.World;
using WorldLocation = Aetherium.Components.WorldLocation;

namespace Aetherium.Test.Perception
{
    /// <summary>
    /// Verifies sphere-native FOV + lighting on H3 (docs/design/h3-sphere-worldgen.md §7 P1): mountains
    /// and walls occlude line of sight, the directional cone restricts perception to the forward arc, and
    /// lighting drives what's visible (a torch lights a pool; darkness collapses the view). The occlusion
    /// and light models are the same tested ObstructsView / topology-ray logic the square grid uses — run
    /// over an H3 gridDisk.
    /// </summary>
    [TestFixture]
    public class H3FovLightingTests
    {
        private static readonly DateTime Noon = new(2026, 1, 1, 12, 0, 0);

        // A controlled H3 world: an open Plains disc around a player cell (so nothing occludes until we
        // place something), using the overworld palette (Plains transparent, Mountain/Wall opaque).
        private static (World World, GridCoord Player) OpenDisc(int radius = 6)
        {
            var palette = new OverworldWorldBuilder();
            var tileTypes = palette.TileTypes;
            var world = new World { Topology = H3Topology.Instance };
            world.AddTileTypes(tileTypes);
            world.AddTerrainTypes(palette.CreateTerrainTypes(tileTypes));

            var player = H3Topology.FromH3((ulong)H3.H3Index.GetRes0Cells().First()
                .GetChildrenForResolution(4).First());
            foreach (var c in H3Topology.Instance.Range(player, radius))
                world.SetTerrain("Plains", c.ToWorldLocation());
            return (world, player);
        }

        private static string Key(GridCoord player, GridCoord cell)
        {
            var rel = H3Topology.Instance.RelativeCoords(player, cell)!.Value;
            return $"{rel.RelX},{rel.RelY},0";
        }

        private static Aetherium.Model.PerceptionDto Perceive(
            World world, GridCoord player, LightingMode mode = LightingMode.Sunlight,
            bool directional = false, int? heading = null, int? fov = null)
            => new Aetherium.Server.PerceptionService().ComputePerception(
                world, player.ToWorldLocation(), Aetherium.WorldDirection.North, new Size(20, 20),
                mode, VisionMode.Normal, null, Noon, directional, heading, fov);

        [Test]
        public void AWallOfMountainsBlocksEverythingBeyondIt()
        {
            // A single mountain cell doesn't fully occlude on a hex/H3 grid — sight goes around its edge
            // via the adjacent path, exactly as the square FovCalculator peeks past a single cell. To
            // block line of sight you need a wall. Ring the player with opaque mountains: every sightline
            // out now passes through one, so nothing beyond the ring is visible — only the player and the
            // ring of blockers they can see.
            var (world, player) = OpenDisc();
            var topo = H3Topology.Instance;

            var ring = topo.Neighbors(player).ToList();
            foreach (var n in ring)
                world.SetTerrain("Mountain", n.ToWorldLocation());

            var p = Perceive(world, player);

            foreach (var n in ring)
                Assert.That(p.Visuals.ContainsKey(Key(player, n)), Is.True, "you can see the ring of blockers");
            Assert.That(p.Visuals.Count, Is.EqualTo(1 + ring.Count),
                "walled in, you see only your own cell and the encircling mountains — nothing beyond");
        }

        [Test]
        public void ASingleObstacleDoesNotBlindYouToTheWholeField()
        {
            // The complement: one mountain removes at most a sliver, not the field. (Documents the
            // hex peek-around so the wall test above isn't mistaken for per-cell shadowing.)
            var (world, player) = OpenDisc();
            var open = Perceive(world, player).Visuals.Count;

            world.SetTerrain("Mountain", H3Topology.Instance.Neighbors(player).First().ToWorldLocation());
            var withOne = Perceive(world, player).Visuals.Count;

            Assert.That(withOne, Is.LessThanOrEqualTo(open), "occlusion only ever removes cells");
            Assert.That(withOne, Is.GreaterThan(open / 2),
                "one mountain casts a shadow wedge but leaves most of the field — you're not blinded");
        }

        [Test]
        public void AnOpenPlanetHasNoOcclusion()
        {
            var (world, player) = OpenDisc();
            var p = Perceive(world, player);
            // Size(20,20) => radius 10; an all-Plains disc should show a large, unoccluded field.
            Assert.That(p.Visuals.Count, Is.GreaterThan(150), "an open daylight disc should be fully visible");
        }

        [Test]
        public void TheDirectionalConeHidesWhatIsBehindYou()
        {
            var (world, player) = OpenDisc();
            var topo = H3Topology.Instance;

            // Cells nearly due north (in front, heading 0) and nearly due south (behind).
            GridCoord? north = null, south = null;
            foreach (var c in topo.Range(player, 4))
            {
                if (c == player) continue;
                var (dx, dy) = topo.Delta(player, c);
                double dist = Math.Sqrt(dx * dx + dy * dy);
                if (dist < 2) continue;
                if (north is null && dy < -0.9 * dist) north = c; // north = -Y
                if (south is null && dy > 0.9 * dist) south = c;
            }
            Assert.That(north, Is.Not.Null);
            Assert.That(south, Is.Not.Null);

            var p = Perceive(world, player, directional: true, heading: 0, fov: 90);

            Assert.That(p.IsDirectionalVision, Is.True);
            Assert.That(p.Visuals.ContainsKey(Key(player, north!.Value)), Is.True, "a cell ahead is in the cone");
            Assert.That(p.Visuals.ContainsKey(Key(player, south!.Value)), Is.False, "a cell behind is outside the cone");
        }

        [Test]
        public void DarknessCollapsesTheViewAndDaylightOpensIt()
        {
            var (world, player) = OpenDisc();

            var day = Perceive(world, player, LightingMode.Sunlight);
            var dark = Perceive(world, player, LightingMode.Ambient); // no ambient sun, no light sources

            Assert.That(day.Visuals.Count, Is.GreaterThan(150), "noon daylight opens the whole disc");
            Assert.That(dark.Visuals.Count, Is.LessThan(day.Visuals.Count / 4),
                "pitch dark with no light source collapses sight to arm's reach");
        }

        [Test]
        public void ATorchLightsAPoolAroundThePlayer()
        {
            var (world, player) = OpenDisc();

            var torch = Perceive(world, player, LightingMode.Torch);

            // The player's torch (intensity 0.9, range 6) lights a pool; more than the 1-cell darkness
            // fallback, but bounded by the torch range — not the full daylight disc.
            var day = Perceive(world, player, LightingMode.Sunlight);
            Assert.That(torch.Visuals.Count, Is.GreaterThan(20), "the torch lights a real pool");
            Assert.That(torch.Visuals.Count, Is.LessThan(day.Visuals.Count),
                "the torch pool is smaller than the fully daylit disc");
            Assert.That(torch.Visuals["0,0,0"].LightLevel, Is.GreaterThan(0.5), "you stand in your own light");
        }
    }
}
