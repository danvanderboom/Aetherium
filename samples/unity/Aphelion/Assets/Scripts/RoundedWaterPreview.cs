#nullable enable
using System.Collections.Generic;
using Aetherium.Unity.Rounded;
using UnityEngine;

namespace Aphelion
{
    /// <summary>
    /// Offline, server-free preview of the shared client's rounded-region rendering: builds a
    /// deterministic lake (with a plus-shaped island) and hands it straight to the package's
    /// <see cref="RoundedRegionRenderer"/> — the exact code path <c>GridMapView</c> drives from a
    /// live perception frame, minus the SignalR connection. Drop this on an empty GameObject and
    /// press Play (or use menu <b>Aetherium → Build Rounded Water Preview</b>) to eyeball the
    /// curved coastline, animated foam, and shallows→deep gradient in the 3D Aphelion pipeline
    /// before wiring it to the live Overworld.
    /// </summary>
    public sealed class RoundedWaterPreview : MonoBehaviour
    {
        [Tooltip("World units per grid cell (match GridMapView / EntityViewRegistry).")]
        [SerializeField] private float cellSize = 1f;

        [Tooltip("Lake radius in cells.")]
        [SerializeField] private int radius = 7;

        [Tooltip("Optional water material. Leave empty to use the Aetherium/RoundedWater shader.")]
        [SerializeField] private Material? waterMaterial;

        [SerializeField] private int smoothIterations = 2;
        [SerializeField] private int subdivisions = 3;
        [SerializeField] private Color groundColor = new Color(0.80f, 0.72f, 0.52f);
        [SerializeField] private Color skyColor = new Color(0.55f, 0.72f, 0.90f);

        private readonly RoundedRegionRenderer _renderer = new RoundedRegionRenderer();

        private void Start() => Build();

        /// <summary>Builds the ground plane, the rounded-water mesh, and frames the camera.</summary>
        public void Build()
        {
            BuildGround();
            _renderer.Sync(
                transform, LakeCells(), _ => waterMaterial,
                cellSize, smoothIterations, subdivisions, Color.white, 0.02f);
            FrameCamera();
        }

        private List<(string terrain, int x, int y, int z)> LakeCells()
        {
            var cells = new List<(string, int, int, int)>();
            for (int y = -radius; y <= radius; y++)
                for (int x = -radius; x <= radius; x++)
                    if (IsWater(x, y))
                        cells.Add(("Water", x, y, 0));
            return cells;
        }

        // Perturbed circle (so the coast isn't a bland disc) minus a small plus-shaped island,
        // which exercises the winding-number hole handling — the island must punch cleanly
        // through the water surface, not bleed over.
        private bool IsWater(int x, int y)
        {
            float ang = Mathf.Atan2(y, x);
            float wobble = 0.92f + 0.16f * Mathf.Sin(ang * 3f) + 0.09f * Mathf.Cos(ang * 5f);
            float r = radius * 0.85f * wobble;
            bool inLake = (x * x + y * y) <= r * r;
            bool island = Mathf.Abs(x - 2) + Mathf.Abs(y - 1) <= 1; // 5-cell plus
            return inLake && !island;
        }

        private void BuildGround()
        {
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane); // Plane lies in XZ, 10u across
            ground.name = "PreviewGround";
            ground.transform.SetParent(transform, false);
            var collider = ground.GetComponent<Collider>();
            if (collider != null)
                Destroy(collider);

            float span = (radius * 2 + 6) * cellSize;
            ground.transform.localScale = new Vector3(span / 10f, 1f, span / 10f);
            ground.transform.localPosition = Vector3.zero; // water sits at y=0.02, just above

            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            if (shader != null)
            {
                var mat = new Material(shader) { name = "PreviewGround" };
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", groundColor);
                mat.color = groundColor;
                ground.GetComponent<Renderer>().sharedMaterial = mat;
            }
        }

        private void FrameCamera()
        {
            var cam = Camera.main;
            if (cam == null)
            {
                var go = new GameObject("Main Camera") { tag = "MainCamera" };
                cam = go.AddComponent<Camera>();
                go.AddComponent<AudioListener>();
            }

            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = skyColor;
            cam.orthographic = false;
            float d = radius * cellSize;
            cam.transform.position = new Vector3(0f, d * 1.7f, -d * 1.4f);
            cam.transform.LookAt(Vector3.zero);
        }
    }
}
