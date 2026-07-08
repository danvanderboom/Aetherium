using System.Linq;
using NUnit.Framework;
using Aetherium.Model.ContentAtlas;
using Aetherium.WorldBuilders;

namespace Aetherium.Test.ContentAtlas
{
    /// <summary>
    /// The design doc's "null renderer" idea (engine gap-analysis §4.10), applied at the schema
    /// level since no DTO references atlas ids yet (Phase 1): asserts the seed atlas never falls
    /// behind the real terrain vocabulary <see cref="TorusWorldBuilder"/> actually produces. Add a
    /// tile type without updating <c>DefaultContentAtlas</c>, and this test fails.
    /// </summary>
    [TestFixture]
    public class DefaultContentAtlasCoverageTests
    {
        // Mirrors TorusWorldBuilder's private TerrainTypeNames filter — only these tile types are
        // real *terrain*; "Player"/"Monster"/"DeadMonster" are entity-overlay glyphs, not terrain.
        private static readonly string[] TerrainOnlyNames =
        {
            "None", "Indoors", "Wall", "Mountain", "Road",
            "Plains", "Forest", "Water", "Cave", "Upstairs", "Downstairs",
        };

        [Test]
        public void EveryRealTerrainTileType_ResolvesToATerrainTag()
        {
            var builder = new TorusWorldBuilder();
            var realNames = builder.TileTypes.Select(t => t.Name)
                .Where(n => TerrainOnlyNames.Contains(n))
                .ToList();

            Assert.That(realNames, Is.Not.Empty, "Sanity check: TorusWorldBuilder must still produce terrain tile types.");

            var atlas = Server.ContentAtlas.DefaultContentAtlas.Build();
            var missing = realNames
                .Where(n => !atlas.Contains(ContentAtlasCategory.Terrain, n.ToLowerInvariant()))
                .ToList();

            Assert.That(missing, Is.Empty,
                $"DefaultContentAtlas is missing TerrainTag(s) for: {string.Join(", ", missing)}. " +
                "Add a matching TerrainTag in Aetherium.Server/ContentAtlas/DefaultContentAtlas.cs.");
        }

        [Test]
        public void TorusWorldBuilder_HasNotGrownNewTerrainTypes_WithoutUpdatingThisTest()
        {
            var builder = new TorusWorldBuilder();
            var actualTerrainNames = builder.TileTypes.Select(t => t.Name)
                .Where(n => TerrainOnlyNames.Contains(n))
                .OrderBy(n => n)
                .ToList();

            Assert.That(actualTerrainNames, Is.EqualTo(TerrainOnlyNames.OrderBy(n => n).ToList()),
                "TorusWorldBuilder's terrain tile-type set changed — update TerrainOnlyNames here and DefaultContentAtlas together.");
        }
    }
}
