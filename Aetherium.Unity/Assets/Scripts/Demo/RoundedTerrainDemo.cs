#nullable enable
using System.IO;
using Aetherium.Unity.Model;
using Aetherium.Unity.Rendering;
using Aetherium.Unity.Rendering.Water;
using Newtonsoft.Json;
using UnityEngine;

namespace Aetherium.Unity.Demo
{
    /// <summary>
    /// One-component demo. Drop this on a GameObject in an empty scene and press Play to
    /// see rounded, lit, animated water: it builds a Grid + band-stack land renderer +
    /// water-mesh renderer + an orthographic camera, loads a lake frame (or a built-in
    /// one), and renders it — no manual scene wiring. Editor/runtime demo, not the
    /// shipping client path.
    /// </summary>
    public class RoundedTerrainDemo : MonoBehaviour
    {
        [SerializeField] private string frameFileName = "rounded-water-demo.json";
        [SerializeField] private float cameraPadding = 2f;
        [SerializeField] private Color backgroundColor = new Color(0.06f, 0.07f, 0.09f);

        private BandStackRenderer? land;
        private WaterRegionRenderer? water;

        /// <summary>The band-stack land renderer (null until built).</summary>
        public BandStackRenderer? Land => land;

        /// <summary>The water-mesh renderer (null until built).</summary>
        public WaterRegionRenderer? Water => water;

        private void Start()
        {
            if (land == null)
                Build(LoadFrame(frameFileName) ?? BuiltInLake());
        }

        /// <summary>Composes the renderers + camera and draws <paramref name="perception"/>.</summary>
        public void Build(PerceptionLite perception)
        {
            var gridGo = new GameObject("TerrainGrid");
            gridGo.transform.SetParent(transform, false);
            gridGo.AddComponent<Grid>();
            land = gridGo.AddComponent<BandStackRenderer>();
            land.SkipRegionTerrain = true; // water is drawn by the mesh, not tiles
            land.ApplyLighting = true;     // per-cell light level × ambient tint

            var waterGo = new GameObject("Water");
            waterGo.transform.SetParent(transform, false);
            water = waterGo.AddComponent<WaterRegionRenderer>();

            int focusZ = perception.PlayerLocation?.Z ?? 0;
            land.RenderPerception(perception, focusZ);
            water.RenderPerception(perception, focusZ);

            FrameCamera(perception);
        }

        private void FrameCamera(PerceptionLite perception)
        {
            bool any = false;
            int minX = 0, minY = 0, maxX = 0, maxY = 0;
            foreach (var v in perception.Visuals.Values)
            {
                int x = v.Location.X, y = v.Location.Y;
                if (!any) { minX = maxX = x; minY = maxY = y; any = true; continue; }
                if (x < minX) minX = x;
                if (x > maxX) maxX = x;
                if (y < minY) minY = y;
                if (y > maxY) maxY = y;
            }

            var cam = Camera.main;
            if (cam == null)
            {
                var camGo = new GameObject("Demo Camera") { tag = "MainCamera" };
                camGo.transform.SetParent(transform, false);
                cam = camGo.AddComponent<Camera>();
            }

            float cx = (minX + maxX + 1) * 0.5f;
            float cy = (minY + maxY + 1) * 0.5f;
            float halfW = (maxX - minX + 1) * 0.5f + cameraPadding;
            float halfH = (maxY - minY + 1) * 0.5f + cameraPadding;

            cam.orthographic = true;
            cam.transform.position = new Vector3(cx, cy, -10f);
            cam.transform.rotation = Quaternion.identity;
            cam.orthographicSize = Mathf.Max(halfH, halfW / Mathf.Max(cam.aspect, 0.1f));
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = backgroundColor;
        }

        private static PerceptionLite? LoadFrame(string fileName)
        {
            try
            {
                string path = Path.Combine(Application.streamingAssetsPath, "PerceptionFrames", fileName);
                if (!File.Exists(path))
                    return null;
                return JsonConvert.DeserializeObject<PerceptionLite>(File.ReadAllText(path));
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// A built-in irregular lake (with a small sandy island) ringed by sand + grass,
        /// under a warm golden-hour ambient tint. Used when the JSON fixture is absent
        /// and by tests. Deterministic (no randomness).
        /// </summary>
        public static PerceptionLite BuiltInLake()
        {
            var perception = new PerceptionLite
            {
                Topology = "square",
                AmbientTint = new AmbientTintLite(1.0f, 0.86f, 0.68f),
                PlayerLocation = new WorldLocationLite(-5, -5, 0),
            };

            const int R = 7;
            for (int y = -R; y <= R; y++)
            {
                for (int x = -R; x <= R; x++)
                {
                    string terrain = ClassifyCell(x, y);
                    float light = Mathf.Clamp01(0.72f + 0.22f * ((y + R) / (float)(2 * R)));
                    perception.Visuals[$"{x},{y},0"] =
                        new VisualLite(new WorldLocationLite(x, y, 0), terrain, light);
                }
            }

            perception.TileTypes["water"] = new TileTypeLite { Name = "water" };
            perception.TileTypes["sand"] = new TileTypeLite { Name = "sand" };
            perception.TileTypes["grass"] = new TileTypeLite { Name = "grass" };
            return perception;
        }

        private static string ClassifyCell(int x, int y)
        {
            if (IsWater(x, y))
                return "water";

            // Sand where a non-water cell touches water (8-neighbour); grass otherwise.
            for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                    if (IsWater(x + dx, y + dy))
                        return "sand";

            return "grass";
        }

        private static bool IsWater(int x, int y)
        {
            float ang = Mathf.Atan2(y, x);
            float r = 4.2f + 0.9f * Mathf.Sin(ang * 3f) + 0.5f * Mathf.Cos(ang * 2f - 0.6f);
            bool water = (x * x + y * y) < r * r;

            // A small island toward the north-east of the lake.
            if ((x - 2) * (x - 2) + (y - 1) * (y - 1) < 1.6f)
                water = false;

            return water;
        }
    }
}
