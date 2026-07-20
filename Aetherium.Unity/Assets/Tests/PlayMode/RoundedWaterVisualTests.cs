#nullable enable
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Aetherium.Unity.Demo;
using Aetherium.Unity.Model;
using Aetherium.Unity.Rendering.Water;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Aetherium.Unity.Tests
{
    /// <summary>
    /// The rounded-water "does it actually look right" sign-off, automated.
    ///
    /// Task 3.3 of add-rounded-terrain-rendering was a human, in-Editor visual check
    /// (foam hugs the coast, shallows→deep gradient reads, waves animate, the island
    /// isn't washed over, the scene is warm). Everything except raw aesthetic taste is
    /// a *measurable* property of the rendered pixels, so this renders the demo lake to
    /// a RenderTexture and asserts those properties directly — turning the manual sign-off
    /// into a repeatable regression. It also dumps the frames to PNG (see
    /// <see cref="CaptureDir"/>) so a human can still eyeball the aesthetics.
    ///
    /// Needs a real graphics device (URP can't render under <c>-nographics</c>); the test
    /// self-skips (Assert.Ignore) when none is present, so it stays green in headless CI
    /// and only does real work when run with graphics.
    /// </summary>
    public class RoundedWaterVisualTests
    {
        private const int Rt = 512;                 // square render target
        private const string ShaderName = "Aetherium/RoundedWater";

        private static string CaptureDir =>
            Environment.GetEnvironmentVariable("ROUNDED_WATER_CAPTURE_DIR")
            ?? Path.Combine(Application.temporaryCachePath, "rounded-water-verify");

        private GameObject? root;
        private RenderTexture? rt;

        [SetUp]
        public void SetUp()
        {
            if (!HasGraphics)
                return;

            // A material off the real shader must be resolvable, or there is nothing to verify.
            Assert.IsNotNull(Shader.Find(ShaderName),
                $"Shader '{ShaderName}' not found — the URP water shader failed to import.");
        }

        [TearDown]
        public void TearDown()
        {
            if (root != null) UnityEngine.Object.DestroyImmediate(root);
            if (Camera.main != null) UnityEngine.Object.DestroyImmediate(Camera.main.gameObject);
            if (rt != null) { rt.Release(); UnityEngine.Object.DestroyImmediate(rt); rt = null; }
        }

        private static bool HasGraphics =>
            SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null;

        [UnityTest]
        public IEnumerator RoundedWater_LooksRight_FoamGradientIslandTintAndWaves()
        {
            if (!HasGraphics)
            {
                Assert.Ignore("No graphics device (running -nographics); pixel sign-off skipped.");
                yield break;
            }

            Directory.CreateDirectory(CaptureDir);

            // --- Scene: the demo's built-in warm lake with a sandy island. ---
            var frame = RoundedTerrainDemo.BuiltInLake();
            var cam = BuildSceneAndCamera(frame);

            // Let URP draw the target-texture camera for a couple of frames.
            yield return RenderSettle();

            var warm = ReadBack();
            SavePng(warm, "01-warm-lake.png");

            // ---- 1. Water actually renders: the deep centre is a blue-ish water pixel. ----
            var waterCells = WaterCells(frame);
            var shoreDist = ShoreDistances(frame, waterCells);
            (int cx, int cy) = DeepestCell(shoreDist);
            Color deep = SampleCell(cam, warm, cx, cy);
            Assert.Greater(deep.b, deep.r + 0.05f,
                $"deep water at ({cx},{cy}) should read blue (got {deep}).");
            Assert.Greater(Luminance(deep), 0.02f, "deep water should not be the empty background.");

            // ---- 2. Shallows/foam brighter near the shore than in the deep interior. ----
            //   shore band = water cells one step from land; deep band = the farthest ring.
            float shoreLum = MeanLuminance(cam, warm, CellsAtShoreDistance(shoreDist, 1));
            int deepBand = MaxShoreDistance(shoreDist);
            var deepCells = new List<(int, int)>();
            for (int d = deepBand; d >= 2 && deepCells.Count < 3; d--)
                deepCells.AddRange(CellsAtShoreDistance(shoreDist, d));
            float deepLum = MeanLuminance(cam, warm, deepCells);
            Assert.Greater(shoreLum, deepLum + 0.02f,
                $"shore water (lum {shoreLum:F3}) should be brighter than deep water (lum {deepLum:F3}) " +
                "— the foam + shallows gradient.");

            // ---- 3. The sandy island reads as warm land, not washed over by blue water. ----
            (int ix, int iy) = IslandCell(frame);
            Color island = SampleCell(cam, warm, ix, iy);
            Assert.Greater(island.r, island.b,
                $"island cell ({ix},{iy}) should read as warm land, not blue water (got {island}).");

            // ---- 4. Waves/foam animate: pixels shift over time. ----
            yield return new WaitForSeconds(0.4f);
            yield return RenderSettle();
            var later = ReadBack();
            SavePng(later, "02-warm-lake-later.png");
            float changed = FractionChanged(cam, warm, later, waterCells, 0.012f);
            Assert.Greater(changed, 0.05f,
                $"only {changed:P0} of water pixels moved between frames — waves/foam are not animating.");

            // ---- 5. Warm ambient tint actually warms the scene vs. a neutral render. ----
            TearDownScene();
            var neutral = RoundedTerrainDemo.BuiltInLake();
            neutral.AmbientTint = new AmbientTintLite(1f, 1f, 1f); // no tint
            var cam2 = BuildSceneAndCamera(neutral);
            yield return RenderSettle();
            var neutralTex = ReadBack();
            SavePng(neutralTex, "03-neutral-lake.png");

            float warmRB = MeanRedMinusBlue(warm);
            float neutralRB = MeanRedMinusBlue(neutralTex);
            Assert.Greater(warmRB, neutralRB + 0.01f,
                $"warm-tinted render (R-B {warmRB:F3}) should be warmer than the neutral one " +
                $"(R-B {neutralRB:F3}).");

            UnityEngine.Object.DestroyImmediate(warm);
            UnityEngine.Object.DestroyImmediate(later);
            UnityEngine.Object.DestroyImmediate(neutralTex);

            Debug.Log($"[RoundedWaterVisual] captures written to: {CaptureDir}");
        }

        // ---------- scene / camera ----------

        private Camera BuildSceneAndCamera(PerceptionLite frame)
        {
            root = new GameObject("RoundedWaterVisualRoot");
            var demo = root.AddComponent<RoundedTerrainDemo>();
            demo.Build(frame);

            var cam = Camera.main!;
            rt ??= new RenderTexture(Rt, Rt, 24) { name = "RoundedWaterRT" };
            cam.targetTexture = rt;

            // Frame the whole ±7 lake squarely regardless of the demo's aspect assumptions.
            cam.aspect = 1f;
            cam.orthographic = true;
            cam.orthographicSize = 8.5f;
            cam.transform.position = new Vector3(0.5f, 0.5f, -10f);
            cam.transform.rotation = Quaternion.identity;
            return cam;
        }

        private void TearDownScene()
        {
            if (root != null) { UnityEngine.Object.DestroyImmediate(root); root = null; }
            if (Camera.main != null) UnityEngine.Object.DestroyImmediate(Camera.main.gameObject);
        }

        private static IEnumerator RenderSettle()
        {
            // URP renders an enabled target-texture camera during the normal loop; a couple
            // of frames guarantees the RT holds a completed render (WaitForEndOfFrame is
            // unreliable in batchmode, so we advance whole frames instead).
            yield return null;
            yield return null;
            yield return null;
        }

        private Texture2D ReadBack()
        {
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(Rt, Rt, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, Rt, Rt), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            return tex;
        }

        private static void SavePng(Texture2D tex, string name)
        {
            try
            {
                File.WriteAllBytes(Path.Combine(CaptureDir, name), tex.EncodeToPNG());
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[RoundedWaterVisual] could not write {name}: {e.Message}");
            }
        }

        // ---------- sampling ----------

        private static Color SampleCell(Camera cam, Texture2D tex, int cellX, int cellY)
        {
            // Cell (x,y) occupies world [x,x+1) — its centre is (x+0.5, y+0.5).
            Vector3 sp = cam.WorldToScreenPoint(new Vector3(cellX + 0.5f, cellY + 0.5f, 0f));
            int px = Mathf.Clamp(Mathf.RoundToInt(sp.x), 0, tex.width - 1);
            int py = Mathf.Clamp(Mathf.RoundToInt(sp.y), 0, tex.height - 1);
            return tex.GetPixel(px, py);
        }

        private static float MeanLuminance(Camera cam, Texture2D tex, IReadOnlyCollection<(int x, int y)> cells)
        {
            if (cells.Count == 0) return 0f;
            float sum = 0f;
            foreach (var (x, y) in cells) sum += Luminance(SampleCell(cam, tex, x, y));
            return sum / cells.Count;
        }

        private static float FractionChanged(Camera cam, Texture2D a, Texture2D b,
            IReadOnlyCollection<(int x, int y)> cells, float threshold)
        {
            if (cells.Count == 0) return 0f;
            int moved = 0;
            foreach (var (x, y) in cells)
            {
                Color ca = SampleCell(cam, a, x, y);
                Color cb = SampleCell(cam, b, x, y);
                float d = Mathf.Abs(ca.r - cb.r) + Mathf.Abs(ca.g - cb.g) + Mathf.Abs(ca.b - cb.b);
                if (d > threshold) moved++;
            }
            return moved / (float)cells.Count;
        }

        private static float Luminance(Color c) => 0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b;

        private static float MeanRedMinusBlue(Texture2D tex)
        {
            var px = tex.GetPixels();
            float sum = 0f;
            foreach (var c in px) sum += c.r - c.b;
            return sum / px.Length;
        }

        // ---------- geometry over the frame ----------

        private static bool IsWater(PerceptionLite frame, int x, int y)
            => frame.Visuals.TryGetValue($"{x},{y},0", out var v)
               && RegionTerrains.IsRegionVisual(frame, v);

        private static List<(int x, int y)> WaterCells(PerceptionLite frame)
        {
            var list = new List<(int, int)>();
            foreach (var v in frame.Visuals.Values)
                if (RegionTerrains.IsRegionVisual(frame, v))
                    list.Add((v.Location.X, v.Location.Y));
            return list;
        }

        // Chebyshev distance from each water cell to the nearest non-water cell (shore = 1).
        private static Dictionary<(int, int), int> ShoreDistances(
            PerceptionLite frame, List<(int x, int y)> water)
        {
            var dist = new Dictionary<(int, int), int>();
            foreach (var (x, y) in water)
            {
                int d = 0;
                bool found = false;
                for (int ring = 1; ring <= 16 && !found; ring++)
                {
                    for (int dy = -ring; dy <= ring && !found; dy++)
                        for (int dx = -ring; dx <= ring && !found; dx++)
                        {
                            if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy)) != ring) continue;
                            if (!IsWater(frame, x + dx, y + dy)) { d = ring; found = true; }
                        }
                }
                dist[(x, y)] = found ? d : 99;
            }
            return dist;
        }

        private static (int, int) DeepestCell(Dictionary<(int, int), int> shore)
        {
            (int, int) best = (0, 0);
            int bestD = -1;
            foreach (var kv in shore)
                if (kv.Value < 99 && kv.Value > bestD) { bestD = kv.Value; best = kv.Key; }
            return best;
        }

        private static int MaxShoreDistance(Dictionary<(int, int), int> shore)
        {
            int m = 0;
            foreach (var kv in shore) if (kv.Value < 99) m = Mathf.Max(m, kv.Value);
            return m;
        }

        private static List<(int, int)> CellsAtShoreDistance(Dictionary<(int, int), int> shore, int d)
        {
            var list = new List<(int, int)>();
            foreach (var kv in shore) if (kv.Value == d) list.Add(kv.Key);
            return list;
        }

        // A land cell fully enclosed by water on all 4 sides — the island interior.
        private static (int x, int y) IslandCell(PerceptionLite frame)
        {
            foreach (var v in frame.Visuals.Values)
            {
                int x = v.Location.X, y = v.Location.Y;
                if (IsWater(frame, x, y)) continue;
                if (IsWater(frame, x + 1, y) && IsWater(frame, x - 1, y) &&
                    IsWater(frame, x, y + 1) && IsWater(frame, x, y - 1))
                    return (x, y);
            }
            // Fallback to the known island centre if the enclosure test finds nothing.
            return (2, 1);
        }
    }
}
