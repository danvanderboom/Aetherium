using System.Linq;
using NUnit.Framework;
using Aetherium.Core;
using Aetherium.Geometry;

namespace Aetherium.Test
{
    public class MazeGeneratorClassificationTests
    {
        [Test]
        public void Classifies_Rooms_Walls_And_Pillars_From_Coloring()
        {
            var world = new World();
            var builder = new WorldBuilders.TorusWorldBuilder();
            world.AddTileTypes(builder.TileTypes);
            world.AddTerrainTypes(builder.CreateTerrainTypes(builder.TileTypes));

            // Create a 2x2 patch of locations at z = 0 and make them passable
            var locations = new[]
            {
                new Components.WorldLocation(0, 0, 0),
                new Components.WorldLocation(1, 0, 0),
                new Components.WorldLocation(0, 1, 0),
                new Components.WorldLocation(1, 1, 0)
            };

            foreach (var loc in locations)
                world.SetTerrain("Indoors", loc);

            var grid = new string[,]
            {
                { "White", "Blue" },
                { "Red",   "White" },
            };
            var coloring = new GridColoring<string>(grid);

            MazeLocationType Map(string color) => color switch
            {
                "White" => MazeLocationType.Room,
                "Blue"  => MazeLocationType.Wall,
                _        => MazeLocationType.Pillar
            };

            var gen = new MazeGenerator(
                world,
                locations,
                coloring,
                Map,
                setRoom:  _ => { },
                setPillar: _ => { },
                setWall:  _ => { },
                removeWall: _ => { });

            // One Blue cell may expand via connected cells; with this grid, Blue is alone, so count 1.
            Assert.AreEqual(2, gen.Rooms.Count);   // two Whites
            Assert.AreEqual(1, gen.Walls.Count);   // one Blue cluster
            Assert.AreEqual(1, gen.Pillars.Count); // one Red
        }
    }
}



