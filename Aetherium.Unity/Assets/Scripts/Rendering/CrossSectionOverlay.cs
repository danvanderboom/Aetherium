#nullable enable
using Aetherium.Unity.Model;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Aetherium.Unity.Rendering
{
    /// <summary>
    /// Renders the cross-section (side-on elevation) schematic into its own tilemap — the Unity analogue of
    /// the console's elevation view. It projects <see cref="CrossSectionBuilder"/> rows onto a small tilemap
    /// (column = east-west offset, row = altitude band) and reuses <see cref="TilemapRenderer2D"/>'s theming so
    /// terrain keeps its color and the player cell is the reserved "player" marker color. Toggled on/off by the
    /// depth director when vertical complexity crosses the escalation threshold.
    /// </summary>
    [RequireComponent(typeof(Grid))]
    public class CrossSectionOverlay : MonoBehaviour
    {
        private TilemapRenderer2D? bandRenderer;

        private void Awake() => EnsureRenderer();

        private TilemapRenderer2D EnsureRenderer()
        {
            if (bandRenderer != null)
                return bandRenderer;

            var go = new GameObject("CrossSectionTilemap");
            go.transform.SetParent(transform, false);
            var tilemap = go.AddComponent<Tilemap>();
            go.AddComponent<TilemapRenderer>();
            bandRenderer = go.AddComponent<TilemapRenderer2D>();
            bandRenderer.Configure(tilemap, null);
            return bandRenderer;
        }

        /// <summary>
        /// Projects the elevation slice for <paramref name="perception"/> onto the overlay tilemap. Each
        /// non-empty cell is placed at (dx, band); the player cell uses the reserved "Player" tile type.
        /// </summary>
        public void Render(PerceptionLite perception, int halfWidth)
        {
            var renderer = EnsureRenderer();
            var rows = CrossSectionBuilder.Build(perception, halfWidth);

            // Convert the schematic into a synthetic single-Z PerceptionLite the tilemap renderer understands:
            // world Y = band (altitude up the screen), world X = east-west offset.
            var synthetic = new PerceptionLite();
            foreach (var row in rows)
            {
                for (int i = 0; i < row.Cells.Length; i++)
                {
                    var cell = row.Cells[i];
                    if (cell.Content == CrossSectionContent.Empty)
                        continue;

                    int dx = i - row.HalfWidth;
                    string tileTypeId = cell.Content == CrossSectionContent.Player ? "Player" : cell.TileTypeId;
                    var loc = new WorldLocationLite(dx, row.Band, 0);
                    synthetic.Visuals[$"{dx},{row.Band},0"] = new VisualLite(loc, tileTypeId, 1.0);
                }
            }

            renderer.RenderPerception(synthetic, 0);
        }

        /// <summary>Shows or hides the overlay (activates/deactivates its tilemap object).</summary>
        public void Show(bool visible)
        {
            EnsureRenderer();
            if (bandRenderer != null)
                bandRenderer.gameObject.SetActive(visible);
        }

        /// <summary>True when the overlay tilemap is active.</summary>
        public bool IsShowing => bandRenderer != null && bandRenderer.gameObject.activeSelf;

        /// <summary>Number of cells currently drawn in the schematic.</summary>
        public int RenderedCellCount => bandRenderer != null ? bandRenderer.GetRenderedTileCount() : 0;

        /// <summary>The inner tilemap renderer (for inspection/tests).</summary>
        public TilemapRenderer2D? Renderer => bandRenderer;
    }
}
