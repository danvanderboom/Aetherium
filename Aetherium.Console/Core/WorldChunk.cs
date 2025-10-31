using System;
using System.Linq;
using System.Collections.Generic;
using Aetherium.Components;

namespace Aetherium.Core
{
    public struct WorldChunk
    {
        public WorldLocation Location { get; set; }

        public Size3d Size { get; set; }

        public int LocationCount => Size.Depth * Size.Length * Size.Width;

        public static WorldChunk Nowhere { get; set; } = new WorldChunk();

        public WorldChunk(WorldLocation location, Size3d size) 
        {
            Location = location;
            Size = size;
        }

        public WorldChunk(bool nowhere = true)
        {
            Location = WorldLocation.None;
            Size = Size3d.Empty;
        }

        public IEnumerable<WorldLocation> AllLocations
        {
            get
            {
                for (int z = Location.Z; z < Location.Z + Size.Depth; z++)
                    for (int y = Location.Y; y < Location.Y + Size.Length; y++)
                        for (int x = Location.X; x < Location.X + Size.Width; x++)
                            yield return new WorldLocation(x, y, z);

                yield break;
            }
        }

        public override string ToString() => $"X: {Location.X}, Y: {Location.Y}, Z: {Location.Z}, Length: {Size.Length}, Width: {Size.Width}, Depth: {Size.Depth}";
    }
}

