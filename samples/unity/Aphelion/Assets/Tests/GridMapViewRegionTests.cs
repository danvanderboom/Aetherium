using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Aetherium.Client;
using Aetherium.Unity;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Aphelion.Tests
{
    /// <summary>
    /// Nails down the GridMapView ↔ RoundedRegionRenderer integration the live Overworld exercises:
    /// on a square topology, terrain the theme marks as a "region" (Water) must NOT spawn a prefab
    /// per cell (no double-draw) and MUST contribute to the smooth region mesh; other terrain still
    /// spawns prefabs. Drives GridMapView.RenderCells directly so no server/SignalR is needed.
    /// </summary>
    public class GridMapViewRegionTests
    {
        private static GridMapView NewView(ThemeAsset theme)
        {
            // Build inactive so we can disable auto-connect before Awake/Start run.
            var go = new GameObject("rig");
            go.SetActive(false);
            var behaviour = go.AddComponent<AetheriumClientBehaviour>();
            typeof(AetheriumClientBehaviour)
                .GetField("autoConnect", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.SetValue(behaviour, false);
            var view = go.AddComponent<GridMapView>();
            go.SetActive(true); // Awake runs now (client is constructed but never connects)
            view.Theme = theme;
            return view;
        }

        private static List<GridMapView.CellInput> Lake(int half)
        {
            // A (2·half+1)² field: a 3×3 water blob at the centre, everything else "Grass".
            var cells = new List<GridMapView.CellInput>();
            for (int y = -half; y <= half; y++)
                for (int x = -half; x <= half; x++)
                {
                    bool water = System.Math.Abs(x) <= 1 && System.Math.Abs(y) <= 1;
                    cells.Add(new GridMapView.CellInput(
                        new GridPoint(x, y, 0), water ? "Water" : "Grass", true, 1.0));
                }
            return cells;
        }

        [UnityTest]
        public IEnumerator RegionTerrain_SkipsPrefabs_AndBuildsMesh()
        {
            var theme = ScriptableObject.CreateInstance<ThemeAsset>();
            theme.RegisterRegionTerrain("Water", null); // null material → runtime rounded-water fallback
            var view = NewView(theme);
            yield return null;

            view.RenderCells(Lake(3), GridPoint.Origin, "square");
            yield return null;

            int cellPrefabs = 0, regionVerts = 0;
            foreach (Transform child in view.transform)
            {
                if (child.name.StartsWith("cell:")) cellPrefabs++;
                if (child.name.StartsWith("region:Water"))
                {
                    var mf = child.GetComponent<MeshFilter>();
                    if (mf != null && mf.sharedMesh != null) regionVerts = mf.sharedMesh.vertexCount;
                }
            }

            // 7×7 = 49 cells, 9 of them water → 40 prefabs, zero water prefabs, one region mesh.
            Assert.AreEqual(40, cellPrefabs, "only the 40 non-water cells become prefabs; water is skipped");
            Assert.Greater(regionVerts, 0, "the water region mesh was built");

            Object.Destroy(view.gameObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator NonSquareTopology_FallsBackToPrefabs()
        {
            var theme = ScriptableObject.CreateInstance<ThemeAsset>();
            theme.RegisterRegionTerrain("Water", null);
            var view = NewView(theme);
            yield return null;

            // On hex, the region mesh math doesn't apply → every cell (incl. water) is a prefab.
            view.RenderCells(Lake(3), GridPoint.Origin, "hex");
            yield return null;

            int cellPrefabs = 0; bool regionMesh = false;
            foreach (Transform child in view.transform)
            {
                if (child.name.StartsWith("cell:")) cellPrefabs++;
                if (child.name.StartsWith("region:")) regionMesh = true;
            }

            Assert.AreEqual(49, cellPrefabs, "non-square topology draws every cell as a prefab");
            Assert.IsFalse(regionMesh, "no region mesh on a non-square topology");

            Object.Destroy(view.gameObject);
            yield return null;
        }
    }
}
