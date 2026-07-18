using System;
using System.Drawing;
using NUnit.Framework;
using Aetherium.Core;
using Aetherium.Components;
using Aetherium.Server;
using Aetherium.Model;
using Aetherium;

namespace Aetherium.Test
{
    /// <summary>
    /// Section 5.4 of add-adaptive-depth-visualization: context tint. The band-context policy maps an altitude
    /// band to a default lighting mode (underground → torch, skyway → sunlight, surface → ambient), and — when
    /// a world opts in — perception applies it from the viewer's band. Opt-in; default off is unchanged.
    /// </summary>
    public class ContextTintTests
    {
        [Test]
        public void BandContext_Underground_IsTorch()
        {
            Assert.AreEqual(LightingMode.Torch, BandContext.SuggestLightingMode(-1));
            Assert.AreEqual(LightingMode.Torch, BandContext.SuggestLightingMode(-4));
        }

        [Test]
        public void BandContext_Skyway_IsSunlight()
        {
            Assert.AreEqual(LightingMode.Sunlight, BandContext.SuggestLightingMode(1));
            Assert.AreEqual(LightingMode.Sunlight, BandContext.SuggestLightingMode(6));
        }

        [Test]
        public void BandContext_Surface_IsAmbient()
        {
            Assert.AreEqual(LightingMode.Ambient, BandContext.SuggestLightingMode(0));
        }

        [Test]
        public void BandContext_HonorsSkyThreshold()
        {
            // With a higher sky threshold, low positive bands stay ambient until the threshold.
            Assert.AreEqual(LightingMode.Ambient, BandContext.SuggestLightingMode(2, skyThreshold: 3));
            Assert.AreEqual(LightingMode.Sunlight, BandContext.SuggestLightingMode(3, skyThreshold: 3));
        }

        private static World MakeWorld()
        {
            var world = new World();
            var builder = new WorldBuilders.TorusWorldBuilder();
            world.AddTileTypes(builder.TileTypes);
            world.AddTerrainTypes(builder.CreateTerrainTypes(builder.TileTypes));
            return world;
        }

        private static PerceptionDto Perceive(World world, WorldLocation at, LightingMode requested) =>
            new PerceptionService().ComputePerception(
                world, at, WorldDirection.North, new Size(42, 22), requested, VisionMode.Normal, null, DateTime.UtcNow);

        [Test]
        public void AutoContextTint_On_DerivesLightingFromBand()
        {
            var world = MakeWorld();
            world.AutoContextTint = true;

            Assert.AreEqual(LightingMode.Torch,
                Perceive(world, new WorldLocation(20, 20, -2), LightingMode.Ambient).CurrentLightingMode,
                "Underground → torch");
            Assert.AreEqual(LightingMode.Sunlight,
                Perceive(world, new WorldLocation(20, 20, 3), LightingMode.Ambient).CurrentLightingMode,
                "Skyway → sunlight");
            Assert.AreEqual(LightingMode.Ambient,
                Perceive(world, new WorldLocation(20, 20, 0), LightingMode.Torch).CurrentLightingMode,
                "Surface → ambient (overrides the requested Torch)");
        }

        [Test]
        public void AutoContextTint_Off_LeavesRequestedModeUnchanged()
        {
            var world = MakeWorld(); // AutoContextTint default off

            Assert.AreEqual(LightingMode.Ambient,
                Perceive(world, new WorldLocation(20, 20, -2), LightingMode.Ambient).CurrentLightingMode,
                "Without opt-in, the caller's requested mode is used unchanged");
        }
    }
}
