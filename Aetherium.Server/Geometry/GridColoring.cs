using System;
using System.Linq;
using System.Collections.Generic;

namespace Aetherium.Geometry
{
    public class GridColoring<T>
        where T : class
    {
        public T[,] Grid { get; set; }

        public int GridLength => Grid.GetLength(0);
        public int GridWidth => Grid.GetLength(1);

        public int ColorCount => GridLength * GridWidth;

        public T GetColor(int x, int y) => Grid[
            Math.Abs(y % GridLength), 
            Math.Abs(x % GridWidth)];

        public IEnumerable<T> GridValues()
        {
            for (int y = 0; y < GridLength; y++)
                for (int x = 0; x < GridWidth; x++)
                    yield return Grid[y, x];

            yield break;
        }

        public GridColoring(T[,] grid)
        {
            Grid = grid;
        }

        /// <summary>
        /// Given world coordinates, return a set of coordinates connected by the same color. 
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <returns></returns>
        public IList<(int X, int Y)> GetConnectedCells(int rx, int ry, 
            T? color = null, 
            IList<(int X, int Y)>? visited = null)
        {
            color = color ?? GetColor(rx, ry);

            visited = visited ?? new List<(int X, int Y)>();
            if (!visited.Contains((rx, ry)))
                visited.Add((rx, ry));

            var results = new List<(int X, int Y)>();
            if (GetColor(rx, ry) != color)
                return results;

            results.Add((rx, ry));

            foreach (var neighbor in GetNeighbors(rx, ry))
                if (!visited.Contains(neighbor) && GetColor(neighbor.X, neighbor.Y) == GetColor(rx, ry))
                    results.AddRange(GetConnectedCells(neighbor.X, neighbor.Y, color, visited));

            return results;
        }

        public IList<(int X, int Y)> GetNeighbors(int x, int y) => new List<(int X, int Y)>
        {
            (x - 1, y),
            (x + 1, y),
            (x, y - 1),
            (x, y + 1)
        };
    }
}
