using Aetherium.Unity.Model;
using Aetherium.Unity.Rendering;
using Aetherium.Unity.Rendering.Water;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Aetherium.Unity.Tests
{
    public class WaterRegionRenderingTests
    {
        private static PerceptionLite Frame(params (int x, int y, int z, string terrain)[] cells)
        {
            var perception = new PerceptionLite();
            foreach (var (x, y, z, terrain) in cells)
                perception.Visuals[$"{x},{y},{z}"] = new VisualLite(new WorldLocationLite(x, y, z), terrain, 1.0);
            return perception;
        }

        private static PerceptionLite Lake(int z)
        {
            var perception = new PerceptionLite();
            for (int x = 0; x < 3; x++)
                for (int y = 0; y < 3; y++)
                    perception.Visuals[$"{x},{y},{z}"] =
                        new VisualLite(new WorldLocationLite(x, y, z), "water", 1.0);
            return perception;
        }

        private static WaterRegionRenderer NewRenderer()
            => new GameObject("WaterRegionRenderer").AddComponent<WaterRegionRenderer>();

        [Test]
        public void Lake_BuildsNonEmptyMeshForWaterBand()
        {
            var renderer = NewRenderer();
            renderer.RenderPerception(Lake(0), 0);

            var mesh = renderer.GetBandMesh(0);
            Assert.IsNotNull(mesh);
            Assert.Greater(mesh!.vertexCount, 0);

            Object.DestroyImmediate(renderer.gameObject);
        }

        [Test]
        public void MultiBand_FocusOpaque_OffFocusFaded_SortedByAltitude()
        {
            var frame = new PerceptionLite();
            foreach (var (x, y) in new[] { (0, 0), (1, 0), (0, 1), (1, 1) })
            {
                frame.Visuals[$"{x},{y},0"] = new VisualLite(new WorldLocationLite(x, y, 0), "water", 1.0);
                frame.Visuals[$"{x},{y},1"] = new VisualLite(new WorldLocationLite(x, y, 1), "water", 1.0);
            }

            var renderer = NewRenderer();
            renderer.RenderPerception(frame, 0);

            Assert.IsTrue(renderer.TryGetBandAlpha(0, out var a0));
            Assert.IsTrue(renderer.TryGetBandAlpha(1, out var a1));
            Assert.AreEqual(1f, a0, 1e-4f, "focus band is opaque");
            Assert.Less(a1, a0, "off-focus band fades");

            Assert.IsTrue(renderer.TryGetBandSortingOrder(0, out var s0));
            Assert.IsTrue(renderer.TryGetBandSortingOrder(1, out var s1));
            Assert.Less(s0, s1, "higher altitude sorts above");

            Object.DestroyImmediate(renderer.gameObject);
        }

        [Test]
        public void EmptyingWaterBand_ClearsMesh()
        {
            var renderer = NewRenderer();
            renderer.RenderPerception(Lake(0), 0);
            Assert.Greater(renderer.GetBandMesh(0)!.vertexCount, 0);

            // Next frame has no water on band 0.
            renderer.RenderPerception(Frame((0, 0, 0, "grass")), 0);
            Assert.AreEqual(0, renderer.GetBandMesh(0)!.vertexCount, "emptied band mesh is cleared");

            Object.DestroyImmediate(renderer.gameObject);
        }

        [Test]
        public void TilemapRenderer_SkipsRegionTerrain_NoDoubleDraw()
        {
            var gridGo = new GameObject("Grid");
            gridGo.AddComponent<Grid>();

            var tmGo = new GameObject("Tilemap");
            tmGo.transform.SetParent(gridGo.transform, false);
            var tilemap = tmGo.AddComponent<Tilemap>();
            tmGo.AddComponent<TilemapRenderer>();
            var renderer = tmGo.AddComponent<TilemapRenderer2D>();
            renderer.Configure(tilemap, null);
            renderer.SkipRegionTerrain = true;

            renderer.RenderPerception(Frame((0, 0, 0, "water"), (1, 0, 0, "grass")), 0);

            Assert.AreEqual(1, renderer.GetRenderedTileCount(), "only the grass cell is drawn");
            Assert.IsFalse(renderer.TryGetTileColor(0, 0, out _), "water cell not drawn by the tilemap");
            Assert.IsTrue(renderer.TryGetTileColor(1, 0, out _), "grass cell is drawn");

            Object.DestroyImmediate(gridGo);
        }
    }
}
