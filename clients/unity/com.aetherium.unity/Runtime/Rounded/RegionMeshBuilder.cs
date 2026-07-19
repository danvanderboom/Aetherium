#nullable enable
using System.Collections.Generic;
using UnityEngine;

namespace Aetherium.Unity.Rounded
{
    /// <summary>Engine-free mesh output, so triangulation unit-tests without allocating a <see cref="Mesh"/>.</summary>
    public struct RegionMeshData
    {
        public List<Vector3> Vertices;
        public List<Vector2> GridUv;    // uv0: grid XY, for wave/detail sampling in the shader
        public List<Vector2> ShoreUv;   // uv1: x = signed distance to coastline (world units), + inside
        public List<int> Triangles;

        public bool IsEmpty => Vertices == null || Vertices.Count == 0;
    }

    /// <summary>
    /// Builds a region-surface mesh (water, and later forest/mountain) from a set of grid
    /// cells, laid flat on the <b>XZ ground plane</b> to match <c>GridMapView</c>'s 3D world
    /// (cell (x,y) → world (x·cellSize, 0, −y·cellSize), altitude carried by the parent object).
    ///
    /// <para>Rather than triangulate the smoothed outline (which yields no interior vertices for
    /// the shore gradient), it tessellates a fine sub-grid over the region and bakes a signed
    /// distance to the Chaikin-smoothed coastline (<see cref="RegionField"/>) into each vertex.
    /// The shader draws the smooth curved edge by thresholding that field and a foam band near
    /// zero. Robust for concave lakes and islands (signed distance handles holes). Pure and
    /// scene-free apart from the optional <see cref="Fill"/> helper.</para>
    ///
    /// <para>Ported from the Aetherium.Unity sandbox <c>WaterMeshBuilder</c> (proven there); the
    /// substantive change is emitting XZ-plane vertices for the 3D client instead of the sandbox's
    /// 2D XY tilemap plane. The winding is unchanged — the region shader renders <c>Cull Off</c>.</para>
    /// </summary>
    public static class RegionMeshBuilder
    {
        // A sub-quad is emitted when at least one corner is within this many grid units
        // of the region, so the coastline (signed distance 0) always sits inside a rendered
        // quad while far-outside quads are skipped.
        private const float KeepMargin = 1.0f;

        public static RegionMeshData Build(
            IReadOnlyCollection<(int x, int y)> cells,
            int smoothIterations = 2,
            int subdivisions = 3,
            float cellSize = 1.0f)
        {
            var data = new RegionMeshData
            {
                Vertices = new List<Vector3>(),
                GridUv = new List<Vector2>(),
                ShoreUv = new List<Vector2>(),
                Triangles = new List<int>(),
            };
            if (cells == null || cells.Count == 0)
                return data;
            if (subdivisions < 1)
                subdivisions = 1;

            // Smoothed coastline loops (grid coords) drive the signed-distance field.
            var rawLoops = MarchingSquares.TraceLoops(cells);
            var loops = new List<IReadOnlyList<Vector2>>(rawLoops.Count);
            foreach (var loop in rawLoops)
                loops.Add(ChaikinSmoothing.Smooth(loop, smoothIterations));

            // Cell bbox dilated by one cell, so the rounded rim + foam have room.
            int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;
            foreach (var (x, y) in cells)
            {
                if (x < minX) minX = x;
                if (x > maxX) maxX = x;
                if (y < minY) minY = y;
                if (y > maxY) maxY = y;
            }
            int gx0 = minX - 1, gy0 = minY - 1;
            int gx1 = maxX + 2, gy1 = maxY + 2; // cells occupy [c, c+1]; +1 dilation each side
            int nx = (gx1 - gx0) * subdivisions;
            int ny = (gy1 - gy0) * subdivisions;
            float step = 1f / subdivisions;

            // Signed distance at every sub-grid lattice point.
            int vpx = nx + 1, vpy = ny + 1;
            var sd = new float[vpx, vpy];
            for (int j = 0; j < vpy; j++)
            {
                float gy = gy0 + j * step;
                for (int i = 0; i < vpx; i++)
                {
                    float gx = gx0 + i * step;
                    sd[i, j] = RegionField.SignedDistance(new Vector2(gx, gy), loops);
                }
            }

            var vertexIndex = new Dictionary<(int, int), int>();

            int GetVertex(int i, int j)
            {
                if (vertexIndex.TryGetValue((i, j), out var idx))
                    return idx;

                float gx = gx0 + i * step;
                float gy = gy0 + j * step;
                idx = data.Vertices.Count;
                // XZ ground plane: grid +Y maps to world −Z, exactly as GridMapView.CellToWorld.
                data.Vertices.Add(new Vector3(gx * cellSize, 0f, -gy * cellSize));
                data.GridUv.Add(new Vector2(gx, gy));
                data.ShoreUv.Add(new Vector2(sd[i, j] * cellSize, 0f));
                vertexIndex[(i, j)] = idx;
                return idx;
            }

            for (int j = 0; j < ny; j++)
            {
                for (int i = 0; i < nx; i++)
                {
                    float maxSd = Mathf.Max(
                        Mathf.Max(sd[i, j], sd[i + 1, j]),
                        Mathf.Max(sd[i + 1, j + 1], sd[i, j + 1]));
                    if (maxSd <= -KeepMargin)
                        continue; // entirely far outside the region

                    int v00 = GetVertex(i, j);
                    int v10 = GetVertex(i + 1, j);
                    int v11 = GetVertex(i + 1, j + 1);
                    int v01 = GetVertex(i, j + 1);

                    data.Triangles.Add(v00); data.Triangles.Add(v10); data.Triangles.Add(v11);
                    data.Triangles.Add(v00); data.Triangles.Add(v11); data.Triangles.Add(v01);
                }
            }

            return data;
        }

        /// <summary>Fills (or clears) a <see cref="Mesh"/> from mesh data.</summary>
        public static void Fill(Mesh mesh, RegionMeshData data)
        {
            mesh.Clear();
            if (data.IsEmpty)
                return;

            mesh.indexFormat = data.Vertices.Count > 65000
                ? UnityEngine.Rendering.IndexFormat.UInt32
                : UnityEngine.Rendering.IndexFormat.UInt16;
            mesh.SetVertices(data.Vertices);
            mesh.SetUVs(0, data.GridUv);
            mesh.SetUVs(1, data.ShoreUv);
            mesh.SetTriangles(data.Triangles, 0);
            mesh.RecalculateBounds();
        }
    }
}
