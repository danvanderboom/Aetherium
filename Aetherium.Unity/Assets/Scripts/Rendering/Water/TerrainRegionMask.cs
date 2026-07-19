#nullable enable
using System;
using System.Collections.Generic;
using Aetherium.Unity.Model;

namespace Aetherium.Unity.Rendering.Water
{
    /// <summary>
    /// Boolean occupancy of "region" terrain cells (e.g. water) on a single Z band,
    /// built from <see cref="PerceptionLite.Visuals"/>. The mesh pipeline traces this
    /// mask's boundary; the tile renderer skips its cells. Pure and scene-free.
    /// </summary>
    public sealed class TerrainRegionMask
    {
        private readonly HashSet<(int x, int y)> _cells;

        public int Z { get; }
        public int Count => _cells.Count;
        public bool IsEmpty => _cells.Count == 0;
        public IReadOnlyCollection<(int x, int y)> Cells => _cells;

        // Inclusive cell bounds; meaningful only when !IsEmpty.
        public int MinX { get; }
        public int MinY { get; }
        public int MaxX { get; }
        public int MaxY { get; }

        private TerrainRegionMask(int z, HashSet<(int, int)> cells)
        {
            Z = z;
            _cells = cells;

            bool first = true;
            foreach (var (x, y) in cells)
            {
                if (first)
                {
                    MinX = MaxX = x;
                    MinY = MaxY = y;
                    first = false;
                    continue;
                }

                if (x < MinX) MinX = x;
                if (x > MaxX) MaxX = x;
                if (y < MinY) MinY = y;
                if (y > MaxY) MaxY = y;
            }
        }

        public bool Contains(int x, int y) => _cells.Contains((x, y));

        /// <summary>
        /// Builds the mask for band <paramref name="z"/>. <paramref name="isRegion"/>
        /// tests a resolved terrain name; when null, <see cref="RegionTerrains.IsRegion"/>
        /// is used.
        /// </summary>
        public static TerrainRegionMask Build(PerceptionLite? perception, int z, Func<string, bool>? isRegion = null)
        {
            var cells = new HashSet<(int, int)>();
            if (perception?.Visuals != null)
            {
                foreach (var visual in perception.Visuals.Values)
                {
                    if (visual?.Location == null || visual.Location.Z != z)
                        continue;

                    string name = RegionTerrains.ResolveName(perception, visual.TileTypeId);
                    bool region = isRegion != null ? isRegion(name) : RegionTerrains.IsRegion(name);
                    if (region)
                        cells.Add((visual.Location.X, visual.Location.Y));
                }
            }

            return new TerrainRegionMask(z, cells);
        }
    }
}
