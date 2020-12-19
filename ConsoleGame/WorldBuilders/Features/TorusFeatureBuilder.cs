using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using ConsoleGame.Core;
using ConsoleGame.Components;
using ConsoleGame.WorldBuilders;
using ConsoleGame;

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

            var axis = Enum.Parse<Axis>(Feature.Settings["RadialSymmetryAxis"]);

            //var coordinates = Feature.Chunk.Location.ToList();

            //var minAxisValue = coordinates.Min();
            //var minAxisIndex = coordinates.IndexOf(minAxisValue);

            //var maxAxisValue = coordinates.Max();
            //var maxAxisIndex = coordinates.IndexOf(maxAxisValue);

            //var axes = Enumerable.Range(0, 3).ToList();
            //axes.Remove(radialSymmetryAxis);
            //axes.Remove(minAxisIndex);
            //axes.Remove(maxAxisIndex);
            //var midAxisIndex = axes.First();
            //var midAxisValue = coordinates[midAxisIndex];

            var size = Feature.Chunk.Size;

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


            //var startCoordinates = new int[3]; // x, y, z

            //startCoordinates[radialSymmetryAxis] = -minorRadius;

            //var otherAxes = new int[3] { 0, 1, 2 }.Where(a => a != radialSymmetryAxis).ToList();
            //foreach (var axis in otherAxes)
            //    startCoordinates[axis] = -(majorRadius * 2) - (minorRadius * 2);


            //var torusChunk = new WorldChunk(
            //    //new WorldLocation(
            //    //    x: -minorRadius - padding,
            //    //    z: -(majorRadius * 2) - minorRadius + 2),
            //    WorldLocation.FromCoordinates(startCoordinates),
            //    //new Size3d(
            //    //    length: (majorRadius + (minorRadius / 2)) * 2 + (padding * 2),
            //    //    width: minorRadius * 2 + (padding * 2),
            //    //    depth: (majorRadius + (minorRadius / 2)) * 2)
            //    new Size3d(0, 0, 0)
            //);

            var levelCounts = new Dictionary<int, int>();

            //foreach (var location in torusChunk.AllLocations)
            foreach (var location in Feature.Chunk.AllLocations)
            {
                if (levelCounts.ContainsKey(location.Z))
                    levelCounts[location.Z]++;
                else
                    levelCounts.Add(location.Z, 1);

                if (location.Z < 0)
                {
                    if (InsideTorus(location, axis, majorRadius, minorRadius))
                        SetTerrain("Indoors", location);
                    else if (InsideTorus(location, axis, majorRadius, minorRadius + 2))
                        SetTerrain("Mountain", location);
                }
                else if (location.Z == 0)
                {
                    if (InsideTorus(location, axis, majorRadius, minorRadius))
                    {
                        var terrainType = rand.NextDouble();

                        if (terrainType < 0.75)
                            SetTerrain("Plains", location);
                        else if (terrainType < 0.90)
                            SetTerrain("Forest", location);
                        else
                            SetTerrain("Water", location);

                    }
                    else if (InsideTorus(location, axis, majorRadius, minorRadius + borderWidth))
                        SetTerrain("Mountain", location);
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
