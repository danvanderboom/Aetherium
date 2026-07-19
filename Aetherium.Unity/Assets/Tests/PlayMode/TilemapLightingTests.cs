using Aetherium.Unity.Model;
using Aetherium.Unity.Rendering;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Aetherium.Unity.Tests
{
    public class TilemapLightingTests
    {
        private static (GameObject grid, TilemapRenderer2D renderer, Tilemap tilemap) NewTilemap()
        {
            var gridGo = new GameObject("Grid");
            gridGo.AddComponent<Grid>();
            var tmGo = new GameObject("Tilemap");
            tmGo.transform.SetParent(gridGo.transform, false);
            var tilemap = tmGo.AddComponent<Tilemap>();
            tmGo.AddComponent<TilemapRenderer>();
            var renderer = tmGo.AddComponent<TilemapRenderer2D>();
            renderer.Configure(tilemap, null);
            return (gridGo, renderer, tilemap);
        }

        private static PerceptionLite FrameWithLight(float light, AmbientTintLite tint = null)
        {
            var perception = new PerceptionLite { AmbientTint = tint };
            perception.Visuals["0,0,0"] = new VisualLite(new WorldLocationLite(0, 0, 0), "grass", light);
            return perception;
        }

        private static Color CellColor(Tilemap tilemap) => tilemap.GetColor(new Vector3Int(0, 0, 0));

        [Test]
        public void LightingOff_LeavesBaseColorUnmodulated()
        {
            var (grid, renderer, tilemap) = NewTilemap();
            renderer.ApplyLighting = false;
            renderer.RenderPerception(FrameWithLight(0.5f), 0);

            // With lighting off no per-cell override is applied, so the cell keeps the
            // terrain's base palette color despite the 0.5 light level (Tilemap.GetColor
            // returns the tile's own color when there is no override).
            var expected = TileTheme.ColorFor("grass");
            var c = CellColor(tilemap);
            Assert.AreEqual(expected.r, c.r, 1e-4f);
            Assert.AreEqual(expected.g, c.g, 1e-4f);
            Assert.AreEqual(expected.b, c.b, 1e-4f);

            Object.DestroyImmediate(grid);
        }

        [Test]
        public void LightingOn_DimCell_DarkensByLightLevel()
        {
            var (grid, renderer, tilemap) = NewTilemap();
            renderer.ApplyLighting = true;
            renderer.RenderPerception(FrameWithLight(0.5f), 0);

            var c = CellColor(tilemap);
            Assert.AreEqual(0.5f, c.r, 1e-3f);
            Assert.AreEqual(0.5f, c.g, 1e-3f);
            Assert.AreEqual(0.5f, c.b, 1e-3f);

            Object.DestroyImmediate(grid);
        }

        [Test]
        public void LightingOn_AmbientTint_ColorsCell()
        {
            var (grid, renderer, tilemap) = NewTilemap();
            renderer.ApplyLighting = true;
            renderer.RenderPerception(FrameWithLight(1.0f, new AmbientTintLite(1f, 0.6f, 0.4f)), 0);

            var c = CellColor(tilemap);
            Assert.AreEqual(1f, c.r, 1e-3f);
            Assert.AreEqual(0.6f, c.g, 1e-3f);
            Assert.AreEqual(0.4f, c.b, 1e-3f);

            Object.DestroyImmediate(grid);
        }
    }
}
