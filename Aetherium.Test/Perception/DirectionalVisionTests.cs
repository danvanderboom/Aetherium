using System;
using System.Linq;
using NUnit.Framework;
using Aetherium.Server;
using Aetherium.WorldBuilders;
using Aetherium.Model;
using World = Aetherium.Core.World;
using WorldLocation = Aetherium.Components.WorldLocation;

namespace Aetherium.Test.Perception
{
    /// <summary>
    /// Proves the central claim about directional vision: turning it on makes the server send a
    /// genuinely smaller perception frame — the forward cone only, with the cells behind the
    /// observer omitted entirely (not hidden client-side, never computed or transmitted). Also
    /// pins the FOV span so a narrower cone yields strictly fewer cells than a wider one.
    ///
    /// Uses Sunlight so every cell is lit — that isolates the *angular* filter from the torch/
    /// darkness range culling, which is a separate mechanism.
    /// </summary>
    [TestFixture]
    public class DirectionalVisionTests
    {
        private const int Size = 40;

        private static World OpenArena()
        {
            // A wide-open floor. Perception uses Torch lighting (a radial lamp on the observer),
            // which lights a disc all around them — including behind — so the only thing that
            // hides the cells behind is the directional cone, not darkness. Indoor tiles get no
            // sunlight, so Torch is the mode that isolates the angular filter cleanly.
            var builder = new TestMazeWorldBuilder();
            var world = new World();
            world.AddTileTypes(builder.TileTypes);
            world.AddTerrainTypes(builder.CreateTerrainTypes(builder.TileTypes));
            for (var y = 0; y < Size; y++)
                for (var x = 0; x < Size; x++)
                    world.SetTerrain("Indoors", new WorldLocation(x, y, 0));
            return world;
        }

        // Frame Visuals keys are world-axis-relative "relX,relY,relZ" (NOT rotated to face-up).
        // Heading North faces -Y, so a cell strictly behind the observer has relY > 0 (south).
        private static (int x, int y) Rel(string key)
        {
            var p = key.Split(',');
            return (int.Parse(p[0]), int.Parse(p[1]));
        }

        private static PerceptionDto Perceive(World world, bool directional, int? fov)
        {
            var center = new WorldLocation(Size / 2, Size / 2, 0);
            return new PerceptionService().ComputePerception(
                world, center, Aetherium.WorldDirection.North, new System.Drawing.Size(60, 40),
                LightingMode.Torch, VisionMode.Normal, heatTracker: null, currentTime: DateTime.UtcNow,
                directionalVision: directional, headingDegrees: directional ? 0 : (int?)null, fovDegrees: fov);
        }

        [Test]
        public void Omnidirectional_SeesBehind_DirectionalDoesNot()
        {
            var world = OpenArena();

            var omni = Perceive(world, directional: false, fov: null);
            var cone = Perceive(world, directional: true, fov: 120);

            var omniBehind = omni.Visuals.Keys.Select(Rel).Count(c => c.y >= 2);
            var coneBehind = cone.Visuals.Keys.Select(Rel).Count(c => c.y >= 2);

            Assert.That(omniBehind, Is.GreaterThan(0),
                "omnidirectional vision must include cells behind the observer (open floor to the south)");
            Assert.That(coneBehind, Is.EqualTo(0),
                "a 120° forward cone (facing north) must send NO cells well behind the observer");
        }

        [Test]
        public void Directional_SendsStrictlyLessData_ThanOmnidirectional()
        {
            var world = OpenArena();

            var omni = Perceive(world, directional: false, fov: null).Visuals.Count;
            var cone = Perceive(world, directional: true, fov: 120).Visuals.Count;

            Assert.That(cone, Is.LessThan(omni),
                "the directional frame must carry fewer visuals than the omnidirectional one — " +
                "less data on the wire is the whole point");
        }

        [Test]
        public void NarrowerFov_SeesFewerCells_ThanWiderFov()
        {
            var world = OpenArena();

            var wide = Perceive(world, directional: true, fov: 200).Visuals.Count;
            var human = Perceive(world, directional: true, fov: 120).Visuals.Count;
            var narrow = Perceive(world, directional: true, fov: 70).Visuals.Count;

            Assert.That(narrow, Is.LessThanOrEqualTo(human));
            Assert.That(human, Is.LessThanOrEqualTo(wide),
                "a wider field of view can never see fewer cells than a narrower one");
            Assert.That(narrow, Is.LessThan(wide),
                "70° must see strictly fewer cells than 200° in the open");
        }

        [Test]
        public void DirectionalFrame_AdvertisesItsConeToTheClient()
        {
            var world = OpenArena();
            var cone = Perceive(world, directional: true, fov: 120);

            Assert.That(cone.IsDirectionalVision, Is.True);
            Assert.That(cone.FieldOfViewDegrees, Is.EqualTo(120),
                "the client needs the cone parameters to render/anchor correctly");
        }
    }
}
