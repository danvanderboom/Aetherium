extern alias Console;
using System.Drawing;
using System.Linq;
using NUnit.Framework;
using Aetherium.Core;
using Aetherium.Components;
using Aetherium.Server;
using Aetherium;
using ClientConsoleMapView = Console::Aetherium.Views.ClientConsoleMapView;

namespace Aetherium.Test
{
    /// <summary>
    /// End-to-end: the 3D perception slab (Section 1, server) feeding the console depth composite (Section 2,
    /// client). A bird two bands overhead, in a column the player can see, is computed into the PerceptionDto by
    /// the server and rendered as an off-focus silhouette by the console view — the motivating "see the bird
    /// overhead" scenario, exercised across the real PerceptionService → ClientConsoleMapView boundary.
    /// </summary>
    public class DepthIntegrationTests
    {
        [Test]
        public void BirdOverhead_FlowsThroughPerception_ToConsoleSilhouette()
        {
            var world = new World();
            var builder = new WorldBuilders.TorusWorldBuilder();
            world.AddTileTypes(builder.TileTypes);
            world.AddTerrainTypes(builder.CreateTerrainTypes(builder.TileTypes));
            world.SlabDepthAbove = 4;
            world.SlabDepthBelow = 4;

            var playerLoc = new WorldLocation(20, 20, 0);
            var player = new Character();
            player.Set(playerLoc);
            player.Set(new Inventory()); // real players always carry one; PerceptionService reads it unguarded
            world.AddEntity(player);

            // A bird one cell east and two bands up — visible up an open column, not directly over the player glyph.
            var bird = new Character();
            bird.Set(new WorldLocation(21, 20, 2));
            world.AddEntity(bird);

            var dto = new PerceptionService()
                .ComputePerception(world, playerLoc, WorldDirection.North, new Size(42, 22));

            // Server side: the bird's cell is in perception, tagged with its relative Z.
            Assert.IsTrue(dto.Visuals.ContainsKey("1,0,2"), "The overhead bird is present in perception at relative (1,0,+2).");

            // Client side: the console composite renders it as an off-focus silhouette.
            var view = new ClientConsoleMapView(new Point(0, 0), new Size(42, 22), hasFrame: false)
            {
                Perception = dto,
                WorldLocation = dto.PlayerLocation
            };
            var map = view.CaptureRenderedFrame();

            var glyphs = map.Tiles.SelectMany(row => row).ToList();
            Assert.Contains("^^", glyphs, "The console renders the overhead bird as a depth silhouette.");
        }
    }
}
