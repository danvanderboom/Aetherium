using System;
using Aetherium.Core;
using Aetherium.Components;
using Aetherium.Entities;
using Aetherium.WorldBuilders;

namespace Aetherium.WorldGen.Generators
{
    /// <summary>
    /// A single wide-open hall ringed by a wall border — a diagnostic layout for testing
    /// illumination and perception in the clear, where any pool asymmetry or collapse is
    /// unambiguously a lighting/vision fault rather than occlusion by nearby geometry.
    ///
    /// <para>Optional pillars let you dial occlusion back in on demand:
    /// <c>pillars</c> (integer count, default 0) scatters single-cell wall pillars on a
    /// deterministic jittered grid, kept well clear of the center spawn so a fresh join
    /// always looks out over open floor.</para>
    ///
    /// <para>Shares the <see cref="TestMazeWorldBuilder"/> tile/terrain vocabulary
    /// ("Wall"/"Indoors") and the center light source with
    /// <see cref="RoomsAndCorridorsGenerator"/>, so a client themes an arena world exactly
    /// as it themes the maze — only the floor plan changes.</para>
    /// </summary>
    public class ArenaGenerator : IMapGenerator
    {
        // Pillars stay at least this many cells from the spawn so a joining player never
        // opens on an obstructed view (the whole point of the arena is clear sightlines).
        private const int SpawnClearRadius = 6;

        private readonly WorldBuilder _baseBuilder;

        public ArenaGenerator()
        {
            _baseBuilder = new TestMazeWorldBuilder();
        }

        public World Generate(GeneratorContext context)
        {
            var world = new World();
            if (_baseBuilder is TestMazeWorldBuilder testBuilder)
            {
                var tileTypes = testBuilder.TileTypes;
                world.AddTileTypes(tileTypes);
                world.AddTerrainTypes(testBuilder.CreateTerrainTypes(tileTypes));
            }
            else
            {
                throw new InvalidOperationException("Expected TestMazeWorldBuilder");
            }

            var z = context.ZLevel;

            // Fill with walls, then carve one open rectangle inside a 1-cell border.
            for (int y = 0; y < context.Height; y++)
            {
                for (int x = 0; x < context.Width; x++)
                {
                    world.SetTerrain("Wall", new WorldLocation(x, y, z));
                }
            }
            for (int y = 1; y < context.Height - 1; y++)
            {
                for (int x = 1; x < context.Width - 1; x++)
                {
                    world.SetTerrain("Indoors", new WorldLocation(x, y, z));
                }
            }

            var centerX = context.Width / 2;
            var centerY = context.Height / 2;

            // Optional pillars: a jittered grid of single-cell walls, spawn-clear.
            var pillars = context.GetIntParam("pillars", 0, 0, 4096);
            if (pillars > 0)
            {
                ScatterPillars(world, context, centerX, centerY, pillars, z);
            }

            context.StartLocation = new WorldLocation(centerX, centerY, z);

            // Same center light source as rooms-and-corridors (torch worlds ignore it; sunlight
            // and any ambient-lit world still get a sensible source).
            var lightEntity = new LightEntity();
            lightEntity.Set(new LightSource(1.0, 50));
            lightEntity.Set(context.StartLocation);
            world.AddEntity(lightEntity);

            return world;
        }

        private static void ScatterPillars(World world, GeneratorContext context,
            int centerX, int centerY, int count, int z)
        {
            var rng = context.GetRandom("arena-pillars");

            // Spacing derived from area/count so pillars spread evenly rather than clumping.
            var interior = Math.Max(1, (context.Width - 2) * (context.Height - 2));
            var spacing = Math.Max(3, (int)Math.Sqrt(interior / (double)count));

            for (int gy = spacing; gy < context.Height - 1; gy += spacing)
            {
                for (int gx = spacing; gx < context.Width - 1; gx += spacing)
                {
                    var jx = gx + rng.Next(-1, 2);
                    var jy = gy + rng.Next(-1, 2);
                    if (jx < 1 || jy < 1 || jx >= context.Width - 1 || jy >= context.Height - 1)
                        continue;

                    var dx = jx - centerX;
                    var dy = jy - centerY;
                    if (dx * dx + dy * dy < SpawnClearRadius * SpawnClearRadius)
                        continue;

                    world.SetTerrain("Wall", new WorldLocation(jx, jy, z));
                }
            }
        }
    }
}
