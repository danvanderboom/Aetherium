using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using ConsoleGame;
using ConsoleGame.Core;
using ConsoleGame.Components;
using ConsoleGame.WorldBuilders;

namespace ConsoleGameClient.WorldBuilders.Features
{
    public class TorusFeatureBuilder : WorldFeatureBuilder
    {
        Random rand = new Random();

        public TorusFeatureBuilder(World world, WorldFeature feature) : base(world, feature)
        {
        }

        public override void Build()
        {
            var borderWidth = 2;
            var size = Feature.Chunk.Size;

            var axis = Enum.Parse<Axis>(Feature.Settings["RadialSymmetryAxis"]);

            var minorRadius = axis switch
            {
                Axis.X => Feature.Chunk.Size.Width / 2 - borderWidth - 1, // x
                Axis.Y => Feature.Chunk.Size.Length / 2 - borderWidth - 1, // y
                Axis.Z => Feature.Chunk.Size.Depth / 2 - borderWidth - 1, // z
                _ => throw new ArgumentException("RadialSymmetryAxis must be 0, 1, or 2")
            };

            var majorRadius = axis switch
            {
                Axis.X => (Math.Min(size.Length, size.Depth) / 2) - minorRadius - borderWidth - 1, // x
                Axis.Y => (Math.Min(size.Width, size.Depth) / 2) - minorRadius - borderWidth - 1, // y
                Axis.Z => (Math.Min(size.Length, size.Width) / 2) - minorRadius - borderWidth - 1, // z
                _ => throw new ArgumentException("radialSymmetryAxis must be 0, 1, or 2")
            };

            var levelCounts = new Dictionary<int, int>();

            foreach (var location in Feature.Chunk.AllLocations)
            {
                if (levelCounts.ContainsKey(location.Z))
                    levelCounts[location.Z]++;
                else
                    levelCounts.Add(location.Z, 1);

                if (location.Z < 0)
                {
                    if (InsideTorus(location, axis, majorRadius, minorRadius))
                        World.SetTerrain("Indoors", location);
                    else if (InsideTorus(location, axis, majorRadius, minorRadius + 2))
                        World.SetTerrain("Mountain", location);
                }
                else if (location.Z == 0)
                {
                    if (InsideTorus(location, axis, majorRadius, minorRadius))
                    {
                        var terrainType = rand.NextDouble();

                        if (terrainType < 0.75)
                            World.SetTerrain("Plains", location);
                        else if (terrainType < 0.90)
                            World.SetTerrain("Forest", location);
                        else
                            World.SetTerrain("Water", location);

                    }
                    else if (InsideTorus(location, axis, majorRadius, minorRadius + borderWidth))
                        World.SetTerrain("Mountain", location);
                }
            }
        }

        bool InsideTorus(WorldLocation loc, Axis axis, int R, int r)
        {
            var x2 = Math.Pow(loc.X, 2);
            var y2 = Math.Pow(loc.Y, 2);
            var z2 = Math.Pow(loc.Z, 2);

            var r2 = Math.Pow(r, 2);

            switch (axis)
            {
                case Axis.X:
                    return r2 > (Math.Pow(Math.Sqrt(y2 + z2) - R, 2) + x2);
                case Axis.Y:
                    return r2 > (Math.Pow(Math.Sqrt(x2 + z2) - R, 2) + y2);
                case Axis.Z:
                    return r2 > (Math.Pow(Math.Sqrt(x2 + y2) - R, 2) + z2);
                default:
                    throw new ArgumentException("Unsupported Axis value");
            }
        }
    }
}
