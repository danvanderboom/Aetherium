using System.Collections.Generic;
using Aetherium.Unity.Demo;
using NUnit.Framework;
using UnityEngine;

namespace Aetherium.Unity.Tests
{
    public class RoundedTerrainDemoTests
    {
        [Test]
        public void Build_ComposesLandWaterAndCamera()
        {
            var go = new GameObject("Demo");
            var demo = go.AddComponent<RoundedTerrainDemo>();
            demo.Build(RoundedTerrainDemo.BuiltInLake());

            Assert.IsNotNull(demo.Land, "band-stack land renderer created");
            Assert.IsNotNull(demo.Water, "water renderer created");
            Assert.IsTrue(demo.Land!.SkipRegionTerrain, "land skips water cells");
            Assert.IsTrue(demo.Land!.ApplyLighting, "lighting is on");

            var mesh = demo.Water!.GetBandMesh(0);
            Assert.IsNotNull(mesh, "water band 0 exists");
            Assert.Greater(mesh!.vertexCount, 0, "water mesh is non-empty");

            Assert.IsNotNull(Camera.main, "a camera is present/created");

            Object.DestroyImmediate(go);
        }

        [Test]
        public void BuiltInLake_HasWaterSandGrassAndTint()
        {
            var perception = RoundedTerrainDemo.BuiltInLake();

            var terrains = new HashSet<string>();
            foreach (var v in perception.Visuals.Values)
                terrains.Add(v.TileTypeId);

            Assert.IsTrue(terrains.Contains("water"), "lake has water");
            Assert.IsTrue(terrains.Contains("sand"), "shore has sand");
            Assert.IsTrue(terrains.Contains("grass"), "surroundings have grass");
            Assert.IsNotNull(perception.AmbientTint, "warm ambient tint set");
        }
    }
}
