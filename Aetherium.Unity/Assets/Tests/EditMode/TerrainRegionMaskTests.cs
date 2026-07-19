using Aetherium.Unity.Model;
using Aetherium.Unity.Rendering.Water;
using NUnit.Framework;

namespace Aetherium.Unity.Tests
{
    public class TerrainRegionMaskTests
    {
        private static PerceptionLite Frame(params (int x, int y, int z, string terrain)[] cells)
        {
            var perception = new PerceptionLite();
            foreach (var (x, y, z, terrain) in cells)
                perception.Visuals[$"{x},{y},{z}"] = new VisualLite(new WorldLocationLite(x, y, z), terrain, 1.0);
            return perception;
        }

        [Test]
        public void RegionTerrains_IsRegion_WaterAndLava_CaseInsensitive()
        {
            Assert.IsTrue(RegionTerrains.IsRegion("water"));
            Assert.IsTrue(RegionTerrains.IsRegion("WATER"));
            Assert.IsTrue(RegionTerrains.IsRegion("Lava"));
            Assert.IsFalse(RegionTerrains.IsRegion("grass"));
            Assert.IsFalse(RegionTerrains.IsRegion(string.Empty));
            Assert.IsFalse(RegionTerrains.IsRegion(null));
        }

        [Test]
        public void RegionTerrains_ResolveName_PrefersTileTypeName()
        {
            var perception = new PerceptionLite();
            perception.TileTypes["w"] = new TileTypeLite { Name = "water" };
            Assert.AreEqual("water", RegionTerrains.ResolveName(perception, "w"));
            Assert.AreEqual("grass", RegionTerrains.ResolveName(perception, "grass"));
        }

        [Test]
        public void Build_PicksOnlyRegionCells()
        {
            var frame = Frame((0, 0, 0, "water"), (1, 0, 0, "water"), (2, 0, 0, "grass"));
            var mask = TerrainRegionMask.Build(frame, 0);
            Assert.AreEqual(2, mask.Count);
            Assert.IsTrue(mask.Contains(0, 0));
            Assert.IsTrue(mask.Contains(1, 0));
            Assert.IsFalse(mask.Contains(2, 0));
        }

        [Test]
        public void Build_RespectsZBand()
        {
            var frame = Frame((0, 0, 0, "water"), (0, 0, 1, "water"));
            var z0 = TerrainRegionMask.Build(frame, 0);
            var z1 = TerrainRegionMask.Build(frame, 1);
            Assert.AreEqual(1, z0.Count);
            Assert.AreEqual(1, z1.Count);
            Assert.IsTrue(z0.Contains(0, 0));
        }

        [Test]
        public void Build_EmptyWhenNoRegion()
        {
            var frame = Frame((0, 0, 0, "grass"), (1, 0, 0, "stone"));
            var mask = TerrainRegionMask.Build(frame, 0);
            Assert.IsTrue(mask.IsEmpty);
            Assert.AreEqual(0, mask.Count);
        }

        [Test]
        public void Build_ComputesInclusiveBounds()
        {
            var frame = Frame((2, 3, 0, "water"), (5, 7, 0, "water"));
            var mask = TerrainRegionMask.Build(frame, 0);
            Assert.AreEqual(2, mask.MinX);
            Assert.AreEqual(5, mask.MaxX);
            Assert.AreEqual(3, mask.MinY);
            Assert.AreEqual(7, mask.MaxY);
        }

        [Test]
        public void Build_UsesCustomPredicate()
        {
            var frame = Frame((0, 0, 0, "grass"), (1, 0, 0, "water"));
            var mask = TerrainRegionMask.Build(frame, 0, name => name == "grass");
            Assert.AreEqual(1, mask.Count);
            Assert.IsTrue(mask.Contains(0, 0));
            Assert.IsFalse(mask.Contains(1, 0));
        }
    }
}
